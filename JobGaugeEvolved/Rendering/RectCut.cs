using System;
using System.Collections.Generic;
using System.Numerics;

namespace JobGaugeEvolved.Rendering;

/// <summary>An axis-aligned rectangle on screen, in pixels.</summary>
internal readonly record struct ScreenRect(Vector2 Min, Vector2 Max)
{
    public float Width => MathF.Max(0f, Max.X - Min.X);
    public float Height => MathF.Max(0f, Max.Y - Min.Y);
    public float Area => Width * Height;
    public bool IsEmpty => Width <= 0f || Height <= 0f;

    public bool Overlaps(ScreenRect other) =>
        Min.X < other.Max.X && other.Min.X < Max.X && Min.Y < other.Max.Y && other.Min.Y < Max.Y;

    public ScreenRect Intersect(ScreenRect other) => new(Vector2.Max(Min, other.Min), Vector2.Min(Max, other.Max));
}

/// <summary>
/// What is left of a rectangle once other rectangles are cut out of it, as disjoint pieces. Used to draw the gauge
/// only where no game window covers it.
/// </summary>
/// <remarks>Only uses the BCL, so it is unit-tested.</remarks>
internal static class RectCut
{
    /// <summary>More pieces than this and further cuts are skipped (the gauge then shows over those windows).</summary>
    public const int MaxPieces = 16;

    public static List<ScreenRect> Visible(ScreenRect area, IEnumerable<ScreenRect> covers)
    {
        var pieces = new List<ScreenRect>();
        if (!area.IsEmpty) {
            pieces.Add(area);
        }

        foreach (var cover in covers) {
            if (pieces.Count == 0) {
                break;
            }

            var next = new List<ScreenRect>(pieces.Count + 3);
            foreach (var piece in pieces) {
                if (cover.IsEmpty || !piece.Overlaps(cover)) {
                    next.Add(piece);
                    continue;
                }

                // Up to four pieces around the cut: a full-width band above and below, the sides in between.
                var top = MathF.Max(piece.Min.Y, cover.Min.Y);
                var bottom = MathF.Min(piece.Max.Y, cover.Max.Y);
                Add(next, new ScreenRect(piece.Min, new Vector2(piece.Max.X, top)));
                Add(next, new ScreenRect(new Vector2(piece.Min.X, bottom), piece.Max));
                Add(next, new ScreenRect(new Vector2(piece.Min.X, top), new Vector2(MathF.Max(piece.Min.X, cover.Min.X), bottom)));
                Add(next, new ScreenRect(new Vector2(MathF.Min(piece.Max.X, cover.Max.X), top), new Vector2(piece.Max.X, bottom)));
            }

            if (next.Count > MaxPieces) {
                break;
            }

            pieces = next;
        }

        return pieces;
    }

    private static void Add(List<ScreenRect> pieces, ScreenRect piece)
    {
        if (!piece.IsEmpty) {
            pieces.Add(piece);
        }
    }
}
