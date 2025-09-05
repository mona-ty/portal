using System;
using System.Collections.Generic;

namespace FF14SubmarinesAct
{
    public sealed class SubmarineInfo
    {
        public string Name { get; set; } = string.Empty;
        public DateTime DepartedAt { get; set; }
        public DateTime Eta { get; set; }
        public int DurationMinutes { get; set; }
        public string RouteKey { get; set; } = string.Empty;
    }

    public sealed class SubmarineTracker
    {
        private readonly Dictionary<string, SubmarineInfo> _byName = new Dictionary<string, SubmarineInfo>(StringComparer.Ordinal);
        private readonly Func<int> _getDefaultMinutes;
        private readonly Func<string, int?> _getOverrideMinutes;

        public SubmarineTracker(Func<int> getDefaultMinutes, Func<string, int?> getOverrideMinutes = null)
        {
            _getDefaultMinutes = getDefaultMinutes;
            _getOverrideMinutes = getOverrideMinutes;
        }

        public IReadOnlyCollection<SubmarineInfo> Current => _byName.Values;

        public void Apply(SubmarineEvent ev)
        {
            if (ev == null || string.IsNullOrWhiteSpace(ev.Name)) return;
            switch (ev.Kind)
            {
                case SubmarineEventKind.Departed:
                    var o = _getOverrideMinutes != null ? _getOverrideMinutes(ev.Name) : null;
                    var mins = Math.Max(1, o ?? _getDefaultMinutes());
                    _byName[ev.Name] = new SubmarineInfo
                    {
                        Name = ev.Name,
                        DepartedAt = ev.Timestamp,
                        DurationMinutes = mins,
                        Eta = ev.Timestamp.AddMinutes(mins)
                    };
                    break;
                case SubmarineEventKind.Completed:
                    _byName.Remove(ev.Name);
                    break;
            }
        }

        public void RefreshAll(Func<string, int> getMinutesForName)
        {
            foreach (var kv in new List<KeyValuePair<string, SubmarineInfo>>(_byName))
            {
                var s = kv.Value;
                var mins = Math.Max(1, getMinutesForName(s.Name));
                s.DurationMinutes = mins;
                s.Eta = s.DepartedAt.AddMinutes(mins);
            }
        }

        // Optionally attach a detected route key later (e.g., from network list parsing)
        // and re-calculate ETA using the route-specific override when available.
        public void SetRoute(string name, string routeKey, Func<string, int?> tryGetOverride)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(routeKey)) return;
            if (!_byName.TryGetValue(name, out var s)) return;
            s.RouteKey = routeKey;

            int mins = s.DurationMinutes;
            try
            {
                var o = tryGetOverride?.Invoke(routeKey) ?? tryGetOverride?.Invoke(name);
                if (o.HasValue && o.Value > 0)
                {
                    mins = o.Value;
                    s.DurationMinutes = mins;
                    s.Eta = s.DepartedAt.AddMinutes(mins);
                }
            }
            catch { }
        }
    }
}
