using System;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiWindowFlags = Dalamud.Bindings.ImGui.ImGuiWindowFlags;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;

namespace XIVSubmarinesReturn.UI
{
    internal static class DebugTab
    {
        internal static void Draw(Plugin p)
        {
            using var _ = Theme.UseDensity(p.Config.UiRowDensity);
            try { ImGui.SetWindowFontScale(Math.Clamp(p.Config.UiFontScale, 0.9f, 1.3f)); } catch { }

            // プロファイル情報
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

            // 設定（縦並び）
            using (Widgets.Card("dbg_settings", new Vector2(0, 0)))
            {
                Widgets.SectionHeader("デバッグ");
                bool dbg = p.Config.DebugLogging;
                if (ImGui.Checkbox("デバッグログを有効", ref dbg)) { p.Config.DebugLogging = dbg; p.SaveConfig(); }
                if (ImGui.Button("トレースを開く")) { p.Ui_OpenTrace(); }
            }

            // 操作（縦並び・右に説明）
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

            // 詳細設定（わかりやすい日本語）
            using (Widgets.Card("dbg_advanced"))
            {
                Widgets.SectionHeader("詳細設定");
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

                bool aggr = p.Config.AggressiveFallback;
                if (ImGui.Checkbox("検出不可時に工房も総当り（強）", ref aggr))
                { p.Config.AggressiveFallback = aggr; p.SaveConfig(); }
                ImGui.TextDisabled("関連パネルも広く走査");

                bool acceptDefaults = p.Config.AcceptDefaultNamesInMemory;
                if (ImGui.Checkbox("Submarine-<n>名でもJSONに記録", ref acceptDefaults))
                { p.Config.AcceptDefaultNamesInMemory = acceptDefaults; p.SaveConfig(); }
            }
        }
    }
}
