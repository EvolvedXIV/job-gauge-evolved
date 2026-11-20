using System.Collections.Generic;
using System.Numerics;
using JobGaugeEvolved.Rendering;

namespace JobGaugeEvolved.Native;

/// <summary>A loaded game addon, as far as covering a gauge goes.</summary>
/// <param name="Framed">Has a window frame (a <c>WindowNode</c>): a real window such as Character or Duty Finder.</param>
internal readonly record struct GameUnit(string Name, int Layer, bool Framed, bool Visible, ScreenRect Rect);

/// <summary>A game window that covers a gauge, for drawing around it and for the debug window.</summary>
internal readonly record struct CoveringWindow(string Name, int Layer, ScreenRect Rect);

/// <summary>Whether a game unit covers the gauge, or why not (shown in the debug window).</summary>
internal enum CoverVerdict
{
    Covers,
    Hidden,
    OwnGauge,
    Ignored,
    Empty,
    FullScreen,

    /// <summary>No window frame and not in a depth layer above the gauge's: a HUD element beside it.</summary>
    NotAboveGauge,
}

/// <summary>
/// Which game units are drawn over a job gauge. The game draws real windows (those with a frame) over its HUD, so
/// they always cover it, whatever their depth layer or place in the layer's list (neither says how windows stack over
/// the HUD: the first version relied on them and nothing was ever covered). Units without a frame, such as tooltips
/// and context menus, cover it only from a depth layer above the gauge's (unverified).
/// </summary>
/// <remarks>Only uses the BCL, so it is unit-tested.</remarks>
internal static class CoverRules
{
    /// <summary>Anything covering this much of the screen is a fade or full-screen overlay, not a window.</summary>
    public const float FullScreenShare = 0.9f;

    /// <param name="gaugeLayer">The depth layer of the job's own gauge addon, or null when it wasn't found.</param>
    /// <param name="gaugeAddons">The job's own gauge addons, which never cover the replacement.</param>
    /// <param name="screen">The screen size in pixels, or zero if not known yet.</param>
    public static CoverVerdict Judge(
        in GameUnit unit, int? gaugeLayer, IReadOnlySet<string> gaugeAddons, IReadOnlySet<string> ignored, Vector2 screen)
    {
        if (gaugeAddons.Contains(unit.Name)) {
            return CoverVerdict.OwnGauge;
        }

        if (!unit.Visible) {
            return CoverVerdict.Hidden;
        }

        if (ignored.Contains(unit.Name)) {
            return CoverVerdict.Ignored;
        }

        if (unit.Rect.IsEmpty) {
            return CoverVerdict.Empty;
        }

        if (screen.X > 0f && screen.Y > 0f && unit.Rect.Area >= screen.X * screen.Y * FullScreenShare) {
            return CoverVerdict.FullScreen;
        }

        if (unit.Framed || gaugeLayer is { } layer && unit.Layer > layer) {
            return CoverVerdict.Covers;
        }

        return CoverVerdict.NotAboveGauge;
    }

    /// <summary>The units that cover the gauge.</summary>
    public static CoveringWindow[] Select(
        IEnumerable<GameUnit> units, int? gaugeLayer, IReadOnlySet<string> gaugeAddons, IReadOnlySet<string> ignored,
        Vector2 screen)
    {
        var found = new List<CoveringWindow>();
        foreach (var unit in units) {
            if (Judge(unit, gaugeLayer, gaugeAddons, ignored, screen) == CoverVerdict.Covers) {
                found.Add(new CoveringWindow(unit.Name, unit.Layer, unit.Rect));
            }
        }

        return found.ToArray();
    }
}
