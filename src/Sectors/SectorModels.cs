using System;
using System.Collections.Generic;

namespace XIVSubmarinesReturn.Sectors
{
    public sealed class SectorEntry
    {
        public uint SectorId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public string? Alias { get; set; }
    }

    public sealed class ResolveResult
    {
        public SectorEntry? Match { get; set; }
        public bool Ambiguous { get; set; }
        public List<SectorEntry>? Candidates { get; set; }
        public string? Note { get; set; }
    }
}

