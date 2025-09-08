using System;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;

namespace XIVSubmarinesReturn.UI
{
    internal static class OverviewTab
    {
        internal static void Draw(Plugin p)
        {
            using var _ = Theme.UseDensity(p.Config.UiRowDensity);
            try { ImGui.SetWindowFontScale(Math.Clamp(p.Config.UiFontScale, 0.9f, 1.3f)); } catch { }

            // 操作
            using (Widgets.Card("ov_ops", new Vector2(0, 0)))
            {
                Widgets.SectionHeader("操作");
                if (ImGui.Button("メモリから取得")) { p.Ui_DumpMemory(); }
                ImGui.SameLine();
                if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.Sync, "JSON再読込（テーブル更新）")) { p.Ui_ReloadSnapshot(); }
                ImGui.SameLine();
                if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.CloudDownloadAlt, "Mogship取込")) { p.Ui_ImportFromMogship(); }
                ImGui.SameLine();
                if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.FolderOpen, "Bridgeフォルダ")) { p.Ui_OpenBridgeFolder(); }
                ImGui.SameLine();
                if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.Cog, "Configフォルダ")) { p.Ui_OpenConfigFolder(); }
                bool autoCap = p.Config.AutoCaptureOnWorkshopOpen;
                ImGui.SameLine(); if (ImGui.Checkbox("工房を開いたら自動取得", ref autoCap)) { p.Config.AutoCaptureOnWorkshopOpen = autoCap; p.SaveConfig(); }
                var status = p.Ui_GetUiStatus();
                if (!string.IsNullOrEmpty(status)) { ImGui.SameLine(); ImGui.TextDisabled(status); }
            }

            // 表示設定（簡素化）
            using (Widgets.Card("ov_display", new Vector2(0, 0)))
            {
                Widgets.SectionHeader("表示設定");
                // ルート表示はレター固定
                if (p.Config.RouteDisplay != RouteDisplayMode.Letters) { p.Config.RouteDisplay = RouteDisplayMode.Letters; p.SaveConfig(); }
                // Mapヒント/エイリアス再読込のみ残す
                var mapHint = p.Config.SectorMapHint ?? string.Empty;
                if (ImGui.InputText("Mapヒント", ref mapHint, 64)) { p.Config.SectorMapHint = mapHint; p.SaveConfig(); }
                ImGui.SameLine();
                if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.RedoAlt, "Alias JSON再読込")) { p.Ui_ReloadAliasIndex(); }
            }

            // 航路情報
            using (Widgets.Card("ov_table"))
            {
                Widgets.SectionHeader("航路情報");
                p.Ui_DrawSnapshotTable();
            }
        }
    }
}
