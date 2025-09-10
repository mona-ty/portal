using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;
using ImGuiWindowFlags = Dalamud.Bindings.ImGui.ImGuiWindowFlags;
using ImGuiCond = Dalamud.Bindings.ImGui.ImGuiCond;
using ImGuiConfigFlags = Dalamud.Bindings.ImGui.ImGuiConfigFlags;
using XIVSubmarinesReturn.UI;
using Dalamud.Interface.Components;
using Dalamud.Interface;
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
    private string _routeLearnLetters = string.Empty; // e.g., "M>R>O>J>Z"
    private string _filterText = string.Empty;
    private int _sortField = 3; // 0=Name 1=Slot 2=Rank 3=ETA
    private bool _sortAsc = true;
    private XIVSubmarinesReturn.UI.SnapshotTable _snapTable = new XIVSubmarinesReturn.UI.SnapshotTable();    // reveal toggles for masked inputs
    private bool _revealDiscordWebhook;
    private bool _showLegacyUi = false;
    private int _mogshipLastMaps;
    private int _mogshipLastAliases;
    public void Ui_ReloadSnapshot() { try { _uiLastReadUtc = DateTime.MinValue; _uiStatus = "JSON再読込 実行"; } catch { } }
    public void Ui_ImportFromMogship() { try { TryImportFromMogship(); } catch (Exception ex) { _uiStatus = $"Mogship取込失敗: {ex.Message}"; } }
    public void Ui_OpenBridgeFolder() { try { TryOpenFolder(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath())); } catch { } }
    public void Ui_OpenConfigFolder() { try { TryOpenFolder(_pi.ConfigDirectory?.FullName); } catch { } }
    public string Ui_GetUiStatus() => _uiStatus ?? string.Empty;
    public void Ui_SetUiStatus(string s) { _uiStatus = s ?? string.Empty; }
    public string Ui_GetProbeText() => _probeText ?? string.Empty;
    public void Ui_ClearProbeText() { _probeText = string.Empty; }
    public void Ui_DrawSnapshotTable() { try { DrawSnapshotTable2(); } catch { } }
    public void Ui_ReloadAliasIndex()
    {
        try { _sectorResolver?.ReloadAliasIndex(); _uiStatus = "Alias JSON 再読込 完了"; }
        catch (Exception ex) { _uiStatus = $"Alias 再読込 失敗: {ex.Message}"; }
    }
    public int Ui_GetSnapshotCount() { try { return _uiSnapshot?.Items?.Count ?? 0; } catch { return 0; } }
    public void Ui_OpenTrace()
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? string.Empty, "xsr_debug.log");
            if (!string.IsNullOrWhiteSpace(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { }
    }

    // プロファイルセレクタ（簡易）
    public void Ui_DrawProfileSelector()
    {
        try
        {
            var list = Config.Profiles ?? new System.Collections.Generic.List<CharacterProfile>();
            // 表示用に CID=0 のプレースホルダを隠す（他に1件以上ある場合）
            var display = new System.Collections.Generic.List<CharacterProfile>(list.Count);
            foreach (var p0 in list)
            {
                if (p0.ContentId == 0 && list.Count > 1) continue;
                display.Add(p0);
            }

            var names = new System.Collections.Generic.List<string>(Math.Max(1, display.Count));
            foreach (var p in display)
            {
                string disp;
                var hasName = !string.IsNullOrWhiteSpace(p.CharacterName);
                var hasWorld = !string.IsNullOrWhiteSpace(p.WorldName);
                if (hasName && hasWorld) disp = $"{p.CharacterName} @ {p.WorldName}";
                else if (hasName) disp = p.CharacterName;
                else if (hasWorld) disp = p.WorldName;
                else disp = $"CID:0x{p.ContentId:X}";
                names.Add(disp);
            }
            if (names.Count == 0) names.Add("(なし)");

            int curIndex = 0;
            if (Config.ActiveContentId.HasValue)
            {
                var idx = display.FindIndex(x => x.ContentId == Config.ActiveContentId.Value);
                if (idx >= 0) curIndex = idx;
            }

            ImGui.TextUnformatted("プロファイル");
            ImGui.SameLine(180);
            ImGui.PushItemWidth(260);
            if (ImGui.Combo("##xsr_prof", ref curIndex, names.ToArray(), names.Count))
            {
                if (display.Count > 0)
                {
                    var sel = display[Math.Clamp(curIndex, 0, display.Count - 1)];
                    Config.ActiveContentId = sel.ContentId;
                    SaveConfig();
                    try { if (sel.LastSnapshot?.Items != null && sel.LastSnapshot.Items.Count > 0) { _uiSnapshot = sel.LastSnapshot; _uiLastReadUtc = DateTime.UtcNow; _uiStatus = "プロファイル保存データ"; } } catch { }
                }
            }
            ImGui.PopItemWidth();

            // 右側に操作: 追加/現在キャラへ切替/削除
            ImGui.SameLine();
            if (ImGui.SmallButton("現在を選択/追加"))
            {
                try { EnsureActiveProfileFromClient(); } catch { }
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("削除") && display.Count > 0)
            {
                try
                {
                    var sel = display[Math.Clamp(curIndex, 0, display.Count - 1)];
                    list.Remove(sel);
                    Config.Profiles = list;
                    if (Config.ActiveContentId == sel.ContentId)
                        Config.ActiveContentId = (list.Count > 0) ? list[0].ContentId : (ulong?)null;
                    SaveConfig();
                }
                catch { }
            }
            if (list.Count >= 10) { ImGui.SameLine(); ImGui.TextDisabled("(上限10)"); }
        }
        catch { }
    }
    public void Ui_LearnNames() { try { CmdLearnNames(); _uiStatus = "Learn triggered"; } catch (Exception ex) { _uiStatus = $"Learn failed: {ex.Message}"; } }
    // UIからの取得は無効化: 常にメモリ経由で手動取得
    public void Ui_DumpUi() { try { CmdDumpFromMemory(); _uiStatus = "Capture(Memory) triggered"; } catch (Exception ex) { _uiStatus = $"Capture(Memory) failed: {ex.Message}"; } }
    public void Ui_DumpMemory() { try { CmdDumpFromMemory(); _uiStatus = "Capture(Memory) triggered"; } catch (Exception ex) { _uiStatus = $"Capture(Memory) failed: {ex.Message}"; } }
    public void Ui_Probe() { try { _probeText = ProbeToText(); _uiStatus = "Probe done"; } catch (Exception ex) { _uiStatus = $"Probe failed: {ex.Message}"; } }

    // ルート別名（レター）編集UI（ActiveProfile優先）。空文字にすると削除。
    public void Ui_DrawRouteAliasEditor()
    {
        try
        {
            var map = GetRouteNameMap();
            if (map == null) { ImGui.TextDisabled("(別名マップなし)"); return; }

            ImGui.TextUnformatted("別名(レター) 追加/更新");
            ImGui.SameLine(180);
            ImGui.PushItemWidth(80);
            int id = Math.Clamp(_routeEditId, 0, 255);
            if (ImGui.InputInt("Point ID", ref id)) { _routeEditId = id; }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.PushItemWidth(160);
            string alias = _routeEditName ?? string.Empty;
            if (ImGui.InputText("Alias", ref alias, 32)) { _routeEditName = alias; }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            if (ImGui.SmallButton("追加/更新"))
            {
                try
                {
                    int pid = _routeEditId;
                    var txt = (_routeEditName ?? string.Empty).Trim();
                    if (pid >= 1 && pid <= 255 && !string.IsNullOrWhiteSpace(txt))
                    {
                        map[(byte)pid] = txt;
                        SaveConfig();
                        _uiStatus = $"Alias P{pid} = '{txt}'";
                        _routeEditId = 0; _routeEditName = string.Empty;
                    }
                    else
                    {
                        _uiStatus = "ID(1-255)とAliasを入力してください";
                    }
                }
                catch (Exception ex) { _uiStatus = $"Alias更新失敗: {ex.Message}"; }
            }

            // 一覧（編集/削除）
            if (map.Count > 0)
            {
                ImGui.Separator();
                ImGui.TextUnformatted("登録済み（空にすると削除）");
                var keys = map.Keys.ToList();
                keys.Sort((a, b) => a.CompareTo(b));
                foreach (var k in keys)
                {
                    try
                    {
                        ImGui.TextUnformatted($"P{k}");
                        ImGui.SameLine(180);
                        ImGui.PushItemWidth(200);
                        string v = map.TryGetValue(k, out var vv) ? (vv ?? string.Empty) : string.Empty;
                        if (ImGui.InputText($"##alias_{k}", ref v, 32))
                        {
                            v = (v ?? string.Empty).Trim();
                            if (string.IsNullOrEmpty(v)) map.Remove(k);
                            else map[k] = v;
                            SaveConfig();
                        }
                        ImGui.PopItemWidth();
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"削除##{k}")) { map.Remove(k); SaveConfig(); }
                    }
                    catch { }
                }
            }
            else
            {
                ImGui.TextDisabled("（未登録）");
            }
        }
        catch { }
    }

    // Day2: Alarm/Notion/Discord テスト系の補助
    public void Ui_TestGameAlarm()
    {
        try
        {
            var leads = (Config.AlarmLeadMinutes ?? new System.Collections.Generic.List<int>()).ToList();
            int lead = (leads.Count > 0) ? leads.Min() : 0;
            var now = DateTimeOffset.UtcNow;
            var snap = new SubmarineSnapshot
            {
                PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                Items = new System.Collections.Generic.List<SubmarineRecord>
                {
                    new() { Name = "Test Alarm", Slot = 1, Rank = 0, RouteKey = "Point-13 - Point-18", EtaUnix = now.AddMinutes(lead).ToUnixTimeSeconds() }
                }
            };
            try { Services.EtaFormatter.Enrich(snap); } catch { }
            try { _alarm?.UpdateSnapshot(snap); } catch { }
            try { _alarm?.Tick(DateTimeOffset.UtcNow); } catch { }
            _uiStatus = "Game alarm test executed";
        }
        catch (Exception ex) { _uiStatus = $"Game alarm test failed: {ex.Message}"; }
    }

    public void Ui_TestDiscord()
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
            if (_discord != null)
            {
                _discord.NotifySnapshotAsync(snap, Config.DiscordLatestOnly).GetAwaiter().GetResult();
                _uiStatus = "Discord test sent";
            }
            else _uiStatus = "Discord client not ready";
        }
        catch (Exception ex) { _uiStatus = $"Discord test failed: {ex.Message}"; }
    }

    public void Ui_ValidateNotion()
    {
        try
        {
            if (_notion == null) { _uiStatus = "Notion client not ready"; return; }
            var msg = _notion.EnsureDatabasePropsAsync().GetAwaiter().GetResult();
            _uiStatus = msg ?? "Notion validate done";
        }
        catch (Exception ex) { _uiStatus = $"Notion validate failed: {ex.Message}"; }
    }

    public void Ui_TestNotion()
    {
        try
        {
            if (_notion == null) { _uiStatus = "Notion client not ready"; return; }
            var snap = _uiSnapshot ?? new SubmarineSnapshot
            {
                PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                Items = new System.Collections.Generic.List<SubmarineRecord>
                {
                    new() { Name = "Submarine-1", Slot = 1, DurationMinutes = 10, RouteKey = "Point-1 - Point-2", Rank = 10, EtaUnix = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() }
                }
            };
            try { Services.EtaFormatter.Enrich(snap); } catch { }
            _notion.UpsertSnapshotAsync(snap, Config.NotionLatestOnly).GetAwaiter().GetResult();
            _uiStatus = "Notion test enqueued";
        }
        catch (Exception ex) { _uiStatus = $"Notion test failed: {ex.Message}"; }
    }
    private bool _revealNotionToken;
    private bool _revealGcalRefresh;
    private bool _revealGcalSecret;

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
            try { var io = ImGui.GetIO(); io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; } catch { }
            // 初回のみデフォルトサイズを設定（毎回大きくならないよう Appearing → FirstUseEver）
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(760, 520), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(520, 320), new System.Numerics.Vector2(2000, 2000));

            var winFlags = ImGuiWindowFlags.None;
            if (Config.AutoResizeWindow)
                winFlags |= ImGuiWindowFlags.AlwaysAutoResize; // タブ内容に合わせて自動で縦幅を調整

            if (!ImGui.Begin("XIV Submarines Return", ref _showUI, winFlags))
            {
                ImGui.End();
                return;
            }
            try { ImGui.SetWindowFontScale(Math.Clamp(Config.UiFontScale, 0.8f, 1.5f)); } catch { }

            // Tabs（概要 / アラーム / デバッグ / スナップショット(旧)）
            if (ImGui.BeginTabBar("xsr_tabs"))
            {
                // 概要
                if (ImGui.BeginTabItem("概要"))
                {
            try { XIVSubmarinesReturn.UI.OverviewTab.Draw(this); } catch { }
                    ImGui.Separator(); if (!_showLegacyUi) goto __OV_END;
                    ImGui.Separator(); ImGui.TextDisabled("(旧UI)");
                    // 概要: 最小構成（自動取得 + Addon名）
                    bool autoCap = Config.AutoCaptureOnWorkshopOpen;
                    if (ImGui.Checkbox("工房を開いたら自動取得", ref autoCap))
                    {
                        Config.AutoCaptureOnWorkshopOpen = autoCap;
                        SaveConfig();
                    }

                    // Top bar (Reload / Mogship import / Open folders)
                    try
                    {
                        if (Widgets.IconButton(FontAwesomeIcon.Sync, "再読込")) { _uiLastReadUtc = DateTime.MinValue; _uiStatus = "再読込"; }
                        ImGui.SameLine();
                        if (Widgets.IconButton(FontAwesomeIcon.CloudDownloadAlt, "Mogship取込")) { TryImportFromMogship(); }
                        ImGui.SameLine();
                        if (Widgets.IconButton(FontAwesomeIcon.FolderOpen, "Bridgeフォルダ")) { TryOpenFolder(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath())); }
                        ImGui.SameLine();
                        if (Widgets.IconButton(FontAwesomeIcon.Cog, "Configフォルダ")) { TryOpenFolder(_pi.ConfigDirectory?.FullName); }
                        if (!string.IsNullOrEmpty(_uiStatus)) { ImGui.SameLine(); ImGui.TextDisabled(_uiStatus); }
                    }
                    catch { }

                    // Addon name input
                    var addonName = Config.AddonName ?? string.Empty;
                    if (ImGui.InputText("Addon名", ref addonName, 64))
                    {
                        Config.AddonName = addonName;
                        SaveConfig();
                    }

                    ImGui.Separator();
                    // 外観設定（密度/フォント/ETA強調）
                    int dens = Config.UiRowDensity == UiDensity.Compact ? 0 : 1;
                    if (ImGui.RadioButton("Compact", dens == 0)) { Config.UiRowDensity = UiDensity.Compact; SaveConfig(); dens = 0; }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Cozy", dens == 1)) { Config.UiRowDensity = UiDensity.Cozy; SaveConfig(); dens = 1; }
                    var fscale = Config.UiFontScale;
                    if (ImGui.SliderFloat("FontScale", ref fscale, 0.9f, 1.2f)) { Config.UiFontScale = fscale; SaveConfig(); }
                    var accStr = Config.AccentColor ?? "#1E90FF";
                    if (ImGui.InputText("Accent(#RRGGBB)", ref accStr, 16)) { Config.AccentColor = accStr; SaveConfig(); }
                    try
                    {
                        var acc = Theme.ParseColor(Config.AccentColor, new Vector4(0.12f, 0.55f, 0.96f, 1f));
                        ImGui.SameLine(); ImGui.ColorButton("acc_prev", acc);
                    }
                    catch { }
                    int soon = Config.HighlightSoonMins;
                    if (ImGui.SliderInt("ETA強調(分)", ref soon, 0, 60)) { Config.HighlightSoonMins = soon; SaveConfig(); }
                    ImGui.TextDisabled("詳細設定はデバッグタブへ移動しました。");

                    // 手動取得（UIは削除し、メモリ取得のみ残す）
                    if (ImGui.Button("メモリから取得"))
                    {
                        try { CmdDumpFromMemory(); _uiStatus = "メモリから取得を実行しました"; }
                        catch (Exception ex) { _uiStatus = $"メモリからの取得に失敗: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("UIが読めない場合のフォールバック");
                    if (!string.IsNullOrEmpty(_uiStatus)) { ImGui.SameLine(); ImGui.Text(_uiStatus); }

                    ImGui.Separator();
                    // 取得サマリ（スナップショット統合）
                    if (ImGui.Button("再読込")) { _uiLastReadUtc = DateTime.MinValue; }
                    ImGui.SameLine(); ImGui.Text("最新スナップショット");
                    // 表示設定（ルート表示モード）
                    try
                    {
                        int rMode = Config.RouteDisplay switch
                        {
                            RouteDisplayMode.Letters => 0,
                            RouteDisplayMode.ShortIds => 1,
                            RouteDisplayMode.Raw => 2,
                            _ => 0
                        };
                        ImGui.Text("ルート表示"); ImGui.SameLine();
                        if (ImGui.RadioButton("レター", rMode == 0)) { Config.RouteDisplay = RouteDisplayMode.Letters; SaveConfig(); rMode = 0; }
                        ImGui.SameLine();
                        if (ImGui.RadioButton("P番号", rMode == 1)) { Config.RouteDisplay = RouteDisplayMode.ShortIds; SaveConfig(); rMode = 1; }
                        ImGui.SameLine();
                        if (ImGui.RadioButton("原文", rMode == 2)) { Config.RouteDisplay = RouteDisplayMode.Raw; SaveConfig(); rMode = 2; }
                        // Mapヒント + Alias再読込
                        var mapHint = Config.SectorMapHint ?? string.Empty;
                        if (ImGui.InputText("Mapヒント", ref mapHint, 64)) { Config.SectorMapHint = mapHint; SaveConfig(); }
                        ImGui.SameLine();
                        if (ImGui.Button("Alias JSON再読込"))
                        {
                            try { _sectorResolver?.ReloadAliasIndex(); _uiStatus = "Alias reloaded"; }
                            catch (Exception ex) { _uiStatus = $"Alias reload failed: {ex.Message}"; }
                        }
                    }
                    catch { }

                    try { DrawSnapshotTable2(); } catch { }

#if XSR_FEAT_GCAL
                    // Google Calendar (soft-disabled)
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
                        if (Widgets.MaskedInput("RefreshToken", ref rt, 256, ref _revealGcalRefresh)) { Config.GoogleRefreshToken = rt; SaveConfig(); }
                        var cid = Config.GoogleClientId ?? string.Empty;
                        if (ImGui.InputText("ClientId", ref cid, 256)) { Config.GoogleClientId = cid; SaveConfig(); }
                        var cs = Config.GoogleClientSecret ?? string.Empty;
                        if (Widgets.MaskedInput("ClientSecret", ref cs, 256, ref _revealGcalSecret)) { Config.GoogleClientSecret = cs; SaveConfig(); }
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
#endif

                    __OV_END: ImGui.EndTabItem();
                }

                // アラーム
                if (ImGui.BeginTabItem("アラーム"))
                {
                    try { XIVSubmarinesReturn.UI.AlarmTab.Draw(this); } catch { }
                    ImGui.Separator(); if (!_showLegacyUi) goto __AL_END;
                    ImGui.Separator(); ImGui.TextDisabled("(旧UI)");
                    // Discord
                    if (ImGui.CollapsingHeader("Discord"))
                    {
                        bool dEnable = Config.DiscordEnabled;
                        if (ImGui.Checkbox("有効", ref dEnable)) { Config.DiscordEnabled = dEnable; SaveConfig(); }
                        var wh = Config.DiscordWebhookUrl ?? string.Empty;
                        if (Widgets.MaskedInput("Webhook URL", ref wh, 512, ref _revealDiscordWebhook)) { Config.DiscordWebhookUrl = wh; SaveConfig(); }
                        bool latestOnly = Config.DiscordLatestOnly;
                        if (ImGui.Checkbox("最早(ETA最小)のみ", ref latestOnly)) { Config.DiscordLatestOnly = latestOnly; SaveConfig(); }
                        bool useEmbeds = Config.DiscordUseEmbeds;
                        if (ImGui.Checkbox("埋め込み(リッチ表示)を使用", ref useEmbeds)) { Config.DiscordUseEmbeds = useEmbeds; SaveConfig(); }
                        if (ImGui.Button("Discordテスト送信"))
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

                    // ゲーム内アラーム
                    if (ImGui.CollapsingHeader("ゲーム内アラーム"))
                    {
                        bool aEnable = Config.GameAlarmEnabled;
                        if (ImGui.Checkbox("有効", ref aEnable)) { Config.GameAlarmEnabled = aEnable; SaveConfig(); }
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
                        if (ImGui.InputText("リード分(カンマ区切り)", ref tmp, 64))
                        {
                            _alarmLeadText = tmp;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("保存"))
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
                        if (ImGui.Checkbox("有効", ref nEnable)) { Config.NotionEnabled = nEnable; SaveConfig(); }
                        var tok = Config.NotionToken ?? string.Empty;
                        if (Widgets.MaskedInput("Integration Token", ref tok, 256, ref _revealNotionToken)) { Config.NotionToken = tok; SaveConfig(); }
                        var db = Config.NotionDatabaseId ?? string.Empty;
                        if (ImGui.InputText("Database ID", ref db, 256)) { Config.NotionDatabaseId = db; SaveConfig(); }
                        bool nLatest = Config.NotionLatestOnly;
                        if (ImGui.Checkbox("最早(ETA最小)のみ", ref nLatest)) { Config.NotionLatestOnly = nLatest; SaveConfig(); }

                        // Upsert Key Mode
                        var modeVal = (int)Config.NotionKeyMode;
                        ImGui.Text("Upsertキー方式");
                        if (ImGui.RadioButton("スロット単位", modeVal == 0)) { Config.NotionKeyMode = NotionKeyMode.PerSlot; SaveConfig(); modeVal = 0; }
                        ImGui.SameLine();
                        if (ImGui.RadioButton("スロット+ルート", modeVal == 1)) { Config.NotionKeyMode = NotionKeyMode.PerSlotRoute; SaveConfig(); modeVal = 1; }
                        ImGui.SameLine();
                        if (ImGui.RadioButton("航海毎(レガシー)", modeVal == 2)) { Config.NotionKeyMode = NotionKeyMode.PerVoyage; SaveConfig(); modeVal = 2; }

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
                        var prem = Config.NotionPropRemaining ?? "Remaining";
                        if (ImGui.InputText("Prop: Remaining (rich_text)", ref prem, 64)) { Config.NotionPropRemaining = prem; SaveConfig(); }
                        var pw = Config.NotionPropWorld ?? "World";
                        if (ImGui.InputText("Prop: World (rich_text)", ref pw, 64)) { Config.NotionPropWorld = pw; SaveConfig(); }
                        var pc = Config.NotionPropCharacter ?? "Character";
                        if (ImGui.InputText("Prop: Character (rich_text)", ref pc, 64)) { Config.NotionPropCharacter = pc; SaveConfig(); }
                        var pfc = Config.NotionPropFC ?? "FC";
                        if (ImGui.InputText("Prop: FC (rich_text)", ref pfc, 64)) { Config.NotionPropFC = pfc; SaveConfig(); }

                        if (ImGui.Button("プロパティ検証"))
                        {
                            try
                            {
                                if (_notion != null)
                                {
                                    var msg = _notion.EnsureDatabasePropsAsync().GetAwaiter().GetResult();
                                    _uiStatus = msg;
                                }
                                else _uiStatus = "Notion client not ready";
                            }
                            catch (System.Exception ex) { _uiStatus = $"Notion validate failed: {ex.Message}"; }
                        }

                        if (ImGui.Button("Notionテスト送信"))
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

                    __AL_END: ImGui.EndTabItem();
                }

                // デバッグ
                if (ImGui.BeginTabItem("デバッグ"))
                {
                    // デバッグ設定
                    try { XIVSubmarinesReturn.UI.DebugTab.Draw(this); } catch { }
                    ImGui.Separator(); if (!_showLegacyUi) goto __DBG_END;
                    ImGui.Separator(); ImGui.TextDisabled("(旧UI)");
                    if (ImGui.CollapsingHeader("デバッグ"))
                    {
                        bool dbg = Config.DebugLogging;
                        if (ImGui.Checkbox("デバッグログを有効化", ref dbg)) { Config.DebugLogging = dbg; SaveConfig(); }
                        if (ImGui.Button("トレースを開く"))
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
                        if (ImGui.Button("セルフテスト実行"))
                        {
                            try { CmdSelfTest(); _uiStatus = "Self-test executed"; }
                            catch (System.Exception ex) { _uiStatus = $"Self-test failed: {ex.Message}"; }
                        }
                    }

                    // ツール群（1行=ボタン+説明）
                    // UIからの取得ボタンは非表示（機能無効化）

                    // UIからの取得ボタンは非表示（機能無効化）

                    if (ImGui.Button("メモリから取得"))
                    {
                        try { CmdDumpFromMemory(); _uiStatus = "Capture(Memory) triggered"; }
                        catch (Exception ex) { _uiStatus = $"Capture(Memory) failed: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("UIが読めない場合のフォールバック");

                    if (ImGui.Button("アドオン探索"))
                    {
                        try { _probeText = ProbeToText(); _uiStatus = "Probe done"; }
                        catch (Exception ex) { _uiStatus = $"Probe failed: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("候補アドオンの存在/表示状態を確認します");

                    if (ImGui.Button("フォルダを開く"))
                    {
                        try { OnCmdOpen("/subopen", string.Empty); _uiStatus = "Folder opened"; }
                        catch (Exception ex) { _uiStatus = $"Open failed: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("出力フォルダ(bridge)を開きます");
                    if (!string.IsNullOrEmpty(_uiStatus)) { ImGui.SameLine(); ImGui.Text(_uiStatus); }

                    ImGui.Separator();
                    if (!string.IsNullOrEmpty(_probeText) && ImGui.Button("結果をクリア")) { _probeText = string.Empty; }
                    if (!string.IsNullOrEmpty(_probeText))
                    {
                        ImGui.Separator();
                        ImGui.Text("探索結果:");
                        ImGui.BeginChild("probe", new Vector2(480, 120), true, ImGuiWindowFlags.None);
                        ImGui.TextWrapped(_probeText);
                        ImGui.EndChild();
                    }

                    // Advanced settings (moved from Overview)
                    ImGui.Separator();
                    if (ImGui.CollapsingHeader("詳細設定"))
                    {
                        bool memFb = Config.UseMemoryFallback;
                        if (ImGui.Checkbox("UI取得に失敗したらメモリから取得", ref memFb))
                        {
                            Config.UseMemoryFallback = memFb;
                            SaveConfig();
                        }

                        bool selExt = Config.UseSelectStringExtraction;
                        if (ImGui.Checkbox("SelectString抽出を使用", ref selExt))
                        {
                            Config.UseSelectStringExtraction = selExt;
                            SaveConfig();
                        }
                        bool selDet = Config.UseSelectStringDetailExtraction;
                        if (ImGui.Checkbox("SelectString詳細抽出を使用", ref selDet))
                        {
                            Config.UseSelectStringDetailExtraction = selDet;
                            SaveConfig();
                        }
                        bool aggr = Config.AggressiveFallback;
                        if (ImGui.Checkbox("テキスト行が少ない場合に強力フォールバック", ref aggr))
                        {
                            Config.AggressiveFallback = aggr;
                            SaveConfig();
                        }
                        bool acceptDefaults = Config.AcceptDefaultNamesInMemory;
                        if (ImGui.Checkbox("メモリ名が既定(Submarine-<n>)でも許可", ref acceptDefaults))
                        {
                            Config.AcceptDefaultNamesInMemory = acceptDefaults;
                            SaveConfig();
                        }

                        ImGui.Separator();
                        ImGui.Text("スロット別名 (1..4)");
                        for (int i = 0; i < 4; i++)
                        {
                            var aliases = GetSlotAliases();
                            var val = aliases != null && i < aliases.Length ? (aliases[i] ?? string.Empty) : string.Empty;
                            var tmp = val;
                            ImGui.PushID(i);
                            if (ImGui.InputText("##alias", ref tmp, 64))
                            {
                                try
                                {
                                    var prof = GetActiveProfile();
                                    if (prof != null)
                                    {
                                        if (prof.SlotAliases == null || prof.SlotAliases.Length < 4) prof.SlotAliases = new string[4];
                                        prof.SlotAliases[i] = tmp;
                                    }
                                    if (Config.SlotAliases == null || Config.SlotAliases.Length < 4) Config.SlotAliases = new string[4];
                                    Config.SlotAliases[i] = tmp;
                                    SaveConfig();
                                }
                                catch { }
                            }
                            ImGui.SameLine();
                            ImGui.Text($"Slot {i + 1}");
                            ImGui.PopID();
                        }

                        // ルート名エディタ（任意）
                        ImGui.Separator();
                        ImGui.Text("ルート名 (ID -> 表示名)");
                        ImGui.InputInt("ID", ref _routeEditId);
                        ImGui.InputText("名前", ref _routeEditName, 64);
                        if (ImGui.Button("追加/更新"))
                        {
                            try
                            {
                                if (_routeEditId >= 0 && _routeEditId <= 255)
                                {
                                    var map = GetRouteNameMap();
                                    if (map != null)
                                    {
                                        map[(byte)_routeEditId] = _routeEditName ?? string.Empty;
                                        // 互換: グローバルにも反映
                                        if (Config.RouteNames == null) Config.RouteNames = new System.Collections.Generic.Dictionary<byte, string>();
                                        Config.RouteNames[(byte)_routeEditId] = _routeEditName ?? string.Empty;
                                    }
                                    SaveConfig();
                                    _uiStatus = "Route name saved";
                                }
                            }
                            catch (Exception ex) { _uiStatus = $"Route save failed: {ex.Message}"; }
                        }
                        {
                            var map = GetRouteNameMap();
                            if (map != null && map.Count > 0)
                        {
                            if (ImGui.BeginTable("routes", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("ID");
                                ImGui.TableSetupColumn("名前");
                                ImGui.TableHeadersRow();
                                foreach (var kv in map.OrderBy(k => k.Key))
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableSetColumnIndex(0); ImGui.Text(kv.Key.ToString());
                                    ImGui.TableSetColumnIndex(1); ImGui.Text(kv.Value ?? string.Empty);
                                }
                                ImGui.EndTable();
                            }
                        }
                        }

                        // スナップショットからレター表記を学習
                        ImGui.Separator();
                        ImGui.Text("スナップショットからレター表記を学習 (例: M>R>O>J>Z)");
                        ImGui.InputText("レター並び", ref _routeLearnLetters, 64);
                        ImGui.SameLine();
                        if (ImGui.Button("対応付けを保存"))
                        {
                            try
                            {
                                var snap = _uiSnapshot;
                                var first = snap?.Items?.FirstOrDefault(x =>
                                    (x.Extra != null && x.Extra.TryGetValue("RouteShort", out var rs) && !string.IsNullOrWhiteSpace(rs))
                                    || !string.IsNullOrWhiteSpace(x.RouteKey));
                                if (first == null) { _uiStatus = "スナップショットに航路がありません"; }
                                else
                                {
                                    // P番号列を取得
                                    var pText = string.Empty;
                                    if (first.Extra != null && first.Extra.TryGetValue("RouteShort", out var rs0) && !string.IsNullOrWhiteSpace(rs0)) pText = rs0!;
                                    else pText = first.RouteKey ?? string.Empty;
                                    var nums = new System.Collections.Generic.List<int>();
                                    try
                                    {
                                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(pText ?? string.Empty, @"P?(\d+)") )
                                        {
                                            if (int.TryParse(m.Groups[1].Value, out var v)) nums.Add(v);
                                        }
                                    } catch { }

                                    // レター列を取得
                                    var letters = (_routeLearnLetters ?? string.Empty)
                                        .Split(new[]{'>','-',' ','/','\t'}, System.StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .ToList();

                                    if (nums.Count == 0 || letters.Count == 0 || nums.Count != letters.Count)
                                    {
                                        _uiStatus = $"学習失敗: P数={nums.Count} レター数={letters.Count} (一致している必要があります)";
                                    }
                                    else
                                    {
                                        var map = GetRouteNameMap();
                                        if (map == null)
                                        {
                                            try
                                            {
                                                var prof = GetActiveProfile();
                                                if (prof != null) prof.RouteNames = new System.Collections.Generic.Dictionary<byte, string>();
                                                if (Config.RouteNames == null) Config.RouteNames = new System.Collections.Generic.Dictionary<byte, string>();
                                                map = prof?.RouteNames ?? Config.RouteNames;
                                            }
                                            catch { }
                                        }
                                        for (int i = 0; i < nums.Count; i++)
                                        {
                                            var id = nums[i];
                                            var nm = letters[i];
                                            if (id >= 0 && id <= 255 && !string.IsNullOrWhiteSpace(nm))
                                            {
                                                map[(byte)id] = nm;
                                                // 互換: グローバルにも反映
                                                if (Config.RouteNames == null) Config.RouteNames = new System.Collections.Generic.Dictionary<byte, string>();
                                                Config.RouteNames[(byte)id] = nm;
                                            }
                                        }
                                        SaveConfig();
                                        _uiStatus = "レター対応を保存しました";
                                    }
                                }
                            }
                            catch (System.Exception ex) { _uiStatus = $"学習エラー: {ex.Message}"; }
                        }
                    }

                    __DBG_END: ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            // (old Overview block removed)

            // Google Calendar (soft-disabled) — stays in Overview
#if XSR_FEAT_GCAL
            if (false && ImGui.CollapsingHeader("Google Calendar"))
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
                if (Widgets.MaskedInput("RefreshToken", ref rt, 256, ref _revealGcalRefresh)) { Config.GoogleRefreshToken = rt; SaveConfig(); }
                var cid = Config.GoogleClientId ?? string.Empty;
                if (ImGui.InputText("ClientId", ref cid, 256)) { Config.GoogleClientId = cid; SaveConfig(); }
                var cs = Config.GoogleClientSecret ?? string.Empty;
                if (Widgets.MaskedInput("ClientSecret", ref cs, 256, ref _revealGcalSecret)) { Config.GoogleClientSecret = cs; SaveConfig(); }
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
#endif

            // Discord (moved under Alarm tab)
            if (false && ImGui.CollapsingHeader("Discord"))
            {
                bool dEnable = Config.DiscordEnabled;
                if (ImGui.Checkbox("Enable", ref dEnable)) { Config.DiscordEnabled = dEnable; SaveConfig(); }
                var wh = Config.DiscordWebhookUrl ?? string.Empty;
                if (Widgets.MaskedInput("Webhook URL", ref wh, 512, ref _revealDiscordWebhook)) { Config.DiscordWebhookUrl = wh; SaveConfig(); }
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

            // Debug (own tab)
            if (false && ImGui.CollapsingHeader("Debug"))
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

            // Alarm (GameAlarm) under Alarm tab
            if (false && ImGui.CollapsingHeader("Alarm"))
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

            // Notion under Alarm tab (Notion upsert/validate lives here)
            if (false && ImGui.CollapsingHeader("Notion"))
            {
                bool nEnable = Config.NotionEnabled;
                if (ImGui.Checkbox("Enable", ref nEnable)) { Config.NotionEnabled = nEnable; SaveConfig(); }
                var tok = Config.NotionToken ?? string.Empty;
                if (Widgets.MaskedInput("Integration Token", ref tok, 256, ref _revealNotionToken)) { Config.NotionToken = tok; SaveConfig(); }
                var db = Config.NotionDatabaseId ?? string.Empty;
                if (ImGui.InputText("Database ID", ref db, 256)) { Config.NotionDatabaseId = db; SaveConfig(); }
                bool nLatest = Config.NotionLatestOnly;
                if (ImGui.Checkbox("Earliest only (ETA min)", ref nLatest)) { Config.NotionLatestOnly = nLatest; SaveConfig(); }

                // Upsert Key Mode
                var modeVal = (int)Config.NotionKeyMode;
                ImGui.Text("Upsert key mode");
                if (ImGui.RadioButton("Per Slot", modeVal == 0)) { Config.NotionKeyMode = NotionKeyMode.PerSlot; SaveConfig(); modeVal = 0; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Per Slot + Route", modeVal == 1)) { Config.NotionKeyMode = NotionKeyMode.PerSlotRoute; SaveConfig(); modeVal = 1; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Per Voyage (legacy)", modeVal == 2)) { Config.NotionKeyMode = NotionKeyMode.PerVoyage; SaveConfig(); modeVal = 2; }

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
                var prem = Config.NotionPropRemaining ?? "Remaining";
                if (ImGui.InputText("Prop: Remaining (rich_text)", ref prem, 64)) { Config.NotionPropRemaining = prem; SaveConfig(); }
                var pw = Config.NotionPropWorld ?? "World";
                if (ImGui.InputText("Prop: World (rich_text)", ref pw, 64)) { Config.NotionPropWorld = pw; SaveConfig(); }
                var pc = Config.NotionPropCharacter ?? "Character";
                if (ImGui.InputText("Prop: Character (rich_text)", ref pc, 64)) { Config.NotionPropCharacter = pc; SaveConfig(); }
                var pfc = Config.NotionPropFC ?? "FC";
                if (ImGui.InputText("Prop: FC (rich_text)", ref pfc, 64)) { Config.NotionPropFC = pfc; SaveConfig(); }

                if (ImGui.Button("Validate properties"))
                {
                    try
                    {
                        if (_notion != null)
                        {
                            var msg = _notion.EnsureDatabasePropsAsync().GetAwaiter().GetResult();
                            _uiStatus = msg;
                        }
                        else _uiStatus = "Notion client not ready";
                    }
                    catch (System.Exception ex) { _uiStatus = $"Notion validate failed: {ex.Message}"; }
                }

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

            // (old Debug tools block removed)

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
                        _uiStatus = $"JSON再読込 完了 {DateTime.Now:HH:mm:ss}";
                    }
                    catch (Exception ex)
                    {
                        _uiStatus = $"Read json failed: {ex.Message}";
                    }
                }
            }

            // Fallback: アクティブプロファイルの保存スナップショット
            if (_uiSnapshot == null || _uiSnapshot.Items == null || _uiSnapshot.Items.Count == 0)
            {
                try
                {
                    var p = GetActiveProfile();
                    if (p?.LastSnapshot?.Items != null && p.LastSnapshot.Items.Count > 0)
                    {
                        _uiSnapshot = p.LastSnapshot;
                        _uiLastReadUtc = DateTime.UtcNow;
                        if (string.IsNullOrEmpty(_uiStatus)) _uiStatus = "プロファイル保存データを表示中";
                    }
                }
                catch { }
            }

            if (_uiSnapshot?.Items == null || _uiSnapshot.Items.Count == 0)
            {
                ImGui.TextDisabled("データがありません。取得を実行してください。");
                return;
            }

            // 列: スロット / 名前 / ランク / ETA/残り / ルート（5列に統合して右端の余計な列を抑制）
            if (ImGui.BeginTable("subs", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("スロット");
                ImGui.TableSetupColumn("名前");
                ImGui.TableSetupColumn("ランク");
                ImGui.TableSetupColumn("ETA/残り");
                ImGui.TableSetupColumn("ルート");
                ImGui.TableHeadersRow();

                foreach (var it in _uiSnapshot.Items)
                {
                    bool changed = IsChanged(it);
                    ImGui.TableNextRow();
                    // Slot
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(it.Slot.HasValue ? $"S{it.Slot.Value}" : "");
                    // Name
                    ImGui.TableSetColumnIndex(1);
                    if (changed) { ImGui.Text($"* {it.Name}"); }
                    else ImGui.Text(it.Name ?? string.Empty);
                    // Rank
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(it.Rank?.ToString() ?? "");
                    // ETA/残り（1列に統合）
                    ImGui.TableSetColumnIndex(3);
                    try
                    {
                        var etaLoc = (it.Extra != null && it.Extra.TryGetValue("EtaLocal", out var t)) ? t : string.Empty;
                        var rem = (it.Extra != null && it.Extra.TryGetValue("RemainingText", out var r)) ? r : string.Empty;
                        var txt = string.IsNullOrWhiteSpace(rem) ? (etaLoc ?? string.Empty) : $"{etaLoc} / {rem}";
                        bool highlight = false;
                        try
                        {
                            int minsLeft = int.MaxValue;
                            if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                            {
                                var eta = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value);
                                minsLeft = (int)Math.Round((eta - DateTimeOffset.Now).TotalMinutes);
                            }
                            else if (!string.IsNullOrWhiteSpace(rem))
                            {
                                var m = System.Text.RegularExpressions.Regex.Match(rem, @"(?:(?<h>\d+)\s*時間)?\s*(?<m>\d+)\s*分");
                                if (m.Success)
                                {
                                    int h = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value) : 0;
                                    int mm = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
                                    minsLeft = Math.Max(0, h * 60 + mm);
                                }
                            }
                            if (minsLeft <= Config.HighlightSoonMins) highlight = true;
                        }
                        catch { }
                        if (highlight)
                        {
                            var acc = Theme.ParseColor(Config.AccentColor, new Vector4(0.12f, 0.55f, 0.96f, 1f));
                            ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, acc);
                            ImGui.Text(txt ?? string.Empty);
                            ImGui.PopStyleColor();
                        }
                        else ImGui.Text(txt ?? string.Empty);
                    }
                    catch { ImGui.Text(""); }

                    // Route -> 表示モードに応じて表示（レター優先）
                    ImGui.TableSetColumnIndex(4);
                    try
                    {
                        var show = BuildRouteDisplay(it);
                        if (ImGui.Selectable(show ?? string.Empty, false))
                        {
                            try { ImGui.SetClipboardText(show ?? string.Empty); } catch { }
                            _uiStatus = "ルートをコピーしました";
                        }
                    }
                    catch { ImGui.Text(it.RouteKey ?? string.Empty); }
                }

                ImGui.EndTable();
            }
        }
        catch (Exception ex)
        {
            _uiStatus = $"Table error: {ex.Message}";
        }
    }

    // New table drawer (encapsulated SnapshotTable)
    private void DrawSnapshotTable2()
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

            // Fallback: アクティブプロファイルの保存スナップショット
            if (_uiSnapshot == null || _uiSnapshot.Items == null || _uiSnapshot.Items.Count == 0)
            {
                try
                {
                    var p = GetActiveProfile();
                    if (p?.LastSnapshot?.Items != null && p.LastSnapshot.Items.Count > 0)
                    {
                        _uiSnapshot = p.LastSnapshot;
                        _uiLastReadUtc = DateTime.UtcNow;
                        if (string.IsNullOrEmpty(_uiStatus)) _uiStatus = "プロファイル保存データを表示中";
                    }
                }
                catch { }
            }

            ImGui.Text($"スナップショット: {( _uiSnapshot?.Items?.Count ?? 0)} 件");
            if (_uiSnapshot?.Items == null || _uiSnapshot.Items.Count == 0)
            {
                ImGui.TextDisabled("データがありません。取得を実行してください。");
                return;
            }

            _snapTable.Draw(_uiSnapshot.Items,
                Config,
                (id) =>
                {
                    try { return _sectorResolver?.GetAliasForSector((uint)id, Config.SectorMapHint); } catch { return null; }
                },
                (msg) => { _uiStatus = msg; },
                () => { try { SaveConfig(); } catch { } }
            );
        }
        catch (Exception ex)
        {
            _uiStatus = $"Table error: {ex.Message}";
        }
    }

    private string BuildRouteDisplay(SubmarineRecord it)
    {
        try
        {
            var baseRoute = (it.Extra != null && it.Extra.TryGetValue("RouteShort", out var rs)) ? rs : it.RouteKey;
            if (string.IsNullOrWhiteSpace(baseRoute)) return string.Empty;

            // Raw は原文（RouteKey）を優先して返す
            if (Config.RouteDisplay == RouteDisplayMode.Raw)
                return it.RouteKey ?? baseRoute ?? string.Empty;

            // P番号抽出
            var nums = new System.Collections.Generic.List<int>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(baseRoute ?? string.Empty, @"P?(\d+)"))
            {
                if (int.TryParse(m.Groups[1].Value, out var v)) nums.Add(v);
            }
            if (nums.Count == 0) return baseRoute ?? string.Empty;

            switch (Config.RouteDisplay)
            {
                case RouteDisplayMode.ShortIds:
                    return string.Join('>', nums.Select(n => $"P{n}"));
                case RouteDisplayMode.Letters:
                default:
                    // Resolver優先→学習（RouteNames）→P番号
                    var parts = new System.Collections.Generic.List<string>(nums.Count);
                    var hint = Config.SectorMapHint;
                    var rmap = GetRouteNameMap();
                    foreach (var n in nums)
                    {
                        string? letter = null;
                        try { letter = _sectorResolver?.GetAliasForSector((uint)n, hint); } catch { }
                        if (string.IsNullOrWhiteSpace(letter) && rmap != null && rmap.TryGetValue((byte)n, out var nm) && !string.IsNullOrWhiteSpace(nm))
                            letter = nm;
                        parts.Add(string.IsNullOrWhiteSpace(letter) ? $"P{n}" : letter!);
                    }
                    var text = string.Join('>', parts);
                    // 全てP番号（未学習）なら、原文のほうが分かりやすいので原文を優先
                    return parts.All(p => p.StartsWith("P")) ? (baseRoute ?? text) : text;
            }
        }
        catch { return it.RouteKey ?? string.Empty; }
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

    private int CompareItems(SubmarineRecord? a, SubmarineRecord? b, int field, bool asc)
    {
        try
        {
            int s = 0;
            switch (field)
            {
                case 0: s = string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase); break;
                case 1: s = Nullable.Compare(a?.Slot, b?.Slot); break;
                case 2: s = Nullable.Compare(a?.Rank, b?.Rank); break;
                case 3:
                    long ea = a?.EtaUnix ?? long.MinValue;
                    long eb = b?.EtaUnix ?? long.MinValue;
                    s = ea.CompareTo(eb);
                    break;
            }
            return asc ? s : -s;
        }
        catch { return 0; }
    }

    private void TryImportFromMogship()
    {
        try
        {
            var aliasPath = System.IO.Path.Combine(_pi.ConfigDirectory?.FullName ?? string.Empty, "AliasIndex.json");
            var importer = new XIVSubmarinesReturn.Sectors.MogshipImporter(new System.Net.Http.HttpClient(), _log);
            var t = importer.ImportAsync(aliasPath); t.Wait();
            var maps = 0; var aliases = 0;
            try { (maps, aliases) = t.Result; } catch { }
            _mogshipLastMaps = maps; _mogshipLastAliases = aliases;
            _sectorResolver?.ReloadAliasIndex();
            _uiStatus = $"Mogship取り込み: { _mogshipLastAliases } aliases / { _mogshipLastMaps } maps";
        }
        catch (Exception ex) { _uiStatus = $"取込失敗: {ex.Message}"; }
    }

    private void TryOpenFolder(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!System.IO.Directory.Exists(path)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });
        }
        catch { }
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

