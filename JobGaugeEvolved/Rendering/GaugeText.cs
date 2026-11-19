using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using JobGaugeEvolved.Configuration;

namespace JobGaugeEvolved.Rendering;

/// <summary>Outlined numbers in the game's own gauge font, built large and scaled down to any size.</summary>
internal sealed class GaugeText : IDisposable
{
    private static readonly Vector4 Fill = new(0.97f, 0.98f, 1f, 1f);
    private static readonly Vector4 Outline = new(0.02f, 0.03f, 0.05f, 1f);

    private readonly IFontHandle font;

    public GaugeText()
    {
        font = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
            new GameFontStyle(GameFontFamilyAndSize.MiedingerMid36));
    }

    public void Dispose() => font.Dispose();

    /// <summary>Draws <paramref name="text"/> centred on <paramref name="centre"/>, <paramref name="pixelHeight"/> tall.</summary>
    public void Draw(ImDrawListPtr drawList, string text, Vector2 centre, float pixelHeight)
    {
        if (!font.Available || pixelHeight < 4f) {
            return;
        }

        using (font.Push()) {
            var imFont = ImGui.GetFont();
            var native = ImGui.GetFontSize();
            if (native <= 0f) {
                return;
            }

            var size = ImGui.CalcTextSize(text, false, -1f) * (pixelHeight / native);
            var pos = centre - size / 2f;
            var outline = MathF.Max(1f, pixelHeight * 0.08f);
            var outlineColor = ColorSpec.Pack(Outline);
            for (var dx = -1; dx <= 1; dx++) {
                for (var dy = -1; dy <= 1; dy++) {
                    if (dx != 0 || dy != 0) {
                        drawList.AddText(imFont, pixelHeight, pos + new Vector2(dx, dy) * outline, outlineColor, text);
                    }
                }
            }

            drawList.AddText(imFont, pixelHeight, pos, ColorSpec.Pack(Fill), text);
        }
    }
}
