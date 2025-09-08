using System;
using System.Globalization;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiStyleVar = Dalamud.Bindings.ImGui.ImGuiStyleVar;

namespace XIVSubmarinesReturn.UI
{
    internal static class Theme
    {
        public static Vector4 ParseColor(string? hex, Vector4 fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return fallback;
                var s = hex.Trim().TrimStart('#');
                if (s.Length == 6)
                {
                    var r = byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber);
                    var g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber);
                    var b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber);
                    return new Vector4(r / 255f, g / 255f, b / 255f, 1f);
                }
                if (s.Length == 8)
                {
                    var r = byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber);
                    var g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber);
                    var b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber);
                    var a = byte.Parse(s.Substring(6, 2), NumberStyles.HexNumber);
                    return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
                }
            }
            catch { }
            return fallback;
        }

        public static IDisposable UseDensity(XIVSubmarinesReturn.UiDensity density)
        {
            try
            {
                if (density == XIVSubmarinesReturn.UiDensity.Compact)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 4));
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 4));
                }
                else
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8));
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 8));
                }
            }
            catch { }
            return new Popper(2);
        }

        private sealed class Popper : IDisposable
        {
            private readonly int _count;
            public Popper(int count) { _count = count; }
            public void Dispose()
            {
                try { for (int i = 0; i < _count; i++) ImGui.PopStyleVar(); } catch { }
            }
        }
    }
}

