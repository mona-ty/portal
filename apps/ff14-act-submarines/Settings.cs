using System;

namespace FF14SubmarinesAct
{
    public class DurationOverride
    {
        public string Key { get; set; } = string.Empty; // 艦名 または ルート名
        public int Minutes { get; set; }
    }

    public class Settings
    {
        public string ActFolder { get; set; } = string.Empty;
        public string FfxivPluginPath { get; set; } = string.Empty;
        public string LogsFolder { get; set; } = string.Empty;
        public int DefaultDurationMinutes { get; set; } = 180; // 仮: 3時間。環境に合わせて変更可
        public System.Collections.Generic.List<DurationOverride> DurationOverrides { get; set; } = new System.Collections.Generic.List<DurationOverride>();

        public int? TryGetOverrideMinutes(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || DurationOverrides == null) return null;
            foreach (var x in DurationOverrides)
            {
                if (x == null) continue;
                if (string.Equals(x.Key, key, System.StringComparison.OrdinalIgnoreCase))
                    return x.Minutes;
            }
            return null;
        }
    }
}
