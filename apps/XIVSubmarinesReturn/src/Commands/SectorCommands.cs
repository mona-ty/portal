using System;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using XIVSubmarinesReturn.Sectors;

namespace XIVSubmarinesReturn.Commands
{
    public sealed class SectorCommands : IDisposable
    {
        private readonly IDalamudPluginInterface _pi;
        private readonly ICommandManager _cmds;
        private readonly IChatGui _chat;
        private readonly SectorResolver _resolver;
        private readonly XIVSubmarinesReturn.Sectors.MogshipImporter _importer;

        private const string Cmd = "/sv";

        public SectorCommands(IDalamudPluginInterface pi, ICommandManager cmds, IChatGui chat, SectorResolver resolver, System.Net.Http.HttpClient http, Dalamud.Plugin.Services.IPluginLog log)
        {
            _pi = pi; _cmds = cmds; _chat = chat; _resolver = resolver;
            _importer = new XIVSubmarinesReturn.Sectors.MogshipImporter(http, log);
            _cmds.AddHandler(Cmd, new CommandInfo(OnCmd)
            {
                HelpMessage = "/sv test <code> 例: /sv test P18"
            });
        }

        public void Dispose() { try { _cmds.RemoveHandler(Cmd); } catch { } }

        private void OnCmd(string cmd, string args)
        {
            try
            {
                var parts = (args ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 1 && parts[0].Equals("debug", StringComparison.OrdinalIgnoreCase))
                {
                    var report = _resolver.GetDebugReport();
                    foreach (var line in report.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                        _chat.Print(line);
                    return;
                }
                if (parts.Length >= 2 && parts[0].Equals("export-sectors", StringComparison.OrdinalIgnoreCase))
                {
                    var outPath = System.IO.Path.Combine(_pi.ConfigDirectory?.FullName ?? string.Empty, "Sectors.export.json");
                    var ok = _resolver.ExportSectors(outPath);
                    _chat.Print(ok ? $"[sv] exported: {outPath}" : "[sv] export failed");
                    return;
                }
                if (parts.Length >= 2 && parts[0].Equals("import-alias", StringComparison.OrdinalIgnoreCase))
                {
                    var aliasPath = System.IO.Path.Combine(_pi.ConfigDirectory?.FullName ?? string.Empty, "AliasIndex.json");
                    _chat.Print("[sv] importing from Mogship...");
                    try
                    {
                        var t = _importer.ImportAsync(aliasPath);
                        t.Wait();
                        var (maps, aliases) = t.Result;
                        _resolver.ReloadAliasIndex();
                        _chat.Print($"[sv] imported {aliases} aliases across {maps} maps");
                    }
                    catch (Exception ex) { _chat.Print($"[sv] import failed: {ex.Message}"); }
                    return;
                }
                if (parts.Length >= 2 && parts[0].Equals("test", StringComparison.OrdinalIgnoreCase))
                {
                    var code = parts[1];
                    var r = _resolver.ResolveCode(code);
                    if (r.Match != null)
                    {
                        var m = r.Match;
                        _chat.Print($"[sv] [{code}] => Map={m.MapName}, Alias={m.Alias ?? "-"}, SectorId={m.SectorId}, Name={m.PlaceName}{(string.IsNullOrEmpty(r.Note) ? string.Empty : $" ({r.Note})")}");
                    }
                    else if (r.Ambiguous && r.Candidates != null && r.Candidates.Count > 0)
                    {
                        var s = string.Join(" | ", r.Candidates.ConvertAll(c => $"{c.MapName}:{c.Alias}/{c.SectorId}:{c.PlaceName}"));
                        _chat.Print($"[sv] [{code}] ambiguous: {s}");
                    }
                    else
                    {
                        _chat.Print($"[sv] [{code}] not found: {r.Note}");
                    }
                    return;
                }
                _chat.Print("usage: /sv debug | /sv export-sectors go | /sv import-alias mogship | /sv test <code>");
            }
            catch (Exception ex) { try { _chat.Print($"[sv] error: {ex.Message}"); } catch { } }
        }
    }
}
