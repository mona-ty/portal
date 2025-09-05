using System;
using System.Collections.Generic;

namespace XIVSubmarinesReturn
{
    public sealed class SubmarineRecord
    {
        public string Name { get; set; } = string.Empty;
        public string RouteKey { get; set; } = string.Empty; // route key or ID
        public int? DurationMinutes { get; set; } // nullable if unknown
        public int? Rank { get; set; } // nullable rank
    }

    public sealed class SubmarineSnapshot
    {
        public int SchemaVersion { get; set; } = 1;
        public string PluginVersion { get; set; } = string.Empty;
        public DateTime CapturedAt { get; set; } = DateTime.Now;
        public List<SubmarineRecord> Items { get; set; } = new();
        public string Source { get; set; } = "dalamud";
        public string Note { get; set; } = string.Empty;
    }
}

