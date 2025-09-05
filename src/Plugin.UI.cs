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
    private string _alarmLeadText = string.Empty;

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
            // Google Calendar
            if (ImGui.CollapsingHeader("Google Calendar"))
            {
                bool gEnable = Config.GoogleEnabled;
                if (ImGui.Checkbox("Enable", ref gEnable)) { Config.GoogleEnabled = gEnable; SaveConfig(); }
                int modeVal = Config.GoogleEventMode == CalendarMode.Latest ? 1 : 0;
                if (ImGui.RadioButton("All", modeVal == 0)) { Config.GoogleEventMode = CalendarMode.All; SaveConfig(); }
                ImGui.SameLine();
                if (ImGui.RadioButton("Latest only", modeVal == 1)) { Config.GoogleEventMode = CalendarMode.Latest; SaveConfig(); }
                var calId = Config.GoogleCalendarId ?? string.Empty;
                if (ImGui.InputText("CalendarId", ref calId, 128)) { Config.GoogleCalendarId = calId; SaveConfig(); }
                var rt = Config.GoogleRefreshToken ?? string.Empty;
                if (ImGui.InputText("RefreshToken", ref rt, 256)) { Config.GoogleRefreshToken = rt; SaveConfig(); }
                var cid = Config.GoogleClientId ?? string.Empty;
                if (ImGui.InputText("ClientId", ref cid, 256)) { Config.GoogleClientId = cid; SaveConfig(); }
                var cs = Config.GoogleClientSecret ?? string.Empty;
                if (ImGui.InputText("ClientSecret", ref cs, 256)) { Config.GoogleClientSecret = cs; SaveConfig(); }
                if (ImGui.Button("Test Google"))
                {
                    try
                    {
                        bool ok = _gcal != null && _gcal.EnsureAuthorizedAsync().GetAwaiter().GetResult();
                        _uiStatus = ok ? "GCal auth OK" : "GCal not ready";
                    }
                    catch (System.Exception ex) { _uiStatus = $"GCal test failed: {ex.Message}"; }
                }
            }

            // Discord
            if (ImGui.CollapsingHeader("Discord"))
            {
                bool dEnable = Config.DiscordEnabled;
                if (ImGui.Checkbox("Enable", ref dEnable)) { Config.DiscordEnabled = dEnable; SaveConfig(); }
                var wh = Config.DiscordWebhookUrl ?? string.Empty;
                if (ImGui.InputText("Webhook URL", ref wh, 512)) { Config.DiscordWebhookUrl = wh; SaveConfig(); }
                bool latestOnly = Config.DiscordLatestOnly;
                if (ImGui.Checkbox("Earliest only (ETA min) / \u6700\u65e9(ETA\u6700\u5c0f)", ref latestOnly)) { Config.DiscordLatestOnly = latestOnly; SaveConfig(); }
                bool useEmbeds = Config.DiscordUseEmbeds;
                if (ImGui.Checkbox("Use embeds", ref useEmbeds)) { Config.DiscordUseEmbeds = useEmbeds; SaveConfig(); }
                if (ImGui.Button("Test Discord"))
                {
                    try
                    {
                        var snap = _uiSnapshot ?? new SubmarineSnapshot
                        {
                            PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                            Items = new System.Collections.Generic.List<SubmarineRecord>
                            {
                                new() { Name = "Submarine-1", Slot = 1, DurationMinutes = 10, RouteKey = "Point-1 - Point-2" }
                            }
                        };
                        try { Services.EtaFormatter.Enrich(snap); } catch { }
                        if (_discord != null)
                        {
                            _discord.NotifySnapshotAsync(snap, Config.DiscordLatestOnly).GetAwaiter().GetResult();
                            _uiStatus = "Discord sent";
                        }
                        else _uiStatus = "Discord not init";
                    }
                    catch (System.Exception ex) { _uiStatus = $"Discord test failed: {ex.Message}"; }
                }
            }

            // Debug
            if (ImGui.CollapsingHeader("Debug"))
            {
                bool dbg = Config.DebugLogging;
                if (ImGui.Checkbox("Enable debug logging", ref dbg)) { Config.DebugLogging = dbg; SaveConfig(); }
                if (ImGui.Button("Open trace"))
                {
                    try
                    {
                        var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? string.Empty, "xsr_debug.log");
                        if (!string.IsNullOrWhiteSpace(path))
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                    }
                    catch (System.Exception ex) { _uiStatus = $"Open trace failed: {ex.Message}"; }
                }
                ImGui.SameLine();
                if (ImGui.Button("Run self-test"))
                {
                    try { CmdSelfTest(); _uiStatus = "Self-test executed"; }
                    catch (System.Exception ex) { _uiStatus = $"Self-test failed: {ex.Message}"; }
                }
            }

            // Alarm
            if (ImGui.CollapsingHeader("Alarm"))
            {
                if (string.IsNullOrEmpty(_alarmLeadText))
                {
                    try
                    {
                        var ls = (Config.AlarmLeadMinutes ?? new System.Collections.Generic.List<int>()).ToArray();
                        _alarmLeadText = string.Join(",", ls);
                    }
                    catch { _alarmLeadText = "5,0"; }
                }
                var tmp = _alarmLeadText;
                if (ImGui.InputText("Lead minutes (comma)", ref tmp, 64))
                {
                    _alarmLeadText = tmp;
                }
                ImGui.SameLine();
                if (ImGui.Button("Save Alarm"))
                {
                    try
                    {
                        var parts = (tmp ?? string.Empty).Split(new[] { ',', ' ', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
                        var list = new System.Collections.Generic.List<int>(parts.Length);
                        foreach (var p in parts)
                        {
                            if (int.TryParse(p.Trim(), out var v)) list.Add(v);
                        }
                        Config.AlarmLeadMinutes = list;
                        SaveConfig();
                        _uiStatus = "Alarm leads saved";
                    }
                    catch (System.Exception ex) { _uiStatus = $"Alarm save failed: {ex.Message}"; }
                }
            }

            // Notion
            if (ImGui.CollapsingHeader("Notion"))
            {
                bool nEnable = Config.NotionEnabled;
                if (ImGui.Checkbox("Enable", ref nEnable)) { Config.NotionEnabled = nEnable; SaveConfig(); }
                var tok = Config.NotionToken ?? string.Empty;
                if (ImGui.InputText("Integration Token", ref tok, 256)) { Config.NotionToken = tok; SaveConfig(); }
                var db = Config.NotionDatabaseId ?? string.Empty;
                if (ImGui.InputText("Database ID", ref db, 256)) { Config.NotionDatabaseId = db; SaveConfig(); }
                bool nLatest = Config.NotionLatestOnly;
                if (ImGui.Checkbox("Earliest only (ETA min)", ref nLatest)) { Config.NotionLatestOnly = nLatest; SaveConfig(); }

                // Property names
                var pn = Config.NotionPropName ?? "Name";
                if (ImGui.InputText("Prop: Name (title)", ref pn, 64)) { Config.NotionPropName = pn; SaveConfig(); }
                var ps = Config.NotionPropSlot ?? "Slot";
                if (ImGui.InputText("Prop: Slot (number)", ref ps, 64)) { Config.NotionPropSlot = ps; SaveConfig(); }
                var pe = Config.NotionPropEta ?? "ETA";
                if (ImGui.InputText("Prop: ETA (date)", ref pe, 64)) { Config.NotionPropEta = pe; SaveConfig(); }
                var pr = Config.NotionPropRoute ?? "Route";
                if (ImGui.InputText("Prop: Route (rich_text)", ref pr, 64)) { Config.NotionPropRoute = pr; SaveConfig(); }
                var prk = Config.NotionPropRank ?? "Rank";
                if (ImGui.InputText("Prop: Rank (number)", ref prk, 64)) { Config.NotionPropRank = prk; SaveConfig(); }
                var px = Config.NotionPropExtId ?? "ExtId";
                if (ImGui.InputText("Prop: ExtId (rich_text)", ref px, 64)) { Config.NotionPropExtId = px; SaveConfig(); }

                if (ImGui.Button("Test Notion"))
                {
                    try
                    {
                        var snap = _uiSnapshot ?? new SubmarineSnapshot
                        {
                            PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                            Items = new System.Collections.Generic.List<SubmarineRecord>
                            {
                                new() { Name = "Submarine-1", Slot = 1, DurationMinutes = 10, RouteKey = "Point-1 - Point-2", Rank = 10, EtaUnix = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() }
                            }
                        };
                        try { Services.EtaFormatter.Enrich(snap); } catch { }
                        if (_alarm != null)
                        {
                            // reuse scheduler path
                            _alarm.UpdateSnapshot(snap);
                            _uiStatus = "Notion test enqueued";
                        }
                    }
                    catch (System.Exception ex) { _uiStatus = $"Notion test failed: {ex.Message}"; }
                }
            }

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
