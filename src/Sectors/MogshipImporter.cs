using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XIVSubmarinesReturn.Sectors
{
    internal sealed class MogshipImporter
    {
        private readonly HttpClient _http;
        private readonly Dalamud.Plugin.Services.IPluginLog _log;

        public MogshipImporter(HttpClient http, Dalamud.Plugin.Services.IPluginLog log)
        {
            _http = http; _log = log;
        }

        public async Task<(int maps, int aliases)> ImportAsync(string aliasPath, CancellationToken ct = default)
        {
            var idx = AliasIndex.LoadOrDefault(aliasPath);
            var mapNames = await FetchMapsAsync(ct).ConfigureAwait(false);
            var sectors = await FetchSectorsAsync(ct).ConfigureAwait(false);

            int aliasCount = 0;
            foreach (var s in sectors)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(s.letter) || s.id <= 0 || s.mapId <= 0) continue;
                    var mapName = mapNames.TryGetValue(s.mapId, out var mn) ? mn : ("Map-" + s.mapId);
                    if (!idx.MapAliasToSector.TryGetValue(mapName, out var table))
                    {
                        table = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                        idx.MapAliasToSector[mapName] = table;
                    }
                    table[s.letter.ToUpperInvariant()] = (uint)s.id;
                    aliasCount++;
                }
                catch { }
            }

            try { idx.Save(aliasPath); } catch { }
            return (idx.MapAliasToSector.Count, aliasCount);
        }

        private async Task<Dictionary<int, string>> FetchMapsAsync(CancellationToken ct)
        {
            var url = "https://api.mogship.com/submarine/maps";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("XIVSubmarinesReturn/1.0");
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var map = new Dictionary<int, string>();
                var res = doc.RootElement.GetProperty("results");
                foreach (var el in res.EnumerateArray())
                {
                    try
                    {
                        var id = el.GetProperty("id").GetInt32();
                        var name = el.GetProperty("name_en").GetString() ?? string.Empty;
                        if (id > 0 && !string.IsNullOrWhiteSpace(name)) map[id] = name;
                    }
                    catch { }
                }
                return map;
            }
            catch (Exception ex) { try { _log.Warning(ex, "FetchMaps failed"); } catch { } return new(); }
        }

        private async Task<List<(int id, int mapId, string letter)>> FetchSectorsAsync(CancellationToken ct)
        {
            var url = "https://api.mogship.com/submarine/sectors";
            var list = new List<(int, int, string)>();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("XIVSubmarinesReturn/1.0");
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var res = doc.RootElement.GetProperty("results");
                foreach (var el in res.EnumerateArray())
                {
                    try
                    {
                        var id = el.GetProperty("id").GetInt32();
                        var mapId = el.GetProperty("mapId").GetInt32();
                        var letter = el.GetProperty("lettername_en").GetString() ?? string.Empty;
                        if (id > 0 && mapId > 0 && !string.IsNullOrWhiteSpace(letter))
                            list.Add((id, mapId, letter));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { try { _log.Warning(ex, "FetchSectors failed"); } catch { } }
            return list;
        }
    }
}

