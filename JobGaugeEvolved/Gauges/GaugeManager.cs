using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using JobGaugeEvolved.Configuration;
using JobGaugeEvolved.Gauges.Viper;
using JobGaugeEvolved.Native;
using JobGaugeEvolved.Rendering;

namespace JobGaugeEvolved.Gauges;

/// <summary>
/// Picks the gauge for the current job, reads the game for it every frame, hides the game's own gauge, and draws
/// the overlay.
/// </summary>
internal sealed class GaugeManager : IDisposable
{
    private readonly PluginConfiguration config;
    private readonly NativeGaugeHider hider;
    private readonly GaugeText text = new();
    private readonly Dictionary<(IJobGauge, string), GaugePlacement> lastHud = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    private IJobGauge? visible;

    public GaugeManager(PluginConfiguration config)
    {
        this.config = config;
        Gauges = [new ViperGauge(config.Viper)];
        hider = new NativeGaugeHider(Gauges.SelectMany(g => g.GameAddons));

        Service.Framework.Update += OnUpdate;
        Service.PluginInterface.UiBuilder.Draw += OnDraw;
    }

    public IReadOnlyList<IJobGauge> Gauges { get; }

    /// <summary>Lets the visible gauge be dragged with the mouse (set by the settings window).</summary>
    public bool Unlocked { get; set; }

    /// <summary>Shows <see cref="PreviewGauge"/> with made-up data, on any job (set by the settings window).</summary>
    public bool Preview { get; set; }

    public IJobGauge? PreviewGauge { get; set; }

    /// <summary>The previewed state, or null to cycle through all of them.</summary>
    public int? PreviewState { get; set; }

    /// <summary>Set when a drag changed a position, so the settings window saves it.</summary>
    public bool MovedSinceLastCheck { get; set; }

    /// <summary>The gauge for the current job, whether or not it is shown right now.</summary>
    public IJobGauge? Current { get; private set; }

    public void Dispose()
    {
        Service.PluginInterface.UiBuilder.Draw -= OnDraw;
        Service.Framework.Update -= OnUpdate;
        hider.Dispose();
        text.Dispose();
        foreach (var gauge in Gauges) {
            gauge.Dispose();
        }
    }

    private void OnUpdate(IFramework framework)
    {
        try {
            var job = Service.PlayerState.IsLoaded ? Service.PlayerState.ClassJob.RowId : 0;
            var loggedIn = Service.ClientState.IsLoggedIn && Service.PlayerState.IsLoaded;
            var pvp = Service.ClientState.IsPvP;

            Current = null;
            foreach (var gauge in Gauges) {
                var mine = loggedIn && !pvp && gauge.JobId == job && gauge.Config.Enabled;
                if (mine) {
                    Current = gauge;
                }

                foreach (var addon in gauge.GameAddons) {
                    hider.SetHidden(addon, mine && gauge.Config.HideGameGauge);
                }
            }

            if (Preview && PreviewGauge is { } preview && loggedIn) {
                var count = Math.Max(1, preview.PreviewStates.Count);
                preview.Update(PreviewState ?? (int)(clock.Elapsed.TotalSeconds / 2.0) % count);
                visible = preview.HasData ? preview : null;
                return;
            }

            visible = null;
            if (Current is { } current) {
                current.Update(null);
                var inCombat = Service.Condition[ConditionFlag.InCombat];
                if (current.HasData && (!current.Config.OnlyInCombat || inCombat || Unlocked)) {
                    visible = current;
                }
            }
        }
        catch (Exception e) {
            visible = null;
            Service.Log.Error(e, "Gauge update failed");
        }
    }

    private void OnDraw()
    {
        if (visible is not { } gauge) {
            return;
        }

        try {
            DrawOverlay(gauge);
        }
        catch (Exception e) {
            Service.Log.Error(e, "Gauge drawing failed");
        }
    }

    private void DrawOverlay(IJobGauge gauge)
    {
        var hud = HudPlacement(gauge);
        var following = gauge.Config.PositionMode == PositionMode.FollowHud && hud is not null;
        var viewport = ImGuiHelpers.MainViewport;
        var defaultCentre = viewport.Pos + new Vector2(viewport.Size.X * 0.5f, viewport.Size.Y * 0.72f);
        var placement = GaugePlacement.Resolve(gauge.Config, hud, defaultCentre);

        var bounds = gauge.Bounds;
        var origin = placement.Centre - gauge.Centre * placement.Scale;    // screen position of unit (0, 0)
        var topLeft = origin + bounds.Min * placement.Scale;
        var size = bounds.Size * placement.Scale;

        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus |
                    ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoMove;
        if (!Unlocked) {
            flags |= ImGuiWindowFlags.NoInputs;
        }

        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowPos(topLeft);
        ImGui.SetNextWindowSize(size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        try {
            if (ImGui.Begin($"###JobGaugeEvolved{gauge.Name}", flags)) {
                var drawList = ImGui.GetWindowDrawList();
                gauge.Draw(new GaugeCanvas(drawList, gauge.Art, origin, placement.Scale), text, ImGui.GetTime());

                if (Unlocked) {
                    ImGui.SetCursorScreenPos(topLeft);
                    ImGui.InvisibleButton("##move", size, ImGuiButtonFlags.MouseButtonLeft);
                    var active = ImGui.IsItemActive();
                    if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                        GaugePlacement.Drag(gauge.Config, ImGui.GetIO().MouseDelta, following, placement.Centre);
                        MovedSinceLastCheck = true;
                    }

                    var outline = active || ImGui.IsItemHovered() ? 0xFF5EB2D6u : 0x995EB2D6u; // gold
                    drawList.AddRect(topLeft, topLeft + size, outline, 4f, ImDrawFlags.None, 2f);
                    drawList.AddText(topLeft + new Vector2(6, 4), 0xFFFFFFFFu, $"{gauge.Name}: drag to move");
                }
            }
        }
        finally {
            ImGui.End();
            ImGui.PopStyleVar(3);
        }
    }

    /// <summary>The game gauge's HUD Layout centre and scale, remembered while the addon is gone.</summary>
    private GaugePlacement? HudPlacement(IJobGauge gauge)
    {
        if (gauge.Config.PositionMode != PositionMode.FollowHud) {
            return null;
        }

        var name = gauge.GameAddons[Math.Clamp(gauge.Config.HudAnchor, 0, gauge.GameAddons.Count - 1)];
        var addon = Service.GameGui.GetAddonByName(name);
        var key = (gauge, name);
        if (!addon.IsNull && addon.IsReady && addon.Scale > 0f) {
            lastHud[key] = new GaugePlacement(addon.Position + addon.ScaledSize / 2f, addon.Scale);
        }

        return lastHud.TryGetValue(key, out var placement) ? placement : null;
    }
}
