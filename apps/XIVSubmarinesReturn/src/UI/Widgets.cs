using System;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using ImGuiInputTextFlags = Dalamud.Bindings.ImGui.ImGuiInputTextFlags;
using System.Numerics;

namespace XIVSubmarinesReturn.UI
{
    internal static class Widgets
    {
        public static bool IconButton(FontAwesomeIcon icon, string tooltip)
        {
            bool clicked = false;
            try
            {
                clicked = ImGuiComponents.IconButton(icon);
                if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
            }
            catch
            {
                // Fallback: text button
                clicked = ImGui.Button(tooltip);
            }
            return clicked;
        }

        // マスク付き入力（目アイコンで表示/非表示を切替）。値が変更された場合 true を返す。
        public static bool MaskedInput(string label, ref string value, int maxLen, ref bool reveal)
        {
            bool changed = false;
            try
            {
                ImGui.PushID(label);
                // 左側にラベル
                if (!string.IsNullOrEmpty(label)) ImGui.TextUnformatted(label);
                if (!string.IsNullOrEmpty(label)) { ImGui.SameLine(180); }

                // 表示/非表示切替（非表示時はPasswordフラグで隠す）
                if (reveal)
                {
                    changed = ImGui.InputText("##val", ref value, maxLen);
                }
                else
                {
                    changed = ImGui.InputText("##val", ref value, maxLen, ImGuiInputTextFlags.Password);
                }

                // 目アイコン（ツールチップは日本語: 表示/非表示）
                ImGui.SameLine();
                var icon = reveal ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye;
                if (ImGuiComponents.IconButton(icon)) reveal = !reveal;
                try { if (ImGui.IsItemHovered()) ImGui.SetTooltip(reveal ? "非表示" : "表示"); } catch { }

                ImGui.PopID();
            }
            catch { }
            return changed;
        }

        // Card: 自動高さ（BeginGroup）/固定高さ（BeginChild）を切替
        public readonly struct CardScope : IDisposable
        {
            private readonly bool _isChild;
            public CardScope(bool isChild) { _isChild = isChild; }
            public void Dispose()
            {
                try
                {
                    if (_isChild) ImGui.EndChild();
                    else ImGui.EndGroup();
                }
                catch { }
            }
        }
        public static CardScope Card(string id, Vector2? size = null)
        {
            try
            {
                if (size.HasValue && size.Value.Y > 0)
                {
                    // 固定高さのカード（枠なし）
                    ImGui.BeginChild(id, size.Value, false);
                    return new CardScope(true);
                }
                else
                {
                    // 自動高さ: Childを使わずGroupで囲む
                    ImGui.BeginGroup();
                    return new CardScope(false);
                }
            }
            catch { }
            return new CardScope(false);
        }

        public static void SectionHeader(string text)
        {
            try { ImGui.Text(text ?? string.Empty); ImGui.Separator(); } catch { }
        }

        public static void SectionHeaderBar(string text, System.Numerics.Vector4 color, float height = 22f)
        {
            try
            {
                var dl = ImGui.GetWindowDrawList();
                var p = ImGui.GetCursorScreenPos();
                var w = ImGui.GetContentRegionAvail().X;
                var r = 5f;
                var rectMin = p;
                var rectMax = new System.Numerics.Vector2(p.X + MathF.Max(40f, w), p.Y + height);
                uint bg = ImGui.ColorConvertFloat4ToU32(color);
                dl.AddRectFilled(rectMin, rectMax, bg, r);
                // テキスト色（背景の明度から推定）
                float lum = 0.299f * color.X + 0.587f * color.Y + 0.114f * color.Z;
                bool lightBg = lum > 0.6f;
                var ts = ImGui.CalcTextSize(text ?? string.Empty);
                var tx = p.X + 8f; var ty = p.Y + MathF.Max(2f, (height - ts.Y) * 0.5f);
                uint fg = ImGui.ColorConvertFloat4ToU32(lightBg ? new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1f) : new System.Numerics.Vector4(1f, 1f, 1f, 1f));
                dl.AddText(new System.Numerics.Vector2(tx, ty), fg, text ?? string.Empty);
                // カーソルをバーの下へ移動
                ImGui.Dummy(new System.Numerics.Vector2(w, height + 4f));
            }
            catch { }
        }

        public static bool BeginForm(string id) { try { ImGui.PushID(id); return true; } catch { return false; } }
        public static void EndForm() { try { ImGui.PopID(); } catch { } }
        public static void FormRow(string label, Action draw)
        {
            try
            {
                ImGui.TextUnformatted(label ?? string.Empty);
                ImGui.SameLine(180);
                ImGui.PushItemWidth(-1);
                draw?.Invoke();
                ImGui.PopItemWidth();
            }
            catch { }
        }

        public static void StatusPill(bool on, string onText = "Enabled", string offText = "Disabled")
        {
            try
            {
                var col = on ? new Vector4(0.2f, 0.7f, 0.3f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 1f);
                ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, col);
                ImGui.TextUnformatted(on ? onText : offText);
                ImGui.PopStyleColor();
            }
            catch { }
        }

        public static void Badge(string text)
        {
            try { ImGui.TextDisabled(text ?? string.Empty); } catch { }
        }
    }
}
