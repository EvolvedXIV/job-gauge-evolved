using System.Numerics;
using JobGaugeEvolved.Configuration;

namespace JobGaugeEvolved.Gauges;

/// <summary>Where a gauge is on screen: its centre in pixels and its scale (pixels per gauge unit).</summary>
internal readonly record struct GaugePlacement(Vector2 Centre, float Scale)
{
    /// <summary>
    /// Works out the placement from the settings. <paramref name="hud"/> is the game gauge's HUD Layout
    /// centre and scale when known; without it, "Follow HUD Layout" falls back to the free position.
    /// </summary>
    /// <remarks>Only uses the BCL (plus <see cref="GaugeConfig"/>), so it is unit-tested.</remarks>
    public static GaugePlacement Resolve(GaugeConfig config, GaugePlacement? hud, Vector2 defaultCentre)
    {
        if (config.PositionMode == PositionMode.FollowHud && hud is { } h) {
            return new GaugePlacement(h.Centre + config.HudOffset, h.Scale * config.Size);
        }

        return new GaugePlacement(config.Position ?? defaultCentre, config.Size);
    }

    /// <summary>Moves the gauge by a mouse drag, changing whichever setting the current mode uses.</summary>
    public static void Drag(GaugeConfig config, Vector2 delta, bool followingHud, Vector2 currentCentre)
    {
        if (followingHud) {
            config.HudOffset += delta;
        }
        else {
            config.Position = currentCentre + delta;
        }
    }
}
