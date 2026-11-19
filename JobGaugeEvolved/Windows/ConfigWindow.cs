using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using JobGaugeEvolved.Configuration;
using JobGaugeEvolved.Gauges;

namespace JobGaugeEvolved.Windows;

/// <summary>The settings window: general options, then one expandable section per job.</summary>
internal sealed class ConfigWindow : Window
{
    private static readonly (PositionMode Value, string Name)[] PositionModes =
    [
        (PositionMode.Free, "Free"),
        (PositionMode.FollowHud, "Follow HUD Layout"),
    ];

    private static readonly (JobRole Role, string Name)[] Roles =
    [
        (JobRole.Tank, "Tanks"),
        (JobRole.Healer, "Healers"),
        (JobRole.Melee, "Melee DPS"),
        (JobRole.PhysicalRanged, "Physical Ranged DPS"),
        (JobRole.MagicalRanged, "Magical Ranged DPS"),
    ];

    private readonly PluginConfiguration config;
    private readonly GaugeManager gauges;
    private bool unsaved;

    public ConfigWindow(Plugin plugin) : base("Job Gauge Evolved###JobGaugeEvolvedConfig")
    {
        config = plugin.Configuration;
        gauges = plugin.Gauges;

        Size = new Vector2(560, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void OnClose()
    {
        // A gauge left unlocked would keep catching clicks, and a preview would hide the real one.
        gauges.Unlocked = false;
        gauges.Preview = false;
        SaveIfNeeded(force: true);
    }

    public override void Draw()
    {
        var changed = false;

        DrawGeneral();

        using (var child = ImRaii.Child("##jobs")) {
            if (child) {
                foreach (var (role, roleName) in Roles) {
                    var inRole = gauges.Gauges.Where(g => g.Role == role).OrderBy(g => g.Name).ToList();
                    if (inRole.Count == 0) {
                        continue;
                    }

                    Widgets.SectionHeader(roleName);
                    foreach (var gauge in inRole) {
                        changed |= DrawJob(gauge);
                    }
                }
            }
        }

        if (changed || gauges.MovedSinceLastCheck) {
            gauges.MovedSinceLastCheck = false;
            config.Sanitize();
            unsaved = true;
        }

        SaveIfNeeded(force: false);
    }

    private void DrawGeneral()
    {
        var unlocked = gauges.Unlocked;
        if (ImGui.Checkbox("Unlock gauges (drag to move)", ref unlocked)) {
            gauges.Unlocked = unlocked;
        }

        Widgets.HelpMarker("While this is ticked, drag the gauge on screen to move it. It also shows out of combat. " +
                           "Closing this window locks the gauges again.");

        var preview = gauges.Preview;
        if (ImGui.Checkbox("Preview", ref preview)) {
            gauges.Preview = preview;
        }

        Widgets.HelpMarker("Shows a gauge with made-up values on any job, in or out of combat, so you can try " +
                           "your settings. Stops when this window closes.");

        if (gauges.Preview) {
            gauges.PreviewGauge ??= gauges.Current ?? gauges.Gauges[0];
            using var indent = ImRaii.PushIndent();

            if (gauges.Gauges.Count > 1) {
                ImGui.SetNextItemWidth(Widgets.ItemWidth);
                using (var combo = ImRaii.Combo("Gauge", gauges.PreviewGauge.Name)) {
                    if (combo) {
                        foreach (var gauge in gauges.Gauges) {
                            if (ImGui.Selectable(gauge.Name, gauge == gauges.PreviewGauge)) {
                                gauges.PreviewGauge = gauge;
                                gauges.PreviewState = null;
                            }
                        }
                    }
                }
            }

            var states = gauges.PreviewGauge.PreviewStates;
            var current = gauges.PreviewState is { } index && index < states.Count ? states[index] : "Cycle through all states";
            ImGui.SetNextItemWidth(Widgets.ItemWidth * 1.4f);
            using var stateCombo = ImRaii.Combo("State", current);
            if (stateCombo) {
                if (ImGui.Selectable("Cycle through all states", gauges.PreviewState is null)) {
                    gauges.PreviewState = null;
                }

                for (var i = 0; i < states.Count; i++) {
                    if (ImGui.Selectable(states[i], gauges.PreviewState == i)) {
                        gauges.PreviewState = i;
                    }
                }
            }
        }

        ImGui.Spacing();
    }

    private bool DrawJob(IJobGauge gauge)
    {
        using var id = ImRaii.PushId(gauge.Name);
        var c = gauge.Config;
        var changed = ImGui.Checkbox("##enabled", ref c.Enabled);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(c.Enabled ? $"{gauge.Name} gauge on" : $"{gauge.Name} gauge off");
        }

        ImGui.SameLine();
        if (!ImGui.CollapsingHeader(gauge.Name)) {
            return changed;
        }

        using var indent = ImRaii.PushIndent();
        using (ImRaii.Disabled(!c.Enabled)) {
            changed |= DrawCommon(gauge);
            changed |= gauge.DrawSettings();
        }

        ImGui.Spacing();
        return changed;
    }

    private static bool DrawCommon(IJobGauge gauge)
    {
        var c = gauge.Config;
        var changed = false;

        Widgets.SectionHeader("Display");
        changed |= ImGui.Checkbox("Only show in combat", ref c.OnlyInCombat);
        changed |= ImGui.Checkbox($"Hide the game's own {gauge.Name} gauge", ref c.HideGameGauge);
        Widgets.HelpMarker($"Hides {string.Join(" and ", gauge.GameGaugeNames)} while this gauge is on. " +
                           "Untick to see both.");

        Widgets.SectionHeader("Position and size");
        var size = c.Size * 100f;
        ImGui.SetNextItemWidth(Widgets.ItemWidth);
        if (ImGui.SliderFloat("Size", ref size, GaugeConfig.MinSize * 100f, GaugeConfig.MaxSize * 100f, "%.0f%%")) {
            c.Size = size / 100f;
            changed = true;
        }

        ImGui.SetNextItemWidth(Widgets.ItemWidth);
        changed |= Widgets.EnumCombo("Position", ref c.PositionMode, PositionModes);
        Widgets.HelpMarker("Free: anywhere you put it.\nFollow HUD Layout: where the game's own gauge is in the HUD " +
                           "Layout (even while hidden), scaled like it, then your size and offset on top.");

        if (c.PositionMode == PositionMode.Free) {
            var viewport = ImGuiHelpers.MainViewport;
            var position = c.Position ?? viewport.Pos + new Vector2(viewport.Size.X * 0.5f, viewport.Size.Y * 0.72f);
            ImGui.SetNextItemWidth(Widgets.ItemWidth);
            if (ImGui.DragFloat2("Centre (pixels)", ref position, 1f, -10000f, 10000f, "%.0f", ImGuiSliderFlags.None)) {
                c.Position = position;
                changed = true;
            }
        }
        else {
            if (gauge.GameGaugeNames.Count > 1) {
                var anchor = Math.Clamp(c.HudAnchor, 0, gauge.GameGaugeNames.Count - 1);
                ImGui.SetNextItemWidth(Widgets.ItemWidth);
                using var combo = ImRaii.Combo("Follow", gauge.GameGaugeNames[anchor]);
                if (combo) {
                    for (var i = 0; i < gauge.GameGaugeNames.Count; i++) {
                        if (ImGui.Selectable(gauge.GameGaugeNames[i], i == anchor) && i != anchor) {
                            c.HudAnchor = i;
                            changed = true;
                        }
                    }
                }
            }

            ImGui.SetNextItemWidth(Widgets.ItemWidth);
            changed |= ImGui.DragFloat2("Offset (pixels)", ref c.HudOffset, 1f, -5000f, 5000f, "%.0f", ImGuiSliderFlags.None);
        }

        if (ImGui.Button("Reset position")) {
            c.Position = null;
            c.HudOffset = Vector2.Zero;
            changed = true;
        }

        return changed;
    }

    internal void SaveIfNeeded(bool force)
    {
        // Wait until a slider is released rather than writing the file on every frame of a drag.
        if (unsaved && (force || !ImGui.IsAnyItemActive())) {
            config.Save();
            unsaved = false;
        }
    }
}
