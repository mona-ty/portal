using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;
using ImGuiWindowFlags = Dalamud.Bindings.ImGui.ImGuiWindowFlags;
using System.Numerics;

namespace XIVSubmarinesReturn;

public sealed partial class Plugin
{
    private bool _showUI;
    private string _uiStatus = string.Empty;
    private SubmarineSnapshot? _uiSnapshot;
    private SubmarineSnapshot? _uiPrevSnapshot;
    private DateTime _uiLastReadUtc = DateTime.MinValue;
    private string _probeText = string.Empty;
    private int _routeEditId;
    private string _routeEditName = string.Empty;

    private void InitUI()
    {
        try
        {
            _pi.UiBuilder.Draw += DrawUI;
            _pi.UiBuilder.OpenConfigUi += () => _showUI = true;
        }
        catch { }
    }

    private void DrawUI()
    {
        if (!_showUI) return;
        try
        {
            ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(520, 320), new System.Numerics.Vector2(2000, 2000));
            if (!ImGui.Begin("XIV Submarines Return", ref _showUI, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.End();
                return;
            }

            // Toggles
            bool autoCap = Config.AutoCaptureOnWorkshopOpen;
            if (ImGui.Checkbox("Auto-capture on workshop open", ref autoCap))
            {
                Config.AutoCaptureOnWorkshopOpen = autoCap;
                SaveConfig();
            }
            bool memFb = Config.UseMemoryFallback;
            if (ImGui.Checkbox("Use memory fallback (when UI fails)", ref memFb))
            {
                Config.UseMemoryFallback = memFb;
                SaveConfig();
            }

            // Additional extraction/config toggles
            bool selExt = Config.UseSelectStringExtraction;
            if (ImGui.Checkbox("Use SelectString extraction", ref selExt))
            {
                Config.UseSelectStringExtraction = selExt;
                SaveConfig();
            }
            bool selDet = Config.UseSelectStringDetailExtraction;
            if (ImGui.Checkbox("Use SelectString detail extraction", ref selDet))
            {
                Config.UseSelectStringDetailExtraction = selDet;
                SaveConfig();
            }
            bool aggr = Config.AggressiveFallback;
            if (ImGui.Checkbox("Aggressive fallback (when lines are sparse)", ref aggr))
            {
                Config.AggressiveFallback = aggr;
                SaveConfig();
            }
            bool acceptDefaults = Config.AcceptDefaultNamesInMemory;
            if (ImGui.Checkbox("Accept default names in memory (Submarine-<n>)", ref acceptDefaults))
            {
                Config.AcceptDefaultNamesInMemory = acceptDefaults;
                SaveConfig();
            }

            // Addon name input
            var addonName = Config.AddonName ?? string.Empty;
            if (ImGui.InputText("Addon Name", ref addonName, 64))
            {
                Config.AddonName = addonName;
                SaveConfig();
            }

            ImGui.Separator();
            ImGui.Text("Slot Aliases (1..4)");
            for (int i = 0; i < 4; i++)
            {
                var val = Config.SlotAliases != null && i < Config.SlotAliases.Length ? (Config.SlotAliases[i] ?? string.Empty) : string.Empty;
                var tmp = val;
                ImGui.PushID(i);
                if (ImGui.InputText("##alias", ref tmp, 64))
                {
                    if (Config.SlotAliases == null || Config.SlotAliases.Length < 4)
                        Config.SlotAliases = new string[4];
                    Config.SlotAliases[i] = tmp;
                    SaveConfig();
                }
                ImGui.SameLine();
                ImGui.Text($"Slot {i + 1}");
                ImGui.PopID();
            }

            ImGui.Separator();
            if (ImGui.Button("Learn names from UI"))
            {
                try { CmdLearnNames(); _uiStatus = "Learn triggered"; }
                catch (Exception ex) { _uiStatus = $"Learn failed: {ex.Message}"; }
            }
            ImGui.SameLine();
            if (ImGui.Button("Capture (UI)"))
            {
                try { OnCmdDump("/subdump", string.Empty); _uiStatus = "Capture(UI) triggered"; }
                catch (Exception ex) { _uiStatus = $"Capture(UI) failed: {ex.Message}"; }
            }
            ImGui.SameLine();
            if (ImGui.Button("Capture (Memory)"))
            {
                try { CmdDumpFromMemory(); _uiStatus = "Capture(Memory) triggered"; }
                catch (Exception ex) { _uiStatus = $"Capture(Memory) failed: {ex.Message}"; }
            }
            if (ImGui.Button("Probe addons"))
            {
                try { _probeText = ProbeToText(); _uiStatus = "Probe done"; }
                catch (Exception ex) { _uiStatus = $"Probe failed: {ex.Message}"; }
            }
            ImGui.SameLine();
            if (ImGui.Button("Open folder"))
            {
                try { OnCmdOpen("/subopen", string.Empty); _uiStatus = "Folder opened"; }
                catch (Exception ex) { _uiStatus = $"Open failed: {ex.Message}"; }
            }

            if (!string.IsNullOrEmpty(_uiStatus)) { ImGui.SameLine(); ImGui.Text(_uiStatus); }

            ImGui.Separator();
            if (ImGui.Button("Refresh")) { _uiLastReadUtc = DateTime.MinValue; }
            ImGui.SameLine();
            if (!string.IsNullOrEmpty(_probeText) && ImGui.Button("Clear probe")) { _probeText = string.Empty; }

            DrawSnapshotTable();

            if (!string.IsNullOrEmpty(_probeText))
            {
                ImGui.Separator();
                ImGui.Text("Probe result:");
                ImGui.BeginChild("probe", new Vector2(480, 120), true, ImGuiWindowFlags.None);
                ImGui.TextWrapped(_probeText);
                ImGui.EndChild();
            }

            // Route names editor (simple)
            ImGui.Separator();
            ImGui.Text("Route Names (ID -> Display)");
            ImGui.InputInt("ID", ref _routeEditId);
            ImGui.InputText("Name", ref _routeEditName, 64);
            if (ImGui.Button("Add/Update"))
            {
                try
                {
                    if (_routeEditId >= 0 && _routeEditId <= 255)
                    {
                        Config.RouteNames[(byte)_routeEditId] = _routeEditName ?? string.Empty;
                        SaveConfig();
                        _uiStatus = "Route name saved";
                    }
                }
                catch (Exception ex) { _uiStatus = $"Route save failed: {ex.Message}"; }
            }
            if (Config.RouteNames != null && Config.RouteNames.Count > 0)
            {
                if (ImGui.BeginTable("routes", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("ID");
                    ImGui.TableSetupColumn("Name");
                    ImGui.TableHeadersRow();
                    foreach (var kv in Config.RouteNames.OrderBy(k => k.Key))
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0); ImGui.Text(kv.Key.ToString());
                        ImGui.TableSetColumnIndex(1); ImGui.Text(kv.Value ?? string.Empty);
                    }
                    ImGui.EndTable();
                }
            }

            ImGui.End();
        }
        catch { try { ImGui.End(); } catch { } }
    }

    private void DrawSnapshotTable()
    {
        try
        {
            // Reload snapshot if file changed or last read too old (5s)
            var path = BridgeWriter.CurrentFilePath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var lastWrite = File.GetLastWriteTimeUtc(path);
                if (_uiSnapshot == null || lastWrite > _uiLastReadUtc || (DateTime.UtcNow - _uiLastReadUtc) > TimeSpan.FromSeconds(5))
                {
                    try
                    {
                        _uiPrevSnapshot = _uiSnapshot;
                        var json = File.ReadAllText(path);
                        _uiSnapshot = JsonSerializer.Deserialize<SubmarineSnapshot>(json);
                        _uiLastReadUtc = lastWrite;
                    }
                    catch (Exception ex)
                    {
                        _uiStatus = $"Read json failed: {ex.Message}";
                    }
                }
            }

            ImGui.Text($"Snapshot: {( _uiSnapshot?.Items?.Count ?? 0)} items");
            if (_uiSnapshot?.Items == null || _uiSnapshot.Items.Count == 0)
            {
                ImGui.TextDisabled("No data found. Try Capture.");
                return;
            }

            if (ImGui.BeginTable("subs", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Rank");
                ImGui.TableSetupColumn("ETA(min)");
                ImGui.TableSetupColumn("Route");
                ImGui.TableHeadersRow();

                foreach (var it in _uiSnapshot.Items)
                {
                    bool changed = IsChanged(it);
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    if (changed) { ImGui.Text($"* {it.Name}"); }
                    else ImGui.Text(it.Name ?? string.Empty);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(it.Rank?.ToString() ?? "");
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(it.DurationMinutes?.ToString() ?? "");
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text(it.RouteKey ?? "");
                }

                ImGui.EndTable();
            }
        }
        catch (Exception ex)
        {
            _uiStatus = $"Table error: {ex.Message}";
        }
    }

    private bool IsChanged(SubmarineRecord it)
    {
        try
        {
            var prev = _uiPrevSnapshot?.Items?.FirstOrDefault(x => string.Equals(x.Name, it.Name, StringComparison.Ordinal));
            if (prev == null) return false;
            if (prev.Rank != it.Rank) return true;
            if (prev.DurationMinutes != it.DurationMinutes) return true;
            if (!string.Equals(prev.RouteKey ?? string.Empty, it.RouteKey ?? string.Empty, StringComparison.Ordinal)) return true;
            return false;
        }
        catch { return false; }
    }

    private string ProbeToText()
    {
        var candidates = new[]
        {
            Config.AddonName,
            "SelectString", "SelectIconString",
            "CompanyCraftSubmersibleList", "FreeCompanyWorkshopSubmersible", "CompanyCraftSubmersible", "CompanyCraftList",
            "SubmersibleExploration", "SubmarineExploration", "SubmersibleVoyage", "ExplorationResult",
        };
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("[Submarines] Probing addon availability...");
        unsafe
        {
            foreach (var n in candidates.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal))
            {
                bool shown = false, exists = false; int foundIdx = -1;
                for (int i = 0; i < 16; i++)
                {
                    var u = ToPtr(_gameGui.GetAddonByName(n, i));
                    if (u != null)
                    {
                        exists = true;
                        if (u->IsVisible) { shown = true; foundIdx = i; break; }
                    }
                }
                lines.Add($"  {n}: " + (shown ? $"visible (idx={foundIdx})" : (exists ? "exists (hidden)" : "not found")));
            }
        }
        return string.Join(Environment.NewLine, lines);
    }
}
