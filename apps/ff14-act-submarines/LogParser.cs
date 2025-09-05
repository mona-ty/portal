using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FF14SubmarinesAct
{
    public enum SubmarineEventKind { Departed, Completed }

    public sealed class SubmarineEvent
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public SubmarineEventKind Kind { get; set; }
    }

    public static class LogParser
    {
        // JP patterns (can extend with EN later)
        private static readonly Regex ReDepart = new Regex(
            "潜水艦「(?<name>.+?)」が出港しました",
            RegexOptions.Compiled);

        private static readonly Regex ReCompleted = new Regex(
            "潜水艦「(?<name>.+?)」.*探索完了",
            RegexOptions.Compiled);

        public static bool TryParse(string line, out SubmarineEvent ev)
        {
            ev = null;
            if (string.IsNullOrEmpty(line)) return false;

            string message = line;
            DateTime ts = DateTime.Now;

            // Try ACT network log format: 00|timestamp|0039||message|...
            var parts = line.Split('|');
            if (parts.Length >= 5 && parts[0] == "00")
            {
                // 0039 is the chat-log-like category that carries the message text
                // Some variants include a timezone; parse leniently
                if (parts.Length > 1)
                {
                    if (DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal, out var parsed))
                        ts = parsed;
                }
                message = parts[4];
            }

            // Try matching
            var m1 = ReDepart.Match(message);
            if (m1.Success)
            {
                ev = new SubmarineEvent
                {
                    Kind = SubmarineEventKind.Departed,
                    Name = m1.Groups["name"].Value,
                    Timestamp = ts
                };
                return true;
            }

            var m2 = ReCompleted.Match(message);
            if (m2.Success)
            {
                ev = new SubmarineEvent
                {
                    Kind = SubmarineEventKind.Completed,
                    Name = m2.Groups["name"].Value,
                    Timestamp = ts
                };
                return true;
            }

            return false;
        }
    }
}

