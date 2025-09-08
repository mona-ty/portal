using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;
using ImGuiSortDirection = Dalamud.Bindings.ImGui.ImGuiSortDirection;
using ImGuiTableColumnFlags = Dalamud.Bindings.ImGui.ImGuiTableColumnFlags;
using XIVSubmarinesReturn;

namespace XIVSubmarinesReturn.UI
{
    internal sealed class SnapshotTable
    {
        private int _sortField = 3; // 0=Name 1=Slot 2=Rank 3=ETA
        private bool _sortAsc = true;

        public void Draw(IReadOnlyList<SubmarineRecord> itemsSource,
            Configuration cfg,
            Func<int, string?> aliasResolver,
            Action<string>? setStatus = null,
            Action? onConfigChanged = null)
        {
            if (itemsSource == null || itemsSource.Count == 0)
            {
                ImGui.TextDisabled("データがありません。取得してください。");
                return;
            }

            // 初期ソート状態を設定から復元
            if (_sortField == 3 && cfg.TableSortField != 3) _sortField = cfg.TableSortField;
            if (_sortAsc != cfg.TableSortAsc) _sortAsc = cfg.TableSortAsc;

            var list = new List<SubmarineRecord>(itemsSource);

            // 列幅の動的算出（表示予定テキストから計測）
            // すべての列（スロット/名前/ランク/ETA/残り/ルート）で最大文字幅を基準に初期幅を決定。
            float MeasureMax(params string[] texts)
            {
                float max = 0f;
                foreach (var s in texts)
                {
                    var w = ImGui.CalcTextSize(s ?? string.Empty).X;
                    if (w > max) max = w;
                }
                // 余白（セルパディング + ソートインジケータ + スケール余裕）
                return max + 40f;
            }

            string GetEtaText(SubmarineRecord it)
            {
                try
                {
                    if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                    {
                        var dt = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value).ToLocalTime().DateTime;
                        return dt.ToString("yyyy/M/d HH:mm");
                    }
                    if (it.Extra != null && it.Extra.TryGetValue("EtaLocal", out var t))
                        return t ?? string.Empty;
                }
                catch { }
                return string.Empty;
            }

            var slotWidth = 50f;
            var nameWidth = 120f;
            var rankWidth = 60f;
            var etaWidth = 140f;
            var remWidth = 100f;
            var routeWidth = 180f;
            try
            {
                // 測定データを収集
                var slots = list.Select(x => x.Slot.HasValue ? $"S{x.Slot.Value}" : string.Empty).ToArray();
                var names = list.Select(x => x.Name ?? string.Empty).ToArray();
                var ranks = list.Select(x => x.Rank?.ToString() ?? string.Empty).ToArray();
                var etas  = list.Select(GetEtaText).ToArray();
                var rems  = list.Select(x => (x.Extra != null && x.Extra.TryGetValue("RemainingText", out var r)) ? (r ?? string.Empty) : string.Empty).ToArray();
                var routes= list.Select(x => BuildRouteDisplay(x, cfg, aliasResolver) ?? string.Empty).ToArray();

                // ヘッダ文字列も考慮
                slotWidth = MeasureMax(new[]{"スロット"}.Concat(slots).ToArray());
                nameWidth = MeasureMax(new[]{"名前"}.Concat(names).ToArray());
                rankWidth = MeasureMax(new[]{"ランク"}.Concat(ranks).ToArray());
                var etaTemplate = new [] { "ETA", "2099/12/31 23:59" };
                etaWidth  = MeasureMax(etaTemplate.Concat(etas).ToArray());
                remWidth  = MeasureMax(new[]{"残り"}.Concat(rems).ToArray());
                routeWidth= MeasureMax(new[]{"ルート"}.Concat(routes).ToArray());

                // 最小幅/最大幅のクランプ（見切れ防止しつつ極端な拡張を抑制）
                slotWidth = Math.Clamp(slotWidth, 52f, 90f);
                nameWidth = Math.Clamp(nameWidth, 90f, 300f);
                rankWidth = Math.Clamp(rankWidth, 50f, 90f);
                etaWidth  = Math.Clamp(etaWidth, 140f, 320f);
                remWidth  = Math.Clamp(remWidth, 100f, 260f);
                routeWidth= Math.Clamp(routeWidth, 140f, 600f);
            }
            catch { }

            // ヘッダ配色
            var acc = Theme.ParseColor(cfg.AccentColor, new Vector4(0.12f, 0.55f, 0.96f, 1f));
            ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.TableHeaderBg, acc);
            ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.HeaderHovered, acc);
            ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.HeaderActive, acc);

            // すべて固定幅(FixedFit) + ScrollX。初期幅=実データ由来の"最小幅"として機能し、見切れを回避。
            // 右端に余白列が出ないように、横スクロールをやめて伸縮プロポーショナルに変更
            var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable | ImGuiTableFlags.SizingStretchProp;
            if (ImGui.BeginTable("subs", 6, tableFlags))
            {
                ImGui.TableSetupColumn("スロット", ImGuiTableColumnFlags.WidthFixed, slotWidth);
                ImGui.TableSetupColumn("名前",   ImGuiTableColumnFlags.WidthFixed, nameWidth);
                ImGui.TableSetupColumn("ランク", ImGuiTableColumnFlags.WidthFixed, rankWidth);
                ImGui.TableSetupColumn("ETA",   ImGuiTableColumnFlags.WidthFixed, etaWidth);
                ImGui.TableSetupColumn("残り",   ImGuiTableColumnFlags.WidthFixed, remWidth);
                // ルート列は伸縮させ、不要な右端余白列を作らない
                ImGui.TableSetupColumn("ルート", ImGuiTableColumnFlags.WidthStretch, 0f);
                ImGui.TableHeadersRow();


                // ソート同期
                try
                {
                    var specs = ImGui.TableGetSortSpecs();
                    if (specs.SpecsCount > 0)
                    {
                        var spec = specs.Specs[0];
                        int col = spec.ColumnIndex;
                        bool asc = spec.SortDirection != ImGuiSortDirection.Descending;
                        int mapped = _sortField;
                        switch (col)
                        {
                            case 0: mapped = 1; break; // Slot
                            case 1: mapped = 0; break; // Name
                            case 2: mapped = 2; break; // Rank
                            case 3: mapped = 3; break; // ETA
                            case 4: mapped = 3; break; // Remaining -> ETA相当
                            case 5: mapped = 0; break; // Route ~ Name fallback
                        }
                        if (mapped != _sortField || asc != _sortAsc)
                        {
                            _sortField = mapped; _sortAsc = asc;
                            cfg.TableSortField = _sortField; cfg.TableSortAsc = _sortAsc;
                            onConfigChanged?.Invoke();
                        }
                        specs.SpecsDirty = false;
                    }
                }
                catch { }

                // ソート適用
                try { list.Sort((a, b) => Compare(a, b, _sortField, _sortAsc)); } catch { }

                foreach (var it in list)
                {
                    ImGui.TableNextRow();
                    // Slot
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(it.Slot.HasValue ? $"S{it.Slot.Value}" : string.Empty);
                    // Name
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(it.Name ?? string.Empty);
                    // Rank
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(it.Rank?.ToString() ?? string.Empty);

                    // ETA
                    ImGui.TableSetColumnIndex(3);
                    try
                    {
                        string etaLoc = string.Empty;
                        try
                        {
                            if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                            {
                                var dt = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value).ToLocalTime().DateTime;
                                etaLoc = dt.ToString("yyyy/M/d HH:mm");
                            }
                            else if (it.Extra != null && it.Extra.TryGetValue("EtaLocal", out var t))
                            {
                                etaLoc = t ?? string.Empty;
                            }
                        }
                        catch { }
                        bool highlight = IsSoon(it, cfg);
                        if (highlight) { ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, acc); }
                        ImGui.Text(etaLoc ?? string.Empty);
                        if (highlight) ImGui.PopStyleColor();
                    }
                    catch { ImGui.Text(string.Empty); }

                    // 残り
                    ImGui.TableSetColumnIndex(4);
                    try
                    {
                        var rem = (it.Extra != null && it.Extra.TryGetValue("RemainingText", out var r)) ? r : string.Empty;
                        bool highlight = IsSoon(it, cfg);
                        if (highlight) { ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, acc); }
                        ImGui.Text(rem ?? string.Empty);
                        if (highlight) ImGui.PopStyleColor();
                    }
                    catch { ImGui.Text(string.Empty); }

                    // Route (click to copy)
                    ImGui.TableSetColumnIndex(5);
                    try
                    {
                        var show = BuildRouteDisplay(it, cfg, aliasResolver);
                        if (ImGui.Selectable(show ?? string.Empty, false))
                        {
                            try { ImGui.SetClipboardText(show ?? string.Empty); } catch { }
                            setStatus?.Invoke("ルートをコピーしました");
                        }
                    }
                    catch { ImGui.Text(it.RouteKey ?? string.Empty); }
                }

                ImGui.EndTable();
            }
            ImGui.PopStyleColor(3);
        }

        private static bool IsSoon(SubmarineRecord it, Configuration cfg)
        {
            try
            {
                int minsLeft = int.MaxValue;
                if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                {
                    var eta = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value);
                    minsLeft = (int)Math.Round((eta - DateTimeOffset.Now).TotalMinutes);
                }
                else if (it.Extra != null && it.Extra.TryGetValue("RemainingText", out var rem) && !string.IsNullOrWhiteSpace(rem))
                {
                    var m = Regex.Match(rem, @"(?:(?<h>\d+)\s*時間)?\s*(?<m>\d+)\s*分");
                    if (m.Success)
                    {
                        int h = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value) : 0;
                        int mm = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
                        minsLeft = Math.Max(0, h * 60 + mm);
                    }
                }
                return minsLeft <= cfg.HighlightSoonMins;
            }
            catch { return false; }
        }

        private static int Compare(SubmarineRecord? a, SubmarineRecord? b, int field, bool asc)
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

        private static string BuildRouteDisplay(SubmarineRecord it, Configuration cfg, Func<int, string?> aliasResolver)
        {
            var baseRoute = (it.Extra != null && it.Extra.TryGetValue("RouteShort", out var rs)) ? rs : it.RouteKey;
            if (string.IsNullOrWhiteSpace(baseRoute)) return string.Empty;

            if (cfg.RouteDisplay == RouteDisplayMode.Raw)
                return it.RouteKey ?? baseRoute ?? string.Empty;

            var nums = new List<int>();
            foreach (Match m in Regex.Matches(baseRoute ?? string.Empty, @"P?(\d+)"))
                if (int.TryParse(m.Groups[1].Value, out var v)) nums.Add(v);
            if (nums.Count == 0) return baseRoute ?? string.Empty;

            switch (cfg.RouteDisplay)
            {
                case RouteDisplayMode.ShortIds:
                    return string.Join('>', nums.Select(n => $"P{n}"));
                case RouteDisplayMode.Letters:
                default:
                    var parts = new List<string>(nums.Count);
                    foreach (var n in nums)
                    {
                        string? letter = null;
                        try { letter = aliasResolver?.Invoke(n); } catch { }
                        if (string.IsNullOrWhiteSpace(letter) && cfg.RouteNames != null && cfg.RouteNames.TryGetValue((byte)n, out var nm) && !string.IsNullOrWhiteSpace(nm))
                            letter = nm;
                        parts.Add(string.IsNullOrWhiteSpace(letter) ? $"P{n}" : letter!);
                    }
                    var text = string.Join('>', parts);
                    return parts.All(p => p.StartsWith("P")) ? (baseRoute ?? text) : text;
            }
        }
    }
}

