using System;
using System.Collections.Generic;

namespace XIVSubmarinesReturn.Services
{
    internal static class EtaFormatter
    {
        public static void Enrich(SubmarineSnapshot snapshot)
        {
            if (snapshot == null) return;
            try
            {
                var now = DateTimeOffset.UtcNow;
                snapshot.CapturedAt = now;
                if (snapshot.Items == null) return;

                foreach (var it in snapshot.Items)
                {
                    if (it == null) continue;
                    it.IsDefaultName = IsDefault(it.Name);
                    if (it.Extra == null) it.Extra = new Dictionary<string, string>(StringComparer.Ordinal);

                    // ETA
                    if (!it.EtaUnix.HasValue)
                    {
                        if (it.DurationMinutes.HasValue)
                        {
                            var eta = now.AddMinutes(Math.Max(0, it.DurationMinutes.Value));
                            it.EtaUnix = eta.ToUnixTimeSeconds();
                        }
                    }

                    // Human friendly fields
                    if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                    {
                        var eta = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value).ToLocalTime();
                        var remain = eta - DateTimeOffset.Now;
                        var mins = Math.Max(0, (int)Math.Round(remain.TotalMinutes));
                        it.Extra["EtaLocal"] = eta.ToString("HH:mm");
                        // Discord等で日付まで欲しいケース向けに完全表記も付与
                        it.Extra["EtaLocalFull"] = eta.ToString("yyyy/M/d HH:mm");
                        it.Extra["RemainingText"] = mins < 60 ? $"{mins}\u5206" : $"{mins / 60}\u6642\u9593{mins % 60}\u5206";
                        // (removed duplicate)
                        // duplicate removed
                    }

                    // Route short（既に他処理で設定済みなら上書きしない）
                    if (!string.IsNullOrWhiteSpace(it.RouteKey))
                    {
                        if (!it.Extra.TryGetValue("RouteShort", out var existing) || string.IsNullOrWhiteSpace(existing))
                            it.Extra["RouteShort"] = ShortRoute(it.RouteKey);
                    }
                }
            }
            catch { }
        }

        private static bool IsDefault(string? name)
        {
            try { return System.Text.RegularExpressions.Regex.IsMatch(name ?? string.Empty, @"^Submarine-\d+$"); }
            catch { return false; }
        }

        private static string ShortRoute(string? rk)
        {
            if (string.IsNullOrWhiteSpace(rk)) return string.Empty;
            try
            {
                var s = System.Text.RegularExpressions.Regex.Replace(rk, @"Point-(\d+)", "P$1");
                s = s.Replace(" - ", ">");
                s = System.Text.RegularExpressions.Regex.Replace(s, @"(?<=^P\d+>)P", "");
                return s;
            }
            catch { return rk ?? string.Empty; }
        }
    }
}
