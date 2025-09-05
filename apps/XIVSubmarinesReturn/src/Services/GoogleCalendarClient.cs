using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XIVSubmarinesReturn.Services
{
    public interface IGoogleCalendarClient
    {
        Task<bool> EnsureAuthorizedAsync(CancellationToken ct = default);
        Task UpsertEventsAsync(IEnumerable<SubmarineRecord> items, CalendarMode mode, CancellationToken ct = default);
    }

    public sealed class GoogleCalendarClient : IGoogleCalendarClient
    {
        private readonly Configuration _cfg;
        private readonly Dalamud.Plugin.Services.IPluginLog _log;
        private readonly HttpClient _http;

        private string? _accessToken;
        private DateTimeOffset _accessTokenExp;

        public GoogleCalendarClient(Configuration cfg, Dalamud.Plugin.Services.IPluginLog log, HttpClient http)
        {
            _cfg = cfg;
            _log = log;
            _http = http;
        }

        public async Task<bool> EnsureAuthorizedAsync(CancellationToken ct = default)
        {
            if (!_cfg.GoogleEnabled) return false;
            if (string.IsNullOrWhiteSpace(_cfg.GoogleRefreshToken)) return false;
            if (string.IsNullOrWhiteSpace(_cfg.GoogleClientId) || string.IsNullOrWhiteSpace(_cfg.GoogleClientSecret)) return false;
            // refresh if missing/expired
            if (string.IsNullOrEmpty(_accessToken) || DateTimeOffset.UtcNow >= _accessTokenExp)
            {
                var ok = await RefreshAccessTokenAsync(ct).ConfigureAwait(false);
                if (!ok) return false;
            }
            return true;
        }

        public Task UpsertEventsAsync(IEnumerable<SubmarineRecord> items, CalendarMode mode, CancellationToken ct = default)
        {
            return UpsertEventsCoreAsync(items, mode, ct);
        }

        private async Task UpsertEventsCoreAsync(IEnumerable<SubmarineRecord> items, CalendarMode mode, CancellationToken ct)
        {
            try
            {
                if (!_cfg.GoogleEnabled) return;
                if (!await EnsureAuthorizedAsync(ct).ConfigureAwait(false)) return;

                var list = new List<SubmarineRecord>(items ?? Array.Empty<SubmarineRecord>());
                if (list.Count == 0) return;
                if (mode == CalendarMode.Latest)
                {
                    list.Sort((a, b) => Nullable.Compare(a.EtaUnix, b.EtaUnix));
                    list = new List<SubmarineRecord> { list[0] };
                }

                foreach (var it in list)
                {
                    if (!it.EtaUnix.HasValue) continue;
                    await UpsertOneAsync(it, ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "GCal upsert failed");
                XsrDebug.Log(_cfg, "GCal upsert failed", ex);
            }
        }

        private async Task<bool> RefreshAccessTokenAsync(CancellationToken ct)
        {
            try
            {
                var url = "https://oauth2.googleapis.com/token";
                var kv = new List<KeyValuePair<string, string>>
                {
                    new("client_id", _cfg.GoogleClientId ?? string.Empty),
                    new("client_secret", _cfg.GoogleClientSecret ?? string.Empty),
                    new("refresh_token", _cfg.GoogleRefreshToken ?? string.Empty),
                    new("grant_type", "refresh_token"),
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(kv)
                };
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _log.Warning($"GCal token refresh failed: {(int)resp.StatusCode} {resp.ReasonPhrase} body={body}");
                    return false;
                }
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                _accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
                var expSec = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
                _accessTokenExp = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expSec - 60));
                return !string.IsNullOrEmpty(_accessToken);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "GCal refresh exception");
                XsrDebug.Log(_cfg, "GCal refresh exception", ex);
                return false;
            }
        }

        private async Task UpsertOneAsync(SubmarineRecord it, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                var ok = await RefreshAccessTokenAsync(ct).ConfigureAwait(false);
                if (!ok) return;
            }
            var calId = string.IsNullOrWhiteSpace(_cfg.GoogleCalendarId) ? "primary" : _cfg.GoogleCalendarId!;
            var tz = "Asia/Tokyo";
            var etaUtc = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix!.Value);
            var startTok = etaUtc.ToOffset(TimeSpan.FromHours(9));
            var endTok = startTok.AddMinutes(5);

            var eventId = BuildStableId(it);
            var payload = new
            {
                id = eventId,
                summary = $"Sub: {(it.Slot.HasValue ? $"S{it.Slot.Value} " : string.Empty)}{it.Name}",
                description = BuildDescription(it),
                start = new { dateTime = startTok.ToString("yyyy-MM-dd'T'HH:mm:sszzz"), timeZone = tz },
                end = new { dateTime = endTok.ToString("yyyy-MM-dd'T'HH:mm:sszzz"), timeZone = tz },
                reminders = BuildReminders(),
            };

            // Try update first
            var updateUrl = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calId)}/events/{Uri.EscapeDataString(eventId)}";
            var okUpdate = await SendEventAsync(updateUrl, HttpMethod.Put, payload, ct, allowRetryOn401: true).ConfigureAwait(false);
            if (okUpdate) return;

            // If update fails (404), try insert
            var insertUrl = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calId)}/events";
            await SendEventAsync(insertUrl, HttpMethod.Post, payload, ct, allowRetryOn401: true, treat409AsSuccess: true).ConfigureAwait(false);
        }

        private async Task<bool> SendEventAsync(string url, HttpMethod method, object payload, CancellationToken ct, bool allowRetryOn401, bool treat409AsSuccess = false)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(method, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized && allowRetryOn401)
                {
                    if (await RefreshAccessTokenAsync(ct).ConfigureAwait(false))
                    {
                        using var req2 = new HttpRequestMessage(method, url)
                        {
                            Content = new StringContent(json, Encoding.UTF8, "application/json")
                        };
                        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                        using var resp2 = await _http.SendAsync(req2, ct).ConfigureAwait(false);
                        if (resp2.IsSuccessStatusCode) return true;
                        if (treat409AsSuccess && (int)resp2.StatusCode == 409) return true;
                        return false;
                    }
                    return false;
                }
                if (resp.IsSuccessStatusCode) return true;
                if (treat409AsSuccess && (int)resp.StatusCode == 409) return true;
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.Warning($"GCal event call failed: {(int)resp.StatusCode} {resp.ReasonPhrase} body={body}");
                return false;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "GCal SendEventAsync exception");
                XsrDebug.Log(_cfg, "GCal SendEventAsync exception", ex);
                return false;
            }
        }

        private static string BuildDescription(SubmarineRecord it)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(it.Name)) sb.AppendLine($"Name: {it.Name}");
            if (it.Slot.HasValue) sb.AppendLine($"Slot: {it.Slot}");
            if (it.Rank.HasValue) sb.AppendLine($"Rank: {it.Rank}");
            if (!string.IsNullOrWhiteSpace(it.RouteKey)) sb.AppendLine($"Route: {it.RouteKey}");
            return sb.ToString();
        }

        private object BuildReminders()
        {
            try
            {
                var list = new List<object>();
                foreach (var m in (_cfg.GoogleReminderMinutes ?? new List<int>()))
                {
                    if (m >= 0 && m <= 40320) list.Add(new { method = "popup", minutes = m });
                }
                return new { useDefault = false, overrides = list.ToArray() };
            }
            catch { return new { useDefault = true }; }
        }

        private string BuildStableId(SubmarineRecord it)
        {
            // Stable id uses World|Character|Name|EtaUnix (World/Characterは未保有でも空文字でOK)
            var baseStr = $"{(it.Name ?? string.Empty)}|{it.EtaUnix}|{it.Slot}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(baseStr));
            var hex = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            return "xsr-" + hex;
        }
    }
}

