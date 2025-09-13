using System;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiWindowFlags = Dalamud.Bindings.ImGui.ImGuiWindowFlags;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;
using ImGuiTableColumnFlags = Dalamud.Bindings.ImGui.ImGuiTableColumnFlags;
using ImGuiCond = Dalamud.Bindings.ImGui.ImGuiCond;

namespace XIVSubmarinesReturn.UI
{
    internal static class DebugTab
    {
        internal static void Draw(Plugin p)
        {
            using var _ = Theme.UseDensity(p.Config.UiRowDensity);
            try { ImGui.SetWindowFontScale(Math.Clamp(p.Config.UiFontScale, 0.9f, 1.3f)); } catch { }

            // 2カラムレイアウト（固定幅）で横伸びを抑制
            const float ColWidth = 360f;
            var tblFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.NoHostExtendX;
            if (ImGui.BeginTable("dbg_layout", 2, tblFlags))
            {
                ImGui.TableSetupColumn("left", ImGuiTableColumnFlags.WidthFixed, ColWidth);
                ImGui.TableSetupColumn("right", ImGuiTableColumnFlags.WidthFixed, ColWidth);

                // 左カラム: プロファイル/デバッグ
                ImGui.TableNextColumn();
                ImGui.BeginChild("dbg_col_left", new Vector2(ColWidth, 420f), true, ImGuiWindowFlags.None);
                using (Widgets.Card("dbg_profile", new Vector2(0, 0)))
                {
                    Widgets.SectionHeader("プロファイル情報");
                    try
                    {
                        var activeId = p.Config.ActiveContentId;
                        var profs = p.Config.Profiles ?? new System.Collections.Generic.List<CharacterProfile>();
                        var cnt = profs.Count;
                        ImGui.Text($"Profiles: {cnt}");
                        ImGui.SameLine();
                        ImGui.TextDisabled(activeId.HasValue ? $"Active: 0x{activeId.Value:X}" : "Active: (none)");
                    }
                    catch { }
                }
                using (Widgets.Card("dbg_settings", new Vector2(0, 0)))
                {
                    Widgets.SectionHeader("デバッグ");
                    bool dbg = p.Config.DebugLogging;
                    if (ImGui.Checkbox("デバッグログを有効", ref dbg)) { p.Config.DebugLogging = dbg; p.SaveConfig(); }
                    if (ImGui.Button("トレースを開く")) { p.Ui_OpenTrace(); }
                }
                ImGui.EndChild();

                // 右カラム: 操作/詳細設定
                ImGui.TableNextColumn();
                ImGui.BeginChild("dbg_col_right", new Vector2(ColWidth, 420f), true, ImGuiWindowFlags.None);
                using (Widgets.Card("dbg_ops", new Vector2(0, 0)))
                {
                    Widgets.SectionHeader("操作");
                    if (ImGui.Button("UIから学習")) { p.Ui_LearnNames(); }
                    ImGui.SameLine(); ImGui.TextDisabled("UI項目から艦名学習");

                    if (ImGui.Button("UIから取得")) { p.Ui_DumpUi(); }
                    ImGui.SameLine(); ImGui.TextDisabled("UI解析で取得(実験)");

                    if (ImGui.Button("メモリから取得")) { p.Ui_DumpMemory(); }
                    ImGui.SameLine(); ImGui.TextDisabled("UI不可時の保険");

                    if (ImGui.Button("アドオンを探す")) { p.Ui_Probe(); }
                    ImGui.SameLine(); ImGui.TextDisabled("候補の存在/表示を確認");

                    if (ImGui.Button("フォルダを開く")) { p.Ui_OpenBridgeFolder(); }
                    ImGui.SameLine(); ImGui.TextDisabled("Bridgeフォルダを開く");

                    if (ImGui.Button("プロファイル状態をログ出力")) { p.Ui_LogProfileState(); }
                    ImGui.SameLine(); ImGui.TextDisabled("Active/Profiles/UIの状態を xsr_debug.log に吐きます");
                }
                using (Widgets.Card("dbg_advanced2", new Vector2(0, 0)))
                {
                    // 詳細設定は開閉可能（閉じていてもテーブルは維持して幅を安定化）
                    try { ImGui.SetNextItemOpen(true, ImGuiCond.Once); } catch { }
                    if (!ImGui.CollapsingHeader("詳細設定"))
                    {
                        // 何も描画しない（幅はテーブルが維持する）
                    }
                    else
                    {
                    // メモリ優先/スキャン/位相
                    bool memOnly = p.Config.MemoryOnlyMode;
                    if (ImGui.Checkbox("メモリのみで取得（UI追補なし）", ref memOnly)) { p.Config.MemoryOnlyMode = memOnly; p.SaveConfig(); }
                    bool scan = p.Config.MemoryRouteScanEnabled;
                    if (ImGui.Checkbox("計画航路のスキャンを有効", ref scan)) { p.Config.MemoryRouteScanEnabled = scan; p.SaveConfig(); }
                    int w = p.Config.MemoryRouteScanWindowBytes;
                    if (ImGui.InputInt("スキャン窓(±bytes, 0=全域)", ref w)) { p.Config.MemoryRouteScanWindowBytes = Math.Max(0, w); p.SaveConfig(); }
                    bool zeroTerm = p.Config.MemoryRouteZeroTerminated;
                    if (ImGui.Checkbox("0終端前提で読む", ref zeroTerm)) { p.Config.MemoryRouteZeroTerminated = zeroTerm; p.SaveConfig(); }
                    bool phase = p.Config.MemoryScanPhaseEnabled;
                    if (ImGui.Checkbox("位相/lenhdr探索を許可", ref phase)) { p.Config.MemoryScanPhaseEnabled = phase; p.SaveConfig(); }

                    ImGui.Separator();
                    // ゲート/イベント
                    bool terr = p.Config.EnableTerritoryGate;
                    if (ImGui.Checkbox("工房内のみ自動取得 (TerritoryGate)", ref terr)) { p.Config.EnableTerritoryGate = terr; p.SaveConfig(); }
                    bool ag = p.Config.EnableAddonGate;
                    if (ImGui.Checkbox("対象アドオン可視時のみ実行 (AddonGate)", ref ag)) { p.Config.EnableAddonGate = ag; p.SaveConfig(); }
                    bool prefArr = p.Config.PreferArrayDataFirst;
                    if (ImGui.Checkbox("ArrayData直読を優先（可能時）", ref prefArr)) { p.Config.PreferArrayDataFirst = prefArr; p.SaveConfig(); }
                    bool ev = p.Config.EnableAddonLifecycleCapture;
                    if (ImGui.Checkbox("AddonLifecycle連動を有効（準備中）", ref ev)) { p.Config.EnableAddonLifecycleCapture = ev; p.SaveConfig(); }

                    ImGui.Separator();
                    // 採用ポリシー
                    bool preferLong = p.Config.AdoptPreferLonger;
                    if (ImGui.Checkbox("長いルートを優先（Complete>Partial>Tail）", ref preferLong)) { p.Config.AdoptPreferLonger = preferLong; p.SaveConfig(); }
                    bool downgr = p.Config.AdoptAllowDowngrade;
                    if (ImGui.Checkbox("降格を許可（通常OFF）", ref downgr)) { p.Config.AdoptAllowDowngrade = downgr; p.SaveConfig(); }
                    bool persist = p.Config.AdoptCachePersist;
                    if (ImGui.Checkbox("最後に良かったルートを永続化", ref persist)) { p.Config.AdoptCachePersist = persist; p.SaveConfig(); }
                    int ttl = p.Config.AdoptTtlHours;
                    if (ImGui.InputInt("TTL(時間) Full保持", ref ttl)) { p.Config.AdoptTtlHours = Math.Clamp(ttl, 1, 72); p.SaveConfig(); }

                    ImGui.Separator();
                    bool memFb = p.Config.UseMemoryFallback;
                    if (ImGui.Checkbox("UI失敗時はメモリ取得（保険）", ref memFb))
                    { p.Config.UseMemoryFallback = memFb; p.SaveConfig(); }
                    ImGui.TextDisabled("解析不可時にメモリ直読へ");

                    bool selExt = p.Config.UseSelectStringExtraction;
                    if (ImGui.Checkbox("メニューから艦名を取得（高速）", ref selExt))
                    { p.Config.UseSelectStringExtraction = selExt; p.SaveConfig(); }
                    ImGui.TextDisabled("“艦を選ぶ”の項目から抽出");

                    bool selDet = p.Config.UseSelectStringDetailExtraction;
                    if (ImGui.Checkbox("画面テキストを総当り（低速）", ref selDet))
                    { p.Config.UseSelectStringDetailExtraction = selDet; p.SaveConfig(); }
                    ImGui.TextDisabled("見逃し減だが重い。通常OFF");

                    bool aggr2 = p.Config.AggressiveFallback;
                    if (ImGui.Checkbox("検出不可時に工房も総当り（強）", ref aggr2))
                    { p.Config.AggressiveFallback = aggr2; p.SaveConfig(); }
                    ImGui.TextDisabled("関連パネルも広く走査");

                    bool acceptDefaults = p.Config.AcceptDefaultNamesInMemory;
                    if (ImGui.Checkbox("Submarine-<n>名でもJSONに記録", ref acceptDefaults))
                    { p.Config.AcceptDefaultNamesInMemory = acceptDefaults; p.SaveConfig(); }
                    }
                }
                ImGui.EndChild();
                ImGui.EndTable();
            }

            // プローブ結果（省スペース）
            var probeTxt = p.Ui_GetProbeText();
            if (!string.IsNullOrEmpty(probeTxt))
            {
                using (Widgets.Card("dbg_probe", new Vector2(0, 120)))
                {
                    ImGui.Text("スキャン結果:");
                    ImGui.BeginChild("probe", new Vector2(0, 90), true, ImGuiWindowFlags.None);
                    ImGui.TextWrapped(probeTxt);
                    ImGui.EndChild();
                    ImGui.SameLine(); if (ImGui.Button("クリア")) { p.Ui_ClearProbeText(); }
                }
            }

            // 旧「詳細設定」カードは右カラムに統合
        }
    }
}
