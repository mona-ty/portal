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
        Task<bool> EnsureProvisionedAsync(CancellationToken ct = default);
        Task<string> EnsureDatabasePropsAsync(CancellationToken ct = default);
    }

    internal sealed class NotionClient : INotionClient
    {
        private readonly Configuration _cfg;
        private readonly Dalamud.Plugin.Services.IPluginLog _log;
        private readonly HttpClient _http;
        private const string NotionVersion = "2022-06-28";
        private readonly Dalamud.Plugin.IDalamudPluginInterface? _pi;

        public NotionClient(Configuration cfg, Dalamud.Plugin.Services.IPluginLog log, HttpClient http, Dalamud.Plugin.IDalamudPluginInterface? pi = null)
        {
            _cfg = cfg; _log = log; _http = http; _pi = pi;
        }

        public async Task UpsertSnapshotAsync(SubmarineSnapshot snap, bool latestOnly, CancellationToken ct = default)
        {
            try
            {
                if (snap is null) return;
                if (!_cfg.NotionEnabled) { XsrDebug.Log(_cfg, "Notion: skip (disabled)"); return; }
                if (string.IsNullOrWhiteSpace(_cfg.NotionToken)) { XsrDebug.Log(_cfg, "Notion: skip (no token)"); return; }
                // Ensure DB is provisioned (auto-create if missing or properties incomplete)
                try { await EnsureProvisionedAsync(ct).ConfigureAwait(false); } catch { }
                if (string.IsNullOrWhiteSpace(_cfg.NotionDatabaseId)) { XsrDebug.Log(_cfg, "Notion: skip (no database id)"); return; }
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

        public async Task<bool> EnsureProvisionedAsync(CancellationToken ct = default)
        {
            try
            {
                if (!_cfg.NotionEnabled) return false;
                if (string.IsNullOrWhiteSpace(_cfg.NotionToken)) return false;

                // If DB is set, verify it exists. If gone (404), clear and re-create.
                if (!string.IsNullOrWhiteSpace(_cfg.NotionDatabaseId))
                {
                    var exists = await CheckDatabaseExistsAsync(_cfg.NotionDatabaseId!, ct).ConfigureAwait(false);
                    if (exists)
                    {
                        try { await EnsureDatabasePropsAndFixAsync(ct).ConfigureAwait(false); } catch { }
                        return true;
                    }
                    else
                    {
                        XsrDebug.Log(_cfg, $"Notion: database id invalid or deleted: {_cfg.NotionDatabaseId}");
                        _cfg.NotionDatabaseId = string.Empty; TrySaveConfig();
                    }
                }

                // Determine parent page: prefer configured parent; else create a workspace page
                string? pageId = null;
                if (!string.IsNullOrWhiteSpace(_cfg.NotionParentPageId))
                {
                    pageId = _cfg.NotionParentPageId;
                }
                else
                {
                    pageId = await CreateWorkspacePageAsync("XSR Notifications", ct).ConfigureAwait(false);
                }
                if (string.IsNullOrWhiteSpace(pageId)) { XsrDebug.Log(_cfg, "Notion: page create failed"); return false; }
                var dbId = await CreateDatabaseAsync(pageId!, "XSR Submarines", ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(dbId)) { XsrDebug.Log(_cfg, "Notion: database create failed"); return false; }
                _cfg.NotionDatabaseId = dbId!;
                TrySaveConfig();
                XsrDebug.Log(_cfg, $"Notion: database provisioned id={dbId}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion EnsureProvisioned failed");
                XsrDebug.Log(_cfg, "Notion EnsureProvisioned failed", ex);
                return false;
            }
        }

        private async Task<bool> CheckDatabaseExistsAsync(string databaseId, CancellationToken ct)
        {
            try
            {
                var url = $"https://api.notion.com/v1/databases/{databaseId}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.NotionToken);
                req.Headers.Add("Notion-Version", NotionVersion);
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode) return true;
                if ((int)resp.StatusCode == 404) return false;
                // For other errors, assume not usable
                return false;
            }
            catch { return false; }
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
                    if ((int)resp.StatusCode == 404)
                    {
                        // Database might have been deleted; attempt re-provision once.
                        XsrDebug.Log(_cfg, "Notion query 404: database missing; attempting re-provision");
                        _cfg.NotionDatabaseId = string.Empty; TrySaveConfig();
                        var ok = await EnsureProvisionedAsync(ct).ConfigureAwait(false);
                        if (ok && !string.IsNullOrWhiteSpace(_cfg.NotionDatabaseId))
                        {
                            // retry once with new DB
                            return await TryFindPageIdByExtIdAsync(_cfg.NotionDatabaseId!, extId, ct).ConfigureAwait(false);
                        }
                    }
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
            try
            {
                await SendAsync(url, HttpMethod.Post, payload, ct).ConfigureAwait(false);
            }
            catch
            {
                // If failed due to invalid DB (404), try re-provision once
                try
                {
                    if (!string.IsNullOrWhiteSpace(_cfg.NotionDatabaseId))
                    {
                        var ok = await CheckDatabaseExistsAsync(_cfg.NotionDatabaseId!, ct).ConfigureAwait(false);
                        if (!ok)
                        {
                            _cfg.NotionDatabaseId = string.Empty; TrySaveConfig();
                            var prov = await EnsureProvisionedAsync(ct).ConfigureAwait(false);
                            if (prov && !string.IsNullOrWhiteSpace(_cfg.NotionDatabaseId))
                            {
                                var payload2 = new { parent = new { database_id = _cfg.NotionDatabaseId }, properties = BuildProperties(snap, it, extId) };
                                await SendAsync(url, HttpMethod.Post, payload2, ct).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch { }
            }
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

        private async Task<string?> CreateWorkspacePageAsync(string title, CancellationToken ct)
        {
            try
            {
                var url = "https://api.notion.com/v1/pages";
                var payload = new
                {
                    parent = new { workspace = true },
                    properties = new
                    {
                        title = new
                        {
                            title = new[] { new { text = new { content = title } } }
                        }
                    }
                };
                var json = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.NotionToken);
                req.Headers.Add("Notion-Version", NotionVersion);
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    if (_cfg.DebugLogging) _log.Warning($"Notion page create failed: {(int)resp.StatusCode} {resp.ReasonPhrase} body={body}");
                    else _log.Warning($"Notion page create failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    return null;
                }
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion page create exception");
                XsrDebug.Log(_cfg, "Notion page create exception", ex);
                return null;
            }
        }

        private async Task<string?> CreateDatabaseAsync(string parentPageId, string title, CancellationToken ct)
        {
            try
            {
                string nameProp = _cfg.NotionPropName ?? "Name";
                string slotProp = _cfg.NotionPropSlot ?? "Slot";
                string etaProp = _cfg.NotionPropEta ?? "ETA";
                string routeProp = _cfg.NotionPropRoute ?? "Route";
                string rankProp = _cfg.NotionPropRank ?? "Rank";
                string extProp = _cfg.NotionPropExtId ?? "ExtId";
                var url = "https://api.notion.com/v1/databases";
                var payload = new
                {
                    parent = new { type = "page_id", page_id = parentPageId },
                    title = new[] { new { type = "text", text = new { content = title } } },
                    properties = new System.Collections.Generic.Dictionary<string, object>
                    {
                        [nameProp] = new { title = new { } },
                        [slotProp] = new { number = new { } },
                        [etaProp] = new { date = new { } },
                        [routeProp] = new { rich_text = new { } },
                        [rankProp] = new { number = new { } },
                        [extProp] = new { rich_text = new { } },
                    }
                };
                var json = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.NotionToken);
                req.Headers.Add("Notion-Version", NotionVersion);
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    if (_cfg.DebugLogging) _log.Warning($"Notion db create failed: {(int)resp.StatusCode} {resp.ReasonPhrase} body={body}");
                    else _log.Warning($"Notion db create failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    return null;
                }
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion db create exception");
                XsrDebug.Log(_cfg, "Notion db create exception", ex);
                return null;
            }
        }

        private async Task EnsureDatabasePropsAndFixAsync(CancellationToken ct)
        {
            try
            {
                var check = await EnsureDatabasePropsAsync(ct).ConfigureAwait(false);
                if (check.StartsWith("Notion: プロパティOK", StringComparison.Ordinal)) return;

                // Parse missing from message and try to add
                var toAdd = new List<(string name, string type)>();
                if (check.Contains("Missing:"))
                {
                    try
                    {
                        var mPart = check.Split("Missing:")[1];
                        var lst = mPart.Split('.')?[0] ?? string.Empty;
                        foreach (var raw in lst.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var name = raw.Trim();
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            // Map expected types by configured property names
                            string type = GuessPropType(name);
                            if (!string.IsNullOrEmpty(type)) toAdd.Add((name, type));
                        }
                    }
                    catch { }
                }
                if (toAdd.Count == 0) return;

                var url = $"https://api.notion.com/v1/databases/{_cfg.NotionDatabaseId}";
                var props = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var (name, type) in toAdd)
                {
                    props[name] = type switch
                    {
                        "title" => new { title = new { } },
                        "number" => new { number = new { } },
                        "date" => new { date = new { } },
                        _ => new { rich_text = new { } },
                    };
                }
                var payload = new { properties = props };
                await SendAsync(url, HttpMethod.Patch, payload, ct).ConfigureAwait(false);
                XsrDebug.Log(_cfg, $"Notion: added missing props [{string.Join(",", toAdd.Select(x => x.name))}]");
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion fix props failed");
                XsrDebug.Log(_cfg, "Notion fix props failed", ex);
            }
        }

        private string GuessPropType(string name)
        {
            try
            {
                if (string.Equals(name, _cfg.NotionPropName ?? "Name", StringComparison.Ordinal)) return "title";
                if (string.Equals(name, _cfg.NotionPropSlot ?? "Slot", StringComparison.Ordinal)) return "number";
                if (string.Equals(name, _cfg.NotionPropEta ?? "ETA", StringComparison.Ordinal)) return "date";
                if (string.Equals(name, _cfg.NotionPropRank ?? "Rank", StringComparison.Ordinal)) return "number";
                // others default to rich_text
                return "rich_text";
            }
            catch { return "rich_text"; }
        }

        private void TrySaveConfig()
        {
            try { _pi?.SavePluginConfig(_cfg); } catch { }
        }

        public static string? TryExtractIdFromUrlOrId(string? input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input)) return null;
                var s = input.Trim();
                // Prefer extracting ID from the path part (exclude query like ?v=<viewId>)
                try { var q = s.IndexOf('?'); if (q >= 0) s = s.Substring(0, q); } catch { }
                // If it's already a 36-char dashed UUID, normalize by stripping and re-dashing
                string OnlyHex(string t)
                {
                    var sb = new StringBuilder(t.Length);
                    foreach (var ch in t)
                    {
                        if ((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F')) sb.Append(char.ToLowerInvariant(ch));
                    }
                    return sb.ToString();
                }

                string Redash32(string hex)
                {
                    if (hex.Length != 32) return hex;
                    return string.Create(36, hex, (span, state) =>
                    {
                        // 8-4-4-4-12
                        var i = 0;
                        span[8] = '-'; span[13] = '-'; span[18] = '-'; span[23] = '-';
                        for (int si = 0, di = 0; si < 32; si++, di++)
                        {
                            if (di == 8 || di == 13 || di == 18 || di == 23) di++;
                            span[di] = state[si];
                        }
                    });
                }

                // Scan from tail (path part) for 32 hex characters (database/page id lives here)
                {
                    var hexSb = new StringBuilder(32);
                    for (int i = s.Length - 1; i >= 0 && hexSb.Length < 32; i--)
                    {
                        char ch = s[i];
                        if ((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F'))
                            hexSb.Append(char.ToLowerInvariant(ch));
                    }
                    if (hexSb.Length == 32)
                    {
                        var hex = new string(hexSb.ToString().Reverse().ToArray());
                        return Redash32(hex);
                    }
                }

                // If input might be a dashed UUID but not at tail, strip and re-dash
                var only = OnlyHex(s);
                if (only.Length == 32) return Redash32(only);

                // Fallback: search entire original input (may capture view id, so pick the first 32-hex occurrence)
                {
                    var src = input.Trim();
                    int count = 0; int lastIdx = -1;
                    for (int i = 0; i <= src.Length - 32; i++)
                    {
                        bool ok = true;
                        for (int j = 0; j < 32; j++)
                        {
                            char ch = src[i + j];
                            if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F'))) { ok = false; break; }
                        }
                        if (ok) { lastIdx = i; break; }
                    }
                    if (lastIdx >= 0)
                    {
                        var hex = src.Substring(lastIdx, 32);
                        return Redash32(OnlyHex(hex));
                    }
                }
                return null;
            }
            catch { return null; }
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
            // Optional fields (Remaining/World/Character/FC) are removed for simplicity

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

            // No extra snapshot identity fields are sent

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
