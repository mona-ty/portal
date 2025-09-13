using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;

namespace XIVSubmarinesReturn.UI
{
    internal static class AlarmTab
    {
        private static bool _revealDiscord;
        private static bool _revealNotion;
        private static string _dbUrlInput = string.Empty;
        private static string _parentPageUrl = string.Empty;

        internal static void Draw(Plugin p)
        {
            using var _ = Theme.UseDensity(p.Config.UiRowDensity);
            try { ImGui.SetWindowFontScale(Math.Clamp(p.Config.UiFontScale, 0.9f, 1.3f)); } catch { }

            // ゲーム内アラーム（縦並び）
            using (Widgets.Card("card_game_alarm", new Vector2(0, 0)))
            {
                var header = new Vector4(0.88f, 0.88f, 0.92f, 0.55f);
                Widgets.SectionHeaderBar("ゲーム内アラーム", header);
                var enable = p.Config.GameAlarmEnabled;
                Widgets.StatusPill(enable, "有効", "無効"); ImGui.SameLine();
                if (ImGui.Checkbox("有効##game", ref enable)) { p.Config.GameAlarmEnabled = enable; p.SaveConfig(); }

                // 通知タイミング（ON/OFFトグル）
                var list = (p.Config.AlarmLeadMinutes ?? new List<int>()).ToList();
                ImGui.Text("通知タイミング"); ImGui.SameLine(); ImGui.TextDisabled("選んだ分前に通知（トースト音あり）");
                var set = new HashSet<int>(list);
                bool changed = false;
                int[] opts = new[] { 5, 10, 30, 0 };
                for (int i = 0; i < opts.Length; i++)
                {
                    var m = opts[i]; bool on = set.Contains(m);
                    if (ImGui.Checkbox($"{m}分", ref on)) { if (on) set.Add(m); else set.Remove(m); changed = true; }
                    if (i < opts.Length - 1) ImGui.SameLine();
                }
                if (changed)
                {
                    var distinct = set.OrderBy(x => x).ToList();
                    p.Config.AlarmLeadMinutes = distinct; p.SaveConfig();
                }
                if (set.Count > 0)
                {
                    var cur = string.Join(", ", set.OrderBy(x => x).Select(x => $"{x}分"));
                    ImGui.SameLine(); ImGui.TextDisabled($"現在: {cur}");
                }
                ImGui.Separator();
                if (ImGui.SmallButton("テスト通知")) { p.Ui_TestGameAlarm(); }
            }

            // 外部通知カードは削除（Discord / Notion を個別表示）

            // Discord（縦並び）
            using (Widgets.Card("card_discord", new Vector2(0, 0)))
            {
                var header = new Vector4(0.88f, 0.88f, 0.92f, 0.55f);
                Widgets.SectionHeaderBar("Discord", header);
                var enabled = p.Config.DiscordEnabled;
                Widgets.StatusPill(enabled, "有効", "無効"); ImGui.SameLine();
                if (ImGui.Checkbox("有効##discord", ref enabled)) { p.Config.DiscordEnabled = enabled; p.SaveConfig(); }
                var wh = p.Config.DiscordWebhookUrl ?? string.Empty;
                if (Widgets.MaskedInput("Webhook URL", ref wh, 256, ref _revealDiscord)) { p.Config.DiscordWebhookUrl = wh; p.SaveConfig(); }
                var latestOnly = p.Config.DiscordLatestOnly;
                if (ImGui.Checkbox("最早のみ", ref latestOnly)) { p.Config.DiscordLatestOnly = latestOnly; p.SaveConfig(); }
                ImGui.SameLine();
                var embeds = p.Config.DiscordUseEmbeds;
                if (ImGui.Checkbox("埋め込み", ref embeds)) { p.Config.DiscordUseEmbeds = embeds; p.SaveConfig(); }
                int minIv = p.Config.DiscordMinIntervalMinutes;
                if (ImGui.InputInt("通知の最小間隔(分)", ref minIv)) { p.Config.DiscordMinIntervalMinutes = Math.Clamp(minIv, 0, 1440); p.SaveConfig(); }
                if (ImGui.SmallButton("テスト送信")) { p.Ui_TestDiscord(); }
            }

            // Notion（縦並び・詳細設定を再掲）
            using (Widgets.Card("card_notion", new Vector2(0, 0)))
            {
                var header = new Vector4(0.88f, 0.88f, 0.92f, 0.55f);
                Widgets.SectionHeaderBar("Notion", header);
                var nEnable = p.Config.NotionEnabled;
                Widgets.StatusPill(nEnable, "有効", "無効"); ImGui.SameLine();
                if (ImGui.Checkbox("有効##notion", ref nEnable)) { p.Config.NotionEnabled = nEnable; p.SaveConfig(); }
                var token = p.Config.NotionToken ?? string.Empty;
                if (Widgets.MaskedInput("Integration Token", ref token, 256, ref _revealNotion)) { p.Config.NotionToken = token; p.SaveConfig(); }
                var db = p.Config.NotionDatabaseId ?? string.Empty;
                if (ImGui.InputText("Database ID", ref db, 128)) { p.Config.NotionDatabaseId = db; p.SaveConfig(); }

                // Helper: DB URL -> ID 抽出
                ImGui.PushItemWidth(360);
                ImGui.InputText("DB URL (貼付)", ref _dbUrlInput, 512);
                ImGui.PopItemWidth();
                ImGui.SameLine();
                if (ImGui.SmallButton("URL→ID"))
                {
                    try
                    {
                        var id = XIVSubmarinesReturn.Services.NotionClient.TryExtractIdFromUrlOrId(_dbUrlInput);
                        if (!string.IsNullOrWhiteSpace(id)) { p.Config.NotionDatabaseId = id!; p.SaveConfig(); }
                    }
                    catch { }
                }

                // Optional: 親ページURL → 親ページID（自動セットアップ時の作成先指定）
                ImGui.PushItemWidth(360);
                ImGui.InputText("親ページURL(貼付)", ref _parentPageUrl, 512);
                ImGui.PopItemWidth();
                ImGui.SameLine();
                if (ImGui.SmallButton("URL→親ID"))
                {
                    try
                    {
                        var id = XIVSubmarinesReturn.Services.NotionClient.TryExtractIdFromUrlOrId(_parentPageUrl);
                        if (!string.IsNullOrWhiteSpace(id)) { p.Config.NotionParentPageId = id!; p.SaveConfig(); }
                    }
                    catch { }
                }

                // Auto setup
                if (ImGui.SmallButton("自動セットアップ")) { p.Ui_AutoSetupNotion(); }
                var nLatest = p.Config.NotionLatestOnly;
                if (ImGui.Checkbox("最早のみ", ref nLatest)) { p.Config.NotionLatestOnly = nLatest; p.SaveConfig(); }

                ImGui.Text("Upsertキー");
                var mode = (int)p.Config.NotionKeyMode;
                if (ImGui.RadioButton("Per Slot", mode == 0)) { p.Config.NotionKeyMode = NotionKeyMode.PerSlot; p.SaveConfig(); mode = 0; }
                ImGui.SameLine(); if (ImGui.RadioButton("Slot+Route", mode == 1)) { p.Config.NotionKeyMode = NotionKeyMode.PerSlotRoute; p.SaveConfig(); mode = 1; }
                ImGui.SameLine(); if (ImGui.RadioButton("Voyage", mode == 2)) { p.Config.NotionKeyMode = NotionKeyMode.PerVoyage; p.SaveConfig(); mode = 2; }

                // プロパティ名（再掲）
                var pn = p.Config.NotionPropName ?? "Name";
                if (ImGui.InputText("Prop: Name (title)", ref pn, 64)) { p.Config.NotionPropName = pn; p.SaveConfig(); }
                var ps = p.Config.NotionPropSlot ?? "Slot";
                if (ImGui.InputText("Prop: Slot (number)", ref ps, 64)) { p.Config.NotionPropSlot = ps; p.SaveConfig(); }
                var pe = p.Config.NotionPropEta ?? "ETA";
                if (ImGui.InputText("Prop: ETA (date)", ref pe, 64)) { p.Config.NotionPropEta = pe; p.SaveConfig(); }
                var pr = p.Config.NotionPropRoute ?? "Route";
                if (ImGui.InputText("Prop: Route (rich_text)", ref pr, 64)) { p.Config.NotionPropRoute = pr; p.SaveConfig(); }
                var prk = p.Config.NotionPropRank ?? "Rank";
                if (ImGui.InputText("Prop: Rank (number)", ref prk, 64)) { p.Config.NotionPropRank = prk; p.SaveConfig(); }
                var px = p.Config.NotionPropExtId ?? "ExtId";
                if (ImGui.InputText("Prop: ExtId (rich_text)", ref px, 64)) { p.Config.NotionPropExtId = px; p.SaveConfig(); }
                var prem = p.Config.NotionPropRemaining ?? "Remaining";
                if (ImGui.InputText("Prop: Remaining (rich_text)", ref prem, 64)) { p.Config.NotionPropRemaining = prem; p.SaveConfig(); }
                var pw = p.Config.NotionPropWorld ?? "World";
                if (ImGui.InputText("Prop: World (rich_text)", ref pw, 64)) { p.Config.NotionPropWorld = pw; p.SaveConfig(); }
                var pc = p.Config.NotionPropCharacter ?? "Character";
                if (ImGui.InputText("Prop: Character (rich_text)", ref pc, 64)) { p.Config.NotionPropCharacter = pc; p.SaveConfig(); }
                var pfc = p.Config.NotionPropFC ?? "FC";
                if (ImGui.InputText("Prop: FC (rich_text)", ref pfc, 64)) { p.Config.NotionPropFC = pfc; p.SaveConfig(); }

                if (ImGui.SmallButton("検証")) { p.Ui_ValidateNotion(); }
                ImGui.SameLine(); if (ImGui.SmallButton("テスト送信")) { p.Ui_TestNotion(); }
            }

        }
    }
}
