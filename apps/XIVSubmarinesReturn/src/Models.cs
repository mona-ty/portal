using System;
using System.Collections.Generic;

namespace XIVSubmarinesReturn
{
    public enum RouteConfidence
    {
        None = 0,
        Tail = 1,
        Partial = 2,
        Full = 3,
        Array = 4, // RequestedUpdate(ArrayData) 直読
    }

    public sealed class LastGoodRoute
    {
        public string RouteKey { get; set; } = string.Empty; // Point-xx - ...
        public RouteConfidence Confidence { get; set; } = RouteConfidence.None;
        public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.MinValue;
        public string Source { get; set; } = string.Empty; // mem|cache|array
    }

    public sealed class SubmarineRecord
    {
        public string Name { get; set; } = string.Empty;
        public string RouteKey { get; set; } = string.Empty; // route key or ID
        public int? DurationMinutes { get; set; } // nullable if unknown
        public int? Rank { get; set; } // nullable rank

        // Extensions (optional)
        public int? Slot { get; set; } // 1..4
        public bool? IsDefaultName { get; set; }
        public long? EtaUnix { get; set; } // epoch seconds
        public Dictionary<string, string>? Extra { get; set; }
    }

    public sealed class SubmarineSnapshot
    {
        public int SchemaVersion { get; set; } = 2;
        public string PluginVersion { get; set; } = string.Empty;
        public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
        public List<SubmarineRecord> Items { get; set; } = new();
        public string Source { get; set; } = "dalamud";
        public string Note { get; set; } = string.Empty;
        // v3 optional fields (identity)
        public string? Character { get; set; }
        public string? World { get; set; }
        public string? FreeCompany { get; set; }
    }
}
