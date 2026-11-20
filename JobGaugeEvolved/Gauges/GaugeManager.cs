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
/// Picks the gauge for the current job, reads the game for it 20 times a second, hides the game's own gauge, and
/// draws the overlay every frame from the last reading.
/// </summary>
internal sealed class GaugeManager : IDisposable
{
    private readonly PluginConfiguration config;
    private readonly NativeGaugeHider hider;
    private readonly GaugeText text = new();
    private readonly Dictionary<(IJobGauge, string), GaugePlacement> lastHud = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    // Reading the game 20 times a second is plenty; drawing still happens every frame.
    private readonly UpdateThrottle reads = new(TimeSpan.FromMilliseconds(50));

    // Game windows are looked at every frame, so a window dragged over the gauge cuts it smoothly.
    private readonly WindowOcclusion windows = new();

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
    public bool Unlocked
    {
        get;
        set { field = value; reads.RunNext(); }
    }

    /// <summary>Shows <see cref="PreviewGauge"/> with made-up data, on any job (set by the settings window).</summary>
    public bool Preview
    {
        get;
        set { field = value; reads.RunNext(); }
    }

    public IJobGauge? PreviewGauge
    {
        get;
        set { field = value; reads.RunNext(); }
    }

    /// <summary>The previewed state, or null to cycle through all of them.</summary>
    public int? PreviewState
    {
        get;
        set { field = value; reads.RunNext(); }
    }

    /// <summary>Makes a changed setting (e.g. a gauge turned on or off) apply without waiting for the next read.</summary>
    public void RequestRead() => reads.RunNext();

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
        AdditiveBlend.Dispose();
        foreach (var gauge in Gauges) {
            gauge.Dispose();
        }
    }

    /// <summary>The game windows covering the shown gauge (for the debug window).</summary>
    public WindowOcclusion Windows => windows;

    private void OnUpdate(IFramework framework)
    {
        try {
            if (visible is { Config.HideBehindWindows: true } shown) {
                windows.Update(shown.GameAddons[0], config.IgnoredWindowNames);
            }
        }
        catch (Exception e) {
            Service.Log.Error(e, "Looking at the game's windows failed");
        }

        if (!reads.ShouldRun(clock.Elapsed)) {
            return;
        }

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

        // The window also covers the glows, which reach beyond the art; dragging uses the art alone.
        var reach = gauge.Config.GlowReach + 2f;
        var art = (Min: origin + bounds.Min * placement.Scale, Size: bounds.Size * placement.Scale);
        bounds = new ArtBox(bounds.X - reach, bounds.Y - reach, bounds.W + 2 * reach, bounds.H + 2 * reach);
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
                var canvas = new GaugeCanvas(drawList, gauge.Art, origin, placement.Scale);
                var time = ImGui.GetTime();

                // Drawn once per piece left uncovered by game windows, clipped to it.
                foreach (var piece in VisiblePieces(gauge, new ScreenRect(topLeft, topLeft + size), viewport.Size)) {
                    drawList.PushClipRect(piece.Min, piece.Max, true);
                    gauge.Draw(canvas, text, time);
                    drawList.PopClipRect();
                }

                if (Unlocked) {
                    ImGui.SetCursorScreenPos(art.Min);
                    ImGui.InvisibleButton("##move", art.Size, ImGuiButtonFlags.MouseButtonLeft);
                    var active = ImGui.IsItemActive();
                    if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                        GaugePlacement.Drag(gauge.Config, ImGui.GetIO().MouseDelta, following, placement.Centre);
                        MovedSinceLastCheck = true;
                    }

                    var outline = active || ImGui.IsItemHovered() ? 0xFF5EB2D6u : 0x995EB2D6u; // gold
                    drawList.AddRect(art.Min, art.Min + art.Size, outline, 4f, ImDrawFlags.None, 2f);
                    drawList.AddText(art.Min + new Vector2(6, 4), 0xFFFFFFFFu, $"{gauge.Name}: drag to move");
                }
            }
        }
        finally {
            ImGui.End();
            ImGui.PopStyleVar(3);
        }
    }

    /// <summary>The parts of the gauge's window that no game window covers.</summary>
    private List<ScreenRect> VisiblePieces(IJobGauge gauge, ScreenRect window, Vector2 screen)
    {
        if (!gauge.Config.HideBehindWindows || windows.Covering.Count == 0) {
            return [window];
        }

        var covers = new List<ScreenRect>();
        foreach (var w in windows.Covering) {
            // Something covering nearly the whole screen is a full-screen overlay, not a window.
            if (w.Rect.Overlaps(window) && w.Rect.Area < screen.X * screen.Y * 0.9f) {
                covers.Add(w.Rect);
            }
        }

        return covers.Count == 0 ? [window] : RectCut.Visible(window, covers);
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
