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
                if (!_cfg.NotionEnabled) return;
                if (string.IsNullOrWhiteSpace(_cfg.NotionToken) || string.IsNullOrWhiteSpace(_cfg.NotionDatabaseId)) return;
                var items = (snap?.Items ?? new List<SubmarineRecord>()).ToList();
                if (items.Count == 0) return;
                if (latestOnly)
                {
                    var nearest = items.Where(x => x.EtaUnix.HasValue).OrderBy(x => x.EtaUnix!.Value).FirstOrDefault() ?? items[0];
                    items = new List<SubmarineRecord> { nearest };
                }
                foreach (var it in items)
                {
                    await UpsertOneAsync(it, ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion upsert failed");
                XsrDebug.Log(_cfg, "Notion upsert failed", ex);
            }
        }

        private async Task UpsertOneAsync(SubmarineRecord it, CancellationToken ct)
        {
            try
            {
                var token = _cfg.NotionToken;
                var dbid = _cfg.NotionDatabaseId;
                var extId = BuildStableId(it);

                var pageId = await TryFindPageIdByExtIdAsync(dbid, extId, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(pageId))
                {
                    await UpdatePageAsync(pageId!, it, extId, ct).ConfigureAwait(false);
                }
                else
                {
                    await CreatePageAsync(dbid, it, extId, ct).ConfigureAwait(false);
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
                    _log.Warning($"Notion query failed: {(int)resp.StatusCode} {resp.ReasonPhrase} body={body}");
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

        private async Task CreatePageAsync(string databaseId, SubmarineRecord it, string extId, CancellationToken ct)
        {
            var url = "https://api.notion.com/v1/pages";
            var payload = new
            {
                parent = new { database_id = databaseId },
                properties = BuildProperties(it, extId)
            };
            await SendAsync(url, HttpMethod.Post, payload, ct).ConfigureAwait(false);
        }

        private async Task UpdatePageAsync(string pageId, SubmarineRecord it, string extId, CancellationToken ct)
        {
            var url = $"https://api.notion.com/v1/pages/{pageId}";
            var payload = new { properties = BuildProperties(it, extId) };
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
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    _log.Warning($"Notion send failed: {(int)resp.StatusCode} {resp.ReasonPhrase} body={body}");
                    XsrDebug.Log(_cfg, $"Notion send failed: {(int)resp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Notion send exception");
                XsrDebug.Log(_cfg, "Notion send exception", ex);
            }
        }

        private object BuildProperties(SubmarineRecord it, string extId)
        {
            // Build Notion properties using configured names
            var props = new Dictionary<string, object>(StringComparer.Ordinal);
            string nameProp = _cfg.NotionPropName ?? "Name";
            string slotProp = _cfg.NotionPropSlot ?? "Slot";
            string etaProp = _cfg.NotionPropEta ?? "ETA";
            string routeProp = _cfg.NotionPropRoute ?? "Route";
            string rankProp = _cfg.NotionPropRank ?? "Rank";
            string extProp = _cfg.NotionPropExtId ?? "ExtId";

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
                var tok = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value).ToOffset(TimeSpan.FromHours(9));
                props[etaProp] = new { date = new { start = tok.ToString("yyyy-MM-dd'T'HH:mm:sszzz") } };
            }

            // external id for upsert
            props[extProp] = new { rich_text = new[] { new { text = new { content = extId } } } };

            return props;
        }

        private static string BuildStableId(SubmarineRecord it)
        {
            var baseStr = $"{(it.Name ?? string.Empty)}|{it.EtaUnix}|{it.Slot}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(baseStr));
            var hex = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            return "xsr-" + hex;
        }
    }
}

