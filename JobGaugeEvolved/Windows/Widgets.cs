using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using JobGaugeEvolved.Configuration;

namespace JobGaugeEvolved.Windows;

/// <summary>Small ImGui helpers shared by the settings of every gauge.</summary>
internal static class Widgets
{
    private static readonly (GradientDirection Value, string Name)[] Directions =
    [
        (GradientDirection.TopToBottom, "Top to bottom"),
        (GradientDirection.LeftToRight, "Left to right"),
    ];

    private const ImGuiColorEditFlags ColorFlags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar;

    public static float ItemWidth => 220 * ImGui.GetIO().FontGlobalScale;

    /// <summary>
    /// One colour setting: single colour or a two-colour gradient across the shape, with a reset button.
    /// Returns whether it changed.
    /// </summary>
    public static bool ColorSetting(string label, ColorSpec spec, Func<ColorSpec> defaults, string? help = null)
    {
        using var id = ImRaii.PushId(label);
        var changed = false;

        var gradient = spec.Mode == ColorMode.Gradient;
        changed |= ImGui.ColorEdit4("##a", ref spec.A, ColorFlags);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(gradient ? "Start colour" : "Colour");
        }

        if (gradient) {
            ImGui.SameLine();
            changed |= ImGui.ColorEdit4("##b", ref spec.B, ColorFlags);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("End colour");
            }
        }

        ImGui.SameLine();
        if (ImGui.Checkbox("Gradient", ref gradient)) {
            spec.Mode = gradient ? ColorMode.Gradient : ColorMode.Single;
            if (gradient && spec.B == spec.A) {
                spec.B = Vector4.Lerp(spec.A, Vector4.One, 0.5f) with { W = spec.A.W };
            }

            changed = true;
        }

        if (gradient) {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(130 * ImGui.GetIO().FontGlobalScale);
            changed |= EnumCombo("##direction", ref spec.Direction, Directions);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset")) {
            spec.CopyFrom(defaults());
            changed = true;
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        if (help is not null) {
            HelpMarker(help);
        }

        return changed;
    }

    /// <summary>A glow's settings under their own header: pulse, timing, size, brightness, blending.</summary>
    public static bool GlowSettings(string title, GlowStyle glow, string help)
    {
        using var id = ImRaii.PushId(title);
        var changed = false;
        SectionHeader(title);
        ImGui.TextDisabled(help);

        changed |= ImGui.Checkbox("Pulse", ref glow.Pulse);
        HelpMarker("Each pulse comes out from behind the shape, drifts outward until there is a gap, fades and starts " +
                   "again, like energy coming off it. Off: the glow stays at the edge.");

        using (ImRaii.Disabled(!glow.Pulse)) {
            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderFloat("Seconds per pulse", ref glow.Period, GlowStyle.MinPeriod, GlowStyle.MaxPeriod, "%.1f s");
            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderFloat("Gap at the end of a pulse", ref glow.Gap, 0f, GlowStyle.MaxGap, "%.0f px");
        }

        ImGui.SetNextItemWidth(ItemWidth);
        changed |= ImGui.SliderFloat("Glow width", ref glow.Width, GlowStyle.MinWidth, GlowStyle.MaxWidth, "%.0f px");

        var brightness = glow.Intensity * 100f;
        ImGui.SetNextItemWidth(ItemWidth);
        if (ImGui.SliderFloat("Glow brightness", ref brightness, 0f, GlowStyle.MaxIntensity * 100f, "%.0f%%")) {
            glow.Intensity = brightness / 100f;
            changed = true;
        }

        changed |= ImGui.Checkbox("Additive blending", ref glow.Additive);
        HelpMarker("Added to what is behind it, like GIMP's Addition mode. Turn this off if it looks wrong; it is then " +
                   "painted over instead.");
        return changed;
    }

    public static bool EnumCombo<T>(string label, ref T value, (T Value, string Name)[] options) where T : struct, Enum
    {
        var preview = string.Empty;
        foreach (var (optionValue, name) in options) {
            if (EqualityComparer<T>.Default.Equals(optionValue, value)) {
                preview = name;
                break;
            }
        }

        using var combo = ImRaii.Combo(label, preview);
        if (!combo) {
            return false;
        }

        var changed = false;
        foreach (var (optionValue, name) in options) {
            var selected = EqualityComparer<T>.Default.Equals(optionValue, value);
            if (ImGui.Selectable(name, selected) && !selected) {
                value = optionValue;
                changed = true;
            }
        }

        return changed;
    }

    public static void SectionHeader(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(ImGuiColors.DalamudViolet, title);
        ImGui.Separator();
        ImGui.Spacing();
    }

    public static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) {
            using var tooltip = ImRaii.Tooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
        }
    }
}
