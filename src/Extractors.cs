using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace XIVSubmarinesReturn
{
    internal static class Extractors
    {
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // 全角スペース→半角、全角コロン→半角。制御文字除去。
            var t = s.Replace('\u3000', ' ')
                     .Replace('：', ':')
                     .Trim();
            t = new string(t.Where(ch => !char.IsControl(ch)).ToArray());
            return t;
        }

        private static int? TryParseDuration(string s)
        {
            var t = Normalize(s);
            // JP: 残り時間 16時間57分 / 16時間 / 57分（分のみはキーワード必須）
            var mJp = Regex.Match(t, @"(?:探索中|残り時間?|所要時間?)[: :]*\s*(?:(?<h>\d+)\s*時間)?\s*(?:(?<m>\d+)\s*分)?",
                RegexOptions.CultureInvariant);
            if (mJp.Success)
            {
                int h = mJp.Groups["h"].Success ? int.Parse(mJp.Groups["h"].Value) : 0;
                int m = mJp.Groups["m"].Success ? int.Parse(mJp.Groups["m"].Value) : 0;
                if (h > 0 || m > 0) return h * 60 + m;
            }
            // EN: Remaining Time 3h 15m / 45m（分のみはキーワード必須）
            var mEn = Regex.Match(t, @"(?i)(?:remaining\s*time|time\s*left|eta)[: ]*\s*(?:(?<h>\d+)\s*h)?\s*(?:(?<m>\d+)\s*m)?");
            if (mEn.Success)
            {
                int h = mEn.Groups["h"].Success ? int.Parse(mEn.Groups["h"].Value) : 0;
                int m = mEn.Groups["m"].Success ? int.Parse(mEn.Groups["m"].Value) : 0;
                if (h > 0 || m > 0) return h * 60 + m;
            }
            // JP fallback: 「xx時間yy分」単独
            var mJp2 = Regex.Match(t, @"(?:(?<h>\d+)\s*時間)\s*(?:(?<m>\d+)\s*分)?");
            if (mJp2.Success)
            {
                int h = mJp2.Groups["h"].Success ? int.Parse(mJp2.Groups["h"].Value) : 0;
                int m = mJp2.Groups["m"].Success ? int.Parse(mJp2.Groups["m"].Value) : 0;
                if (h > 0 || m > 0) return h * 60 + m;
            }
            // HH:MM 形式
            var hm = Regex.Match(t, @"\b(?<h>\d{1,2}):(?<m>\d{2})\b");
            if (hm.Success)
            {
                int h = int.Parse(hm.Groups["h"].Value);
                int m = int.Parse(hm.Groups["m"].Value);
                if (h >= 0 && h <= 48 && m >= 0 && m < 60) return h * 60 + m;
            }
            return null;
        }

        public static bool ExtractFromLines(List<string> lines, SubmarineSnapshot snapshot)
        {
            return ExtractFromLines(lines, snapshot, out _);
        }

        public static bool ExtractFromLines(List<string> lines, SubmarineSnapshot snapshot, out List<string> trace)
        {
            trace = new List<string>();
            var byName = snapshot.Items.ToDictionary(x => x.Name, x => x, StringComparer.Ordinal);
            string? current = null;

            foreach (var ln in lines)
            {
                var s = Normalize(ln ?? string.Empty);
                if (string.IsNullOrEmpty(s)) continue;

                // “Submarine-<n>” は艦名ではなくスロット名。採用せずスキップ。
                if (Regex.IsMatch(s, @"^Submarine-\d+\b", RegexOptions.CultureInvariant))
                {
                    current = null;
                    continue;
                }

                // 任意艦名（キーワードが含まれない先頭行を名前候補とする）
                if (!Regex.IsMatch(s, @"(残り時間|探索中|所要時間|rank|ランク|航路|探索地|探索先|目的地|sector|route|destination)", RegexOptions.IgnoreCase))
                {
                    // 明らかなノイズ行は除外
                    if (Regex.IsMatch(s, @"^(やめる|キャンセル|決定|戻る)\b"))
                        continue;
                    if (s.StartsWith("潜水艦を選択してください"))
                        continue;
                    if (s.StartsWith("探索機体数") || s.StartsWith("保有燃料数"))
                        continue;
                    var mGeneric = Regex.Match(s, @"^(?<name>[^:：\[\(（]{2,32})");
                    if (mGeneric.Success)
                    {
                        current = mGeneric.Groups["name"].Value.Trim();
                        if (!byName.TryGetValue(current, out var rec))
                        {
                            rec = new SubmarineRecord { Name = current };
                            byName[current] = rec;
                            snapshot.Items.Add(rec);
                        }
                        trace.Add($"name* <- '{s}' => {current}");
                        continue;
                    }
                }

                var mRank = Regex.Match(s, @"(?:(?i)rank|ランク)\s*(?<r>\d+)");
                if (mRank.Success)
                {
                    if (!string.IsNullOrEmpty(current) && byName.TryGetValue(current, out var rec))
                    {
                        rec.Rank = int.Parse(mRank.Groups["r"].Value);
                        trace.Add($"rank <- '{s}' => {rec.Name}:{rec.Rank}");
                    }
                    continue;
                }

                var minutes = TryParseDuration(s);
                if (minutes != null)
                {
                    if (!string.IsNullOrEmpty(current) && byName.TryGetValue(current, out var rec))
                    {
                        rec.DurationMinutes = minutes;
                        trace.Add($"time <- '{s}' => {rec.Name}:{minutes}m");
                    }
                    continue;
                }

                var mRouteJp = Regex.Match(s, @"(?:航路|探索地|探索先|探索場所|目的地|探索地点|行き先|行先)[::\s]+(?<route>.+)");
                if (mRouteJp.Success)
                {
                    var val = mRouteJp.Groups["route"].Value.Trim();
                    if (!string.IsNullOrEmpty(current) && byName.TryGetValue(current, out var rec) && string.IsNullOrEmpty(rec.RouteKey))
                    {
                        rec.RouteKey = val;
                        trace.Add($"route <- '{s}' => {rec.Name}:{val}");
                    }
                    continue;
                }
                var mRouteEn = Regex.Match(s, @"(?i)(?:route|destination|sector|area|zone|voyage|path)[:\s]+(?<route>.+)");
                if (mRouteEn.Success)
                {
                    var val = mRouteEn.Groups["route"].Value.Trim();
                    if (!string.IsNullOrEmpty(current) && byName.TryGetValue(current, out var rec) && string.IsNullOrEmpty(rec.RouteKey))
                    {
                        rec.RouteKey = val;
                        trace.Add($"route <- '{s}' => {rec.Name}:{val}");
                    }
                    continue;
                }
            }

            if (snapshot.Items.Count > 4)
                snapshot.Items = snapshot.Items.Take(4).ToList();

            // プレースホルダ(Submarine-<n>)だけで、時間/ランク/航路のヒントが無ければ不合格
            bool placeholdersOnly = snapshot.Items.Count > 0 && snapshot.Items.All(x => Regex.IsMatch(x.Name ?? string.Empty, @"^Submarine-\d+$"));
            bool hasHints = snapshot.Items.Any(x => x.DurationMinutes.HasValue || x.Rank.HasValue || !string.IsNullOrEmpty(x.RouteKey));
            if (placeholdersOnly && !hasHints)
                return false;

            return snapshot.Items.Count > 0;
        }
    }
}
