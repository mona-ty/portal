using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace XIVSubmarinesReturn.Sectors
{
    public sealed class AliasIndex
    {
        public Dictionary<string, Dictionary<string, uint>> MapAliasToSector { get; set; }
            = new Dictionary<string, Dictionary<string, uint>>(StringComparer.OrdinalIgnoreCase);

        public static AliasIndex LoadOrDefault(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var opts = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    var idx = JsonSerializer.Deserialize<AliasIndex>(json, opts);
                    if (idx != null) return idx;
                }
            }
            catch { }

            // default (Deep-sea Site: R -> 18)
            var def = new AliasIndex();
            def.MapAliasToSector["Deep-sea Site"] = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
            {
                ["R"] = 18u
            };
            return def;
        }

        public void Save(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(this, opts));
            }
            catch { }
        }
    }
}

