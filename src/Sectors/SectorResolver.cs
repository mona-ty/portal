using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XIVSubmarinesReturn.Sectors;

namespace XIVSubmarinesReturn.Sectors
{
    public sealed class SectorResolver
    {
        private readonly Dalamud.Plugin.Services.IDataManager _data;
        private readonly Dalamud.Plugin.Services.IPluginLog _log;
        private readonly string _aliasPath;

        private readonly Dictionary<uint, SectorEntry> _byId = new();
        private readonly Dictionary<(string map, uint id), SectorEntry> _byMapId = new();
        private readonly Dictionary<(string map, string alias), SectorEntry> _byAlias = new();
        private string _sheetTypeName = string.Empty;

        public SectorResolver(Dalamud.Plugin.Services.IDataManager data, string aliasPath, Dalamud.Plugin.Services.IPluginLog log)
        {
            _data = data; _aliasPath = aliasPath; _log = log;
            try { LoadExcel(); }
            catch (Exception ex) { try { _log.Warning(ex, "SectorResolver: LoadExcel failed"); } catch { } }
            try { ApplyAliasIndex(AliasIndex.LoadOrDefault(_aliasPath)); }
            catch (Exception ex) { try { _log.Warning(ex, "SectorResolver: ApplyAlias failed"); } catch { } }
        }

        public void ReloadAliasIndex()
        {
            try
            {
                ApplyAliasIndex(AliasIndex.LoadOrDefault(_aliasPath));
            }
            catch (Exception ex) { try { _log.Warning(ex, "SectorResolver: ReloadAlias failed"); } catch { } }
        }

        public string? GetAliasForSector(uint id, string? mapHint)
        {
            try
            {
                if (!string.IsNullOrEmpty(mapHint) && _byMapId.TryGetValue((mapHint, id), out var e1) && !string.IsNullOrWhiteSpace(e1.Alias))
                    return e1.Alias;
                if (_byId.TryGetValue(id, out var e) && !string.IsNullOrWhiteSpace(e.Alias))
                    return e.Alias;
            }
            catch { }
            return null;
        }

        public ResolveResult ResolveCode(string code, string? mapHint = null)
        {
            var res = new ResolveResult();
            try
            {
                var s = (code ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(s)) { res.Ambiguous = true; res.Note = "empty"; return res; }

                // split letters and digits (up to 2 letters like 'AA')
                string letters = string.Empty; string digits = string.Empty;
                foreach (var ch in s.ToUpperInvariant())
                {
                    if (ch >= 'A' && ch <= 'Z') letters += ch;
                    else if (char.IsDigit(ch)) digits += ch;
                }

                if (!string.IsNullOrEmpty(digits) && uint.TryParse(digits, out var id))
                {
                    if (_byId.TryGetValue(id, out var e))
                    {
                        string? note = null;
                        if (!string.IsNullOrEmpty(letters) && !string.Equals(letters, e.Alias, StringComparison.OrdinalIgnoreCase))
                            note = $"alias mismatch: input='{letters}' data='{e.Alias ?? "-"}'";
                        res.Match = SelectByMapHint(e, mapHint);
                        res.Note = note;
                        return res;
                    }
                }

                if (!string.IsNullOrEmpty(letters))
                {
                    if (!string.IsNullOrEmpty(mapHint))
                    {
                        if (_byAlias.TryGetValue((mapHint, letters), out var e2)) { res.Match = e2; return res; }
                        res.Ambiguous = true; res.Note = $"alias '{letters}' not found in '{mapHint}'"; return res;
                    }
                    var cands = _byId.Values.Where(v => string.Equals(v.Alias, letters, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (cands.Count == 1) { res.Match = cands[0]; return res; }
                    res.Ambiguous = true; res.Candidates = cands; res.Note = "ambiguous"; return res;
                }

                res.Ambiguous = true; res.Note = "unrecognized"; return res;
            }
            catch (Exception ex) { try { _log.Warning(ex, "ResolveCode failed"); } catch { } return res; }
        }

        private SectorEntry SelectByMapHint(SectorEntry e, string? mapHint)
        {
            if (string.IsNullOrEmpty(mapHint)) return e;
            try
            {
                if (_byMapId.TryGetValue((mapHint, e.SectorId), out var m)) return m;
            }
            catch { }
            return e;
        }

        private void LoadExcel()
        {
            // Try to get SubmarineExploration sheet type (GeneratedSheets or Sheets) from loaded assemblies
            var t = TryFindSheetType();
            if (t == null) return;
            _sheetTypeName = t.FullName ?? t.Name;

            // Find IDataManager.GetExcelSheet<T>() via reflection
            var mi = _data.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "GetExcelSheet" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1);
            if (mi == null) return;
            var gm = mi.MakeGenericMethod(t);
            var sheet = gm.Invoke(_data, null);
            if (sheet == null) return;

            // sheet is IEnumerable<T>
            var enumerable = sheet as System.Collections.IEnumerable;
            if (enumerable == null) return;

            foreach (var row in enumerable)
            {
                try
                {
                    var id = (uint)(row?.GetType().GetProperty("RowId")?.GetValue(row) ?? 0u);
                    if (id == 0) continue;
                    var name = TryGetString(row, "Name") ?? TryGetString(row, "PlaceName");
                    var mapName = TryGetMapName(row) ?? string.Empty;
                    var e = new SectorEntry { SectorId = id, PlaceName = name ?? string.Empty, MapName = mapName };
                    _byId[id] = e;
                    _byMapId[(mapName, id)] = e;
                }
                catch { }
            }
        }

        private static Type? TryFindSheetType()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = asm.GetType("Lumina.Excel.GeneratedSheets.SubmarineExploration", false);
                        if (t != null) return t;
                        t = asm.GetType("Lumina.Excel.Sheets.SubmarineExploration", false);
                        if (t != null) return t;
                    }
                    catch { }
                }
                // Fallback: scan by simple name
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types?.Where(x => x != null).Cast<Type>().ToArray() ?? Array.Empty<Type>(); }
                    catch { continue; }
                    var hit = types.FirstOrDefault(x => x != null && string.Equals(x.Name, "SubmarineExploration", StringComparison.Ordinal));
                    if (hit != null) return hit;
                }
            }
            catch { }
            return null;
        }

        private static string? TryGetString(object row, string prop)
        {
            try
            {
                var p = row.GetType().GetProperty(prop);
                var v = p?.GetValue(row);
                return v?.ToString();
            }
            catch { return null; }
        }

        private static string? TryGetMapName(object row)
        {
            try
            {
                var map = row.GetType().GetProperty("Map")?.GetValue(row);
                var valProp = map?.GetType().GetProperty("Value");
                var value = valProp?.GetValue(map);
                var nameProp = value?.GetType().GetProperty("Name");
                var name = nameProp?.GetValue(value)?.ToString();
                return name;
            }
            catch { return null; }
        }

        private void ApplyAliasIndex(AliasIndex idx)
        {
            try
            {
                _byAlias.Clear();
                foreach (var kv in idx.MapAliasToSector)
                {
                    var map = kv.Key;
                    foreach (var av in kv.Value)
                    {
                        var alias = av.Key?.ToUpperInvariant() ?? string.Empty;
                        var id = av.Value;
                        // Ensure minimal entries even if Excel failed
                        if (!_byId.TryGetValue(id, out var e))
                        {
                            e = new SectorEntry { SectorId = id, MapName = map, PlaceName = string.Empty };
                            _byId[id] = e;
                        }
                        // Map-specific entry
                        _byMapId[(map, id)] = e;
                        e.MapName = string.IsNullOrWhiteSpace(e.MapName) ? map : e.MapName;
                        e.Alias = alias;
                        _byAlias[(map, alias)] = e;
                    }
                }
            }
            catch (Exception ex) { try { _log.Warning(ex, "ApplyAliasIndex failed"); } catch { } }
        }

        public string AliasPath => _aliasPath;

        public string GetDebugReport()
        {
            try
            {
                var maps = new Dictionary<string, (int ids, int aliases)>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in _byMapId.Keys)
                {
                    if (!maps.TryGetValue(k.map, out var t)) t = (0, 0);
                    t.ids++;
                    maps[k.map] = t;
                }
                foreach (var k in _byAlias.Keys)
                {
                    if (!maps.TryGetValue(k.map, out var t)) t = (0, 0);
                    t.aliases++;
                    maps[k.map] = t;
                }

                var lines = new List<string>();
                lines.Add($"[sv] AliasPath: {_aliasPath}");
                try { var fi = new System.IO.FileInfo(_aliasPath); if (fi.Exists) lines.Add($"[sv] Alias mtime: {fi.LastWriteTime}"); } catch { }
                lines.Add($"[sv] ExcelType: {(_sheetTypeName ?? "(none)")}");
                lines.Add($"[sv] Counts: ids={_byId.Count}, mapIds={_byMapId.Count}, aliases={_byAlias.Count}, maps={maps.Count}");
                foreach (var kv in maps.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Take(20))
                {
                    lines.Add($"[sv] Map: {kv.Key} ids={kv.Value.ids} aliases={kv.Value.aliases}");
                }
                return string.Join(Environment.NewLine, lines);
            }
            catch { return "[sv] debug failed"; }
        }

        public bool ExportSectors(string path)
        {
            try
            {
                var maps = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in _byId.Values)
                {
                    if (!maps.TryGetValue(e.MapName ?? string.Empty, out var list))
                    {
                        list = new List<object>();
                        maps[e.MapName ?? string.Empty] = list;
                    }
                    list.Add(new { e.SectorId, e.PlaceName, Alias = e.Alias });
                }
                var payload = new Dictionary<string, object>();
                foreach (var kv in maps.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    payload[kv.Key] = kv.Value.OrderBy(x => (x as dynamic).SectorId).ToArray();
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
                var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(payload, opts));
                return true;
            }
            catch { return false; }
        }
    }
}
