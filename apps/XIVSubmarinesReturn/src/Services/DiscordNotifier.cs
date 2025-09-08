using System;
using System.Net.Http;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XIVSubmarinesReturn.Services
{
    public interface IDiscordNotifier
    {
        Task NotifySnapshotAsync(SubmarineSnapshot snap, bool latestOnly, CancellationToken ct = default);
        Task NotifyAlarmAsync(SubmarineRecord rec, int leadMinutes, CancellationToken ct = default);
    }

    public sealed class DiscordNotifier : IDiscordNotifier
    {
        private readonly Configuration _cfg;
        private readonly Dalamud.Plugin.Services.IPluginLog _log;
        private readonly HttpClient _http;

        public DiscordNotifier(Configuration cfg, Dalamud.Plugin.Services.IPluginLog log, HttpClient http)
        {
            _cfg = cfg;
            _log = log;
            _http = http;
        }

        public async Task NotifySnapshotAsync(SubmarineSnapshot snap, bool latestOnly, CancellationToken ct = default)
        {
            try
            {
                if (!_cfg.DiscordEnabled) return;
                var url = _cfg.DiscordWebhookUrl;
                if (string.IsNullOrWhiteSpace(url)) return;
                if (snap?.Items == null || snap.Items.Count == 0) return;

                var items = latestOnly
                    ? new[] { GetNearest(snap) ?? snap.Items[0] }
                    : snap.Items.ToArray();

                if (_cfg.DiscordUseEmbeds)
                {
                    var fields = items.Take(25).Select(it => new
                    {
                        name = (it.Name ?? string.Empty),
                        value = BuildSnapshotLine2(it),
                        inline = false
                    }).ToArray();
                    var payload = new
                    {
                        embeds = new[]
                        {
                            new { title = "Submarines", description = latestOnly ? "Earliest only (ETA min)" : "All", color = 0x0066CC, fields }
                        }
                    };
                    await PostJsonAsync(url, payload, ct).ConfigureAwait(false);
                }
                else
                {
                    foreach (var it in items)
                    {
                        var msg = BuildSnapshotLine2(it);
                        await PostAsync(url, msg, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Discord NotifySnapshot failed");
                XsrDebug.Log(_cfg, "Discord NotifySnapshot failed", ex);
            }
        }

                public async Task NotifyAlarmAsync(SubmarineRecord rec, int leadMinutes, CancellationToken ct = default)
        {
            try
            {
                if (!_cfg.DiscordEnabled) return;
                var url = _cfg.DiscordWebhookUrl;
                if (string.IsNullOrWhiteSpace(url)) return;
                if (rec == null) return;

                string etaFull;
                try
                {
                    if (rec.Extra != null && rec.Extra.TryGetValue("EtaLocalFull", out var tfull) && !string.IsNullOrWhiteSpace(tfull)) etaFull = tfull;
                    else if (rec.EtaUnix.HasValue && rec.EtaUnix.Value > 0) etaFull = DateTimeOffset.FromUnixTimeSeconds(rec.EtaUnix.Value).ToLocalTime().ToString("yyyy/M/d HH:mm");
                    else etaFull = (rec.Extra != null && rec.Extra.TryGetValue("EtaLocal", out var tshort)) ? (tshort ?? "?") : "?";
                }
                catch { etaFull = "?"; }

                var routeText = (rec.Extra != null && rec.Extra.TryGetValue("RouteShort", out var r)) ? r : rec.RouteKey;
                var msg = $"[Sub Alarm] {rec.Name} ETA {etaFull} (残 {leadMinutes}分) {routeText}";
                await PostAsync(url, msg, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Discord NotifyAlarm failed");
                XsrDebug.Log(_cfg, "Discord NotifyAlarm failed", ex);
            }
        }private static SubmarineRecord? GetNearest(SubmarineSnapshot snap)
        {
            try
            {
                return snap.Items
                    .Where(x => x.EtaUnix.HasValue)
                    .OrderBy(x => x.EtaUnix!.Value)
                    .FirstOrDefault() ?? snap.Items.FirstOrDefault();
            }
            catch { return snap.Items.FirstOrDefault(); }
        }

        // Extended line builder that prefers full local date-time for ETA
                        // Extended line builder that prefers full local date-time for ETA
        private static string BuildSnapshotLine2(SubmarineRecord it)
        {
            string eta = string.Empty;
            try
            {
                if (it.Extra != null && it.Extra.TryGetValue("EtaLocalFull", out var tFull) && !string.IsNullOrWhiteSpace(tFull))
                    eta = tFull;
                else if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                    eta = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value).ToLocalTime().ToString("yyyy/M/d HH:mm");
                else if (it.Extra != null && it.Extra.TryGetValue("EtaLocal", out var tShort))
                    eta = tShort ?? string.Empty;
            }
            catch { }

            var rem = it.Extra != null && it.Extra.TryGetValue("RemainingText", out var rm) ? rm : string.Empty;
            var rt = it.Extra != null && it.Extra.TryGetValue("RouteShort", out var r) ? r : it.RouteKey;
            return  $"[Sub] {eta} (残 {rem}) {rt}".Trim(); 
        }private static string BuildSnapshotLine(SubmarineRecord it)
        {
            var eta = it.Extra != null && it.Extra.TryGetValue("EtaLocal", out var t) ? t : string.Empty;
            var rem = it.Extra != null && it.Extra.TryGetValue("RemainingText", out var rm) ? rm : string.Empty;
            var rt = it.Extra != null && it.Extra.TryGetValue("RouteShort", out var r) ? r : it.RouteKey;
            var slot = it.Slot.HasValue ? $"S{it.Slot.Value} " : string.Empty;
            return $"[Sub] {slot}{it.Name} {eta} (残 {rem}) {rt}".Trim();
        }

        private async Task PostAsync(string url, string content, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content)) return;
                content = SanitizeJp(content);
                var body = new StringContent(JsonSerializer.Serialize(new { content }), Encoding.UTF8, "application/json");
                using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = body };
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                if ((int)resp.StatusCode == 429)
                {
                    // rate limit handling with clamp + small jitter
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
                    var rnd = new System.Random();
                    retry = Math.Min(30, Math.Max(1, retry + rnd.Next(0, 3)));
                    await Task.Delay(TimeSpan.FromSeconds(retry), ct).ConfigureAwait(false);
                    using var req2 = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(JsonSerializer.Serialize(new { content }), Encoding.UTF8, "application/json") };
                    using var resp2 = await _http.SendAsync(req2, ct).ConfigureAwait(false);
                    resp2.EnsureSuccessStatusCode();
                }
                else resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Discord webhook post failed");
                XsrDebug.Log(_cfg, "Discord webhook post failed", ex);
            }
        }

        private async Task PostJsonAsync(string url, object payload, CancellationToken ct)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                json = SanitizeJp(json);
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
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
                    var rnd = new System.Random();
                    retry = Math.Min(30, Math.Max(1, retry + rnd.Next(0, 3)));
                    await Task.Delay(TimeSpan.FromSeconds(retry), ct).ConfigureAwait(false);
                    using var req2 = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    using var resp2 = await _http.SendAsync(req2, ct).ConfigureAwait(false);
                    resp2.EnsureSuccessStatusCode();
                }
                else resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Discord webhook post (json) failed");
                XsrDebug.Log(_cfg, "Discord webhook post (json) failed", ex);
            }
        }

        private static string SanitizeJp(string s)
        {
            try
            {
                if (string.IsNullOrEmpty(s)) return s;
                return s.Replace("�c", "残").Replace("��", "分");
            }
            catch { return s; }
        }
    }
}


