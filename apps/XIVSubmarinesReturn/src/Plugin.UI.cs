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
    private string _routeLearnLetters = string.Empty; // e.g., "M>R>O>J>Z"

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

            // Tabs（概要 / アラーム / デバッグ / スナップショット(旧)）
            if (ImGui.BeginTabBar("xsr_tabs"))
            {
                // 概要
                if (ImGui.BeginTabItem("概要"))
                {
                    // 概要: 最小構成（自動取得 + Addon名）
                    bool autoCap = Config.AutoCaptureOnWorkshopOpen;
                    if (ImGui.Checkbox("工房を開いたら自動取得", ref autoCap))
                    {
                        Config.AutoCaptureOnWorkshopOpen = autoCap;
                        SaveConfig();
                    }

                    // Addon name input
                    var addonName = Config.AddonName ?? string.Empty;
                    if (ImGui.InputText("Addon名", ref addonName, 64))
                    {
                        Config.AddonName = addonName;
                        SaveConfig();
                    }

                    ImGui.Separator();
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
                    }
                    catch { }

                    try { DrawSnapshotTable(); } catch { }

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
#endif

                    ImGui.EndTabItem();
                }

                // アラーム
                if (ImGui.BeginTabItem("アラーム"))
                {
                    // Discord
                    if (ImGui.CollapsingHeader("Discord"))
                    {
                        bool dEnable = Config.DiscordEnabled;
                        if (ImGui.Checkbox("有効", ref dEnable)) { Config.DiscordEnabled = dEnable; SaveConfig(); }
                        var wh = Config.DiscordWebhookUrl ?? string.Empty;
                        if (ImGui.InputText("Webhook URL", ref wh, 512)) { Config.DiscordWebhookUrl = wh; SaveConfig(); }
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
                        if (ImGui.InputText("Integration Token", ref tok, 256)) { Config.NotionToken = tok; SaveConfig(); }
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

                    ImGui.EndTabItem();
                }

                // デバッグ
                if (ImGui.BeginTabItem("デバッグ"))
                {
                    // デバッグ設定
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
                    if (ImGui.Button("UIから名称学習"))
                    {
                        try { CmdLearnNames(); _uiStatus = "Learn triggered"; }
                        catch (Exception ex) { _uiStatus = $"Learn failed: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("一覧の名前をUIから学習し補完します");

                    if (ImGui.Button("UIから取得"))
                    {
                        try { OnCmdDump("/subdump", string.Empty); _uiStatus = "Capture(UI) triggered"; }
                        catch (Exception ex) { _uiStatus = $"Capture(UI) failed: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("ゲーム内UIから現在の一覧を取得します");

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
                                ImGui.TableSetupColumn("名前");
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
                                        if (Config.RouteNames == null) Config.RouteNames = new System.Collections.Generic.Dictionary<byte,string>();
                                        for (int i = 0; i < nums.Count; i++)
                                        {
                                            var id = nums[i];
                                            var nm = letters[i];
                                            if (id >= 0 && id <= 255 && !string.IsNullOrWhiteSpace(nm))
                                                Config.RouteNames[(byte)id] = nm;
                                        }
                                        SaveConfig();
                                        _uiStatus = "レター対応を保存しました";
                                    }
                                }
                            }
                            catch (System.Exception ex) { _uiStatus = $"学習エラー: {ex.Message}"; }
                        }
                    }

                    ImGui.EndTabItem();
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
#endif

            // Discord (moved under Alarm tab)
            if (false && ImGui.CollapsingHeader("Discord"))
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
                if (ImGui.InputText("Integration Token", ref tok, 256)) { Config.NotionToken = tok; SaveConfig(); }
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
                    }
                    catch (Exception ex)
                    {
                        _uiStatus = $"Read json failed: {ex.Message}";
                    }
                }
            }

            ImGui.Text($"スナップショット: {( _uiSnapshot?.Items?.Count ?? 0)} 件");
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
                        ImGui.Text(txt ?? string.Empty);
                    }
                    catch { ImGui.Text(""); }

                    // Route -> 表示モードに応じて表示（レター優先）
                    ImGui.TableSetColumnIndex(4);
                    try
                    {
                        var show = BuildRouteDisplay(it);
                        ImGui.Text(show ?? string.Empty);
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
                    // 学習済みレター（RouteNames）で置換。足りない箇所はP番号で補完
                    var parts = new System.Collections.Generic.List<string>(nums.Count);
                    foreach (var n in nums)
                    {
                        if (Config.RouteNames != null && Config.RouteNames.TryGetValue((byte)n, out var nm) && !string.IsNullOrWhiteSpace(nm))
                            parts.Add(nm);
                        else
                            parts.Add($"P{n}");
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
