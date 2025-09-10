using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XIVSubmarinesReturn.Services
{
    public interface INotionClient
    {
        Task UpsertSnapshotAsync(SubmarineSnapshot snap, bool latestOnly, CancellationToken ct = default);
    }

    internal sealed class NotionClient : INotionClient
    {
        private readonly Configuration _cfg;
        private readonly Dalamud.Plugin.Services.IPluginLog _log;
        private readonly HttpClient _http;
        private const string NotionVersion = "2022-06-28";

        public NotionClient(Configuration cfg, Dalamud.Plugin.Services.IPluginLog log, HttpClient http)
        {
            _cfg = cfg; _log = log; _http = http;
        }

        public async Task UpsertSnapshotAsync(SubmarineSnapshot snap, bool latestOnly, CancellationToken ct = default)
        {
            try
            {
                if (snap is null) return;
                if (!_cfg.NotionEnabled) return;
                if (string.IsNullOrWhiteSpace(_cfg.NotionToken) || string.IsNullOrWhiteSpace(_cfg.NotionDatabaseId)) return;
                var items = (snap.Items ?? new List<SubmarineRecord>()).ToList();
                if (items.Count == 0) return;
                if (latestOnly)
                {
                    var nearest = items.Where(x => x.EtaUnix.HasValue).OrderBy(x => x.EtaUnix!.Value).FirstOrDefault() ?? items[0];
                    items = new List<SubmarineRecord> { nearest };
                }
                foreach (var it in items)
                {
                    await UpsertOneAsync(snap, it, ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion upsert failed");
                XsrDebug.Log(_cfg, "Notion upsert failed", ex);
            }
        }

        private async Task UpsertOneAsync(SubmarineSnapshot snap, SubmarineRecord it, CancellationToken ct)
        {
            try
            {
                var token = _cfg.NotionToken;
                var dbid = _cfg.NotionDatabaseId;
                var extId = BuildStableId(snap, it);

                var pageId = await TryFindPageIdByExtIdAsync(dbid, extId, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(pageId))
                {
                    await UpdatePageAsync(pageId!, snap, it, extId, ct).ConfigureAwait(false);
                }
                else
                {
                    await CreatePageAsync(dbid, snap, it, extId, ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion upsert item failed");
                XsrDebug.Log(_cfg, "Notion upsert item failed", ex);
            }
        }

        private async Task<string?> TryFindPageIdByExtIdAsync(string databaseId, string extId, CancellationToken ct)
        {
            try
            {
                var url = $"https://api.notion.com/v1/databases/{databaseId}/query";
                var filter = new
                {
                    filter = new
                    {
                        property = _cfg.NotionPropExtId,
                        rich_text = new { equals = extId }
                    },
                    page_size = 1
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(filter), Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.NotionToken);
                req.Headers.Add("Notion-Version", NotionVersion);
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    if (_cfg.DebugLogging)
                        _log.Warning($"Notion query failed: {(int)resp.StatusCode} {resp.ReasonPhrase} body={body}");
                    else
                        _log.Warning($"Notion query failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    return null;
                }
                using var doc = JsonDocument.Parse(body);
                var arr = doc.RootElement.GetProperty("results");
                if (arr.GetArrayLength() > 0)
                {
                    var id = arr[0].GetProperty("id").GetString();
                    return id;
                }
                return null;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion query exception");
                XsrDebug.Log(_cfg, "Notion query exception", ex);
                return null;
            }
        }

        private async Task CreatePageAsync(string databaseId, SubmarineSnapshot snap, SubmarineRecord it, string extId, CancellationToken ct)
        {
            var url = "https://api.notion.com/v1/pages";
            var payload = new
            {
                parent = new { database_id = databaseId },
                properties = BuildProperties(snap, it, extId)
            };
            await SendAsync(url, HttpMethod.Post, payload, ct).ConfigureAwait(false);
        }

        private async Task UpdatePageAsync(string pageId, SubmarineSnapshot snap, SubmarineRecord it, string extId, CancellationToken ct)
        {
            var url = $"https://api.notion.com/v1/pages/{pageId}";
            var payload = new { properties = BuildProperties(snap, it, extId) };
            await SendAsync(url, HttpMethod.Patch, payload, ct).ConfigureAwait(false);
        }

        private async Task SendAsync(string url, HttpMethod method, object payload, CancellationToken ct)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(method, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.NotionToken);
                req.Headers.Add("Notion-Version", NotionVersion);
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                if ((int)resp.StatusCode == 429)
                {
                    var retry = 1;
                    try
                    {
                        if (resp.Headers.TryGetValues("Retry-After", out var vals))
                        {
                            var v = vals.FirstOrDefault();
                            if (int.TryParse(v, out var sec)) retry = Math.Max(1, sec);
                        }
                    }
                    catch { }
                    await Task.Delay(TimeSpan.FromSeconds(retry), ct).ConfigureAwait(false);
                    using var req2 = new HttpRequestMessage(method, url)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.NotionToken);
                    req2.Headers.Add("Notion-Version", NotionVersion);
                    using var resp2 = await _http.SendAsync(req2, ct).ConfigureAwait(false);
                    if (!resp2.IsSuccessStatusCode)
                    {
                        var body2 = await resp2.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        if (_cfg.DebugLogging)
                            _log.Warning($"Notion send failed (retry): {(int)resp2.StatusCode} {resp2.ReasonPhrase} body={body2}");
                        else
                            _log.Warning($"Notion send failed (retry): {(int)resp2.StatusCode} {resp2.ReasonPhrase}");
                        XsrDebug.Log(_cfg, $"Notion send failed (retry): {(int)resp2.StatusCode}");
                    }
                }
                else if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    if (_cfg.DebugLogging)
                        _log.Warning($"Notion send failed: {(int)resp.StatusCode} {resp.ReasonPhrase} body={body}");
                    else
                        _log.Warning($"Notion send failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    XsrDebug.Log(_cfg, $"Notion send failed: {(int)resp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion send exception");
                XsrDebug.Log(_cfg, "Notion send exception", ex);
            }
        }

        private object BuildProperties(SubmarineSnapshot snap, SubmarineRecord it, string extId)
        {
            // Build Notion properties using configured names
            var props = new Dictionary<string, object>(StringComparer.Ordinal);
            string nameProp = _cfg.NotionPropName ?? "Name";
            string slotProp = _cfg.NotionPropSlot ?? "Slot";
            string etaProp = _cfg.NotionPropEta ?? "ETA";
            string routeProp = _cfg.NotionPropRoute ?? "Route";
            string rankProp = _cfg.NotionPropRank ?? "Rank";
            string extProp = _cfg.NotionPropExtId ?? "ExtId";
            string remProp = _cfg.NotionPropRemaining ?? "Remaining";
            string worldProp = _cfg.NotionPropWorld ?? "World";
            string charProp = _cfg.NotionPropCharacter ?? "Character";
            string fcProp = _cfg.NotionPropFC ?? "FC";

            var title = (it.Name ?? string.Empty);
            props[nameProp] = new { title = new[] { new { text = new { content = title } } } };

            if (it.Slot.HasValue)
                props[slotProp] = new { number = (double?)it.Slot.Value };

            if (it.Rank.HasValue)
                props[rankProp] = new { number = (double?)it.Rank.Value };

            if (!string.IsNullOrWhiteSpace(it.RouteKey))
                props[routeProp] = new { rich_text = new[] { new { text = new { content = it.RouteKey } } } };

            if (it.EtaUnix.HasValue)
            {
                // Notion date with explicit time_zone and start without UTC offset (Asia/Tokyo)
                var tok = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value).ToOffset(TimeSpan.FromHours(9));
                props[etaProp] = new { date = new { start = tok.ToString("yyyy-MM-dd'T'HH:mm:ss"), time_zone = "Asia/Tokyo" } };
            }

            // external id for upsert
            props[extProp] = new { rich_text = new[] { new { text = new { content = extId } } } };

            // Remaining text (human-friendly)
            try
            {
                var remaining = (it.Extra != null && it.Extra.TryGetValue("RemainingText", out var r)) ? r : string.Empty;
                if (!string.IsNullOrWhiteSpace(remaining))
                    props[remProp] = new { rich_text = new[] { new { text = new { content = remaining } } } };
            }
            catch { }

            // Identity (from snapshot)
            try
            {
                if (!string.IsNullOrWhiteSpace(snap?.World))
                    props[worldProp] = new { rich_text = new[] { new { text = new { content = snap.World } } } };
                if (!string.IsNullOrWhiteSpace(snap?.Character))
                    props[charProp] = new { rich_text = new[] { new { text = new { content = snap.Character } } } };
                if (!string.IsNullOrWhiteSpace(snap?.FreeCompany))
                    props[fcProp] = new { rich_text = new[] { new { text = new { content = snap.FreeCompany } } } };
            }
            catch { }

            return props;
        }

        private string BuildStableId(SubmarineSnapshot snap, SubmarineRecord it)
        {
            // NotionKeyMode に応じて安定キーを生成
            string baseStr;
            var slotStr = (it.Slot?.ToString() ?? "0");
            var nameStr = it.Name ?? string.Empty;
            var routeStr = it.RouteKey ?? string.Empty;
            var charStr = snap?.Character ?? string.Empty;
            var worldStr = snap?.World ?? string.Empty;

            switch (_cfg.NotionKeyMode)
            {
                case NotionKeyMode.PerSlot:
                    if (!string.IsNullOrWhiteSpace(charStr) && !string.IsNullOrWhiteSpace(worldStr))
                        baseStr = $"{charStr}|{worldStr}|{slotStr}";
                    else
                        baseStr = $"{nameStr}|{slotStr}"; // フォールバック
                    break;
                case NotionKeyMode.PerSlotRoute:
                    if (!string.IsNullOrWhiteSpace(charStr) && !string.IsNullOrWhiteSpace(worldStr))
                        baseStr = $"{charStr}|{worldStr}|{slotStr}|{routeStr}";
                    else
                        baseStr = $"{nameStr}|{slotStr}|{routeStr}"; // フォールバック
                    break;
                case NotionKeyMode.PerVoyage:
                default:
                    baseStr = $"{nameStr}|{(it.EtaUnix?.ToString() ?? "0")}|{slotStr}";
                    break;
            }
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(baseStr));
            var hex = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            return "xsr-" + hex;
        }

        // Notion DB のプロパティ検証（不足/型不一致を簡易チェック）
        public async Task<string> EnsureDatabasePropsAsync(CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_cfg.NotionToken) || string.IsNullOrWhiteSpace(_cfg.NotionDatabaseId))
                    return "Notion: Token/DatabaseId 未設定";

                var url = $"https://api.notion.com/v1/databases/{_cfg.NotionDatabaseId}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.NotionToken);
                req.Headers.Add("Notion-Version", NotionVersion);
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if ((int)resp.StatusCode == 429)
                {
                    // 単純リトライ
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                    using var req2 = new HttpRequestMessage(HttpMethod.Get, url);
                    req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.NotionToken);
                    req2.Headers.Add("Notion-Version", NotionVersion);
                    using var resp2 = await _http.SendAsync(req2, ct).ConfigureAwait(false);
                    if (!resp2.IsSuccessStatusCode)
                        return $"Notion: GET databases 失敗 {(int)resp2.StatusCode}";
                    body = await resp2.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                }
                else if (!resp.IsSuccessStatusCode)
                {
                    return $"Notion: GET databases 失敗 {(int)resp.StatusCode}";
                }

                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("properties", out var propsEl) || propsEl.ValueKind != JsonValueKind.Object)
                    return "Notion: properties が見つかりません";

                // 必須プロパティと想定型
                var want = new (string name, string type)[]
                {
                    (_cfg.NotionPropName ?? "Name", "title"),
                    (_cfg.NotionPropSlot ?? "Slot", "number"),
                    (_cfg.NotionPropEta ?? "ETA", "date"),
                    (_cfg.NotionPropRoute ?? "Route", "rich_text"),
                    (_cfg.NotionPropRank ?? "Rank", "number"),
                    (_cfg.NotionPropExtId ?? "ExtId", "rich_text"),
                    (_cfg.NotionPropRemaining ?? "Remaining", "rich_text"),
                    (_cfg.NotionPropWorld ?? "World", "rich_text"),
                    (_cfg.NotionPropCharacter ?? "Character", "rich_text"),
                    (_cfg.NotionPropFC ?? "FC", "rich_text"),
                };

                var missing = new List<string>();
                var mismatch = new List<string>();

                foreach (var (name, type) in want)
                {
                    if (!propsEl.TryGetProperty(name, out var pe))
                    {
                        missing.Add(name);
                        continue;
                    }
                    if (!pe.TryGetProperty("type", out var te))
                    {
                        mismatch.Add($"{name} (type 不明)");
                        continue;
                    }
                    var t = te.GetString() ?? string.Empty;
                    if (!string.Equals(t, type, StringComparison.OrdinalIgnoreCase))
                        mismatch.Add($"{name} (actual={t}, want={type})");
                }

                if (missing.Count == 0 && mismatch.Count == 0)
                    return "Notion: プロパティOK";
                var msg = new StringBuilder();
                if (missing.Count > 0) msg.Append($"Missing: {string.Join(", ", missing)}. ");
                if (mismatch.Count > 0) msg.Append($"Type mismatch: {string.Join(", ", mismatch)}.");
                return msg.ToString();
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "EnsureDatabaseProps failed");
                XsrDebug.Log(_cfg, "EnsureDatabaseProps failed", ex);
                return $"Notion: 検証で例外 {ex.Message}";
            }
        }
    }
}
