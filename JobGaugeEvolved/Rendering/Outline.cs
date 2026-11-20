using System;
using System.Collections.Generic;
using System.Numerics;

namespace JobGaugeEvolved.Rendering;

/// <summary>
/// Shapes from transparency: the outlines of a layer's opaque parts, and the geometry the highlights are built from
/// (rings around an outline, a filled outline).
/// </summary>
/// <remarks>
/// Outlines are closed polygons with a positive shoelace area in screen coordinates (y down), i.e. clockwise on
/// screen; the outward normal of an edge (dx, dy) is then (dy, -dx). Only uses the BCL, so it is unit-tested.
/// </remarks>
internal static class Outline
{
    /// <summary>
    /// The outer outlines of the parts of an image whose alpha is at least half, in pixels (pixel (x, y) covers
    /// x..x+1, y..y+1). Holes are left out; so are specks smaller than <paramref name="minArea"/> pixels.
    /// </summary>
    /// <param name="pixels">Rows of pixels, <paramref name="pitch"/> bytes apart.</param>
    /// <param name="alphaOffset">Byte of the alpha channel within a pixel (3 for RGBA and BGRA).</param>
    public static List<Vector2[]> Trace(ReadOnlySpan<byte> pixels, int width, int height, int pitch,
        int bytesPerPixel = 4, int alphaOffset = 3, float minArea = 4f)
    {
        // Samples at pixel centres, with a ring of empty samples around so every outline closes.
        var w = width + 2;
        var h = height + 2;
        var v = new float[w * h];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                v[(y + 1) * w + x + 1] = pixels[y * pitch + x * bytesPerPixel + alphaOffset] / 255f;
            }
        }

        // Marching squares: each cell gives up to two segments between crossings on its edges. Crossings are named
        // by edge (horizontal edge of sample (x, y): 2i, vertical edge: 2i + 1) so segments join exactly.
        var next = new Dictionary<int, int>();
        var position = new Dictionary<int, Vector2>();

        int HorizontalEdge(int x, int y) => 2 * (y * w + x);
        int VerticalEdge(int x, int y) => 2 * (y * w + x) + 1;

        Vector2 Sample(int x, int y) => new(x - 0.5f, y - 0.5f);

        Vector2 Crossing(Vector2 a, Vector2 b, float va, float vb)
        {
            var t = MathF.Abs(vb - va) < 1e-6f ? 0.5f : Math.Clamp((0.5f - va) / (vb - va), 0f, 1f);
            return Vector2.Lerp(a, b, t);
        }

        for (var y = 0; y < h - 1; y++) {
            for (var x = 0; x < w - 1; x++) {
                float tl = v[y * w + x], tr = v[y * w + x + 1], bl = v[(y + 1) * w + x], br = v[(y + 1) * w + x + 1];
                var index = (tl >= 0.5f ? 8 : 0) | (tr >= 0.5f ? 4 : 0) | (br >= 0.5f ? 2 : 0) | (bl >= 0.5f ? 1 : 0);
                if (index is 0 or 15) {
                    continue;
                }

                var top = HorizontalEdge(x, y);
                var bottom = HorizontalEdge(x, y + 1);
                var left = VerticalEdge(x, y);
                var right = VerticalEdge(x + 1, y);
                position.TryAdd(top, Crossing(Sample(x, y), Sample(x + 1, y), tl, tr));
                position.TryAdd(bottom, Crossing(Sample(x, y + 1), Sample(x + 1, y + 1), bl, br));
                position.TryAdd(left, Crossing(Sample(x, y), Sample(x, y + 1), tl, bl));
                position.TryAdd(right, Crossing(Sample(x + 1, y), Sample(x + 1, y + 1), tr, br));

                var centreInside = (tl + tr + bl + br) / 4f >= 0.5f;
                foreach (var (a, b) in Segments(index, centreInside, top, right, bottom, left)) {
                    // Orient every segment the same way relative to the alpha gradient, so outlines chain up.
                    var mid = (position[a] + position[b]) / 2f - Sample(x, y);
                    var gx = (tr - tl) * (1 - mid.Y) + (br - bl) * mid.Y;
                    var gy = (bl - tl) * (1 - mid.X) + (br - tr) * mid.X;
                    var d = position[b] - position[a];
                    var (from, to) = d.X * gy - d.Y * gx < 0 ? (a, b) : (b, a);
                    next[from] = to;
                }
            }
        }

        // Chain the segments into closed loops.
        var loops = new List<Vector2[]>();
        var used = new HashSet<int>();
        foreach (var start in next.Keys) {
            if (used.Contains(start)) {
                continue;
            }

            var loop = new List<Vector2>();
            var at = start;
            while (used.Add(at) && next.TryGetValue(at, out var to)) {
                loop.Add(position[at]);
                at = to;
            }

            if (loop.Count >= 3) {
                loops.Add(loop.ToArray());
            }
        }

        // The biggest loop is always an outer edge; holes wind the other way round.
        var outerSign = 0f;
        var biggest = 0f;
        foreach (var loop in loops) {
            var area = SignedArea(loop);
            if (MathF.Abs(area) > biggest) {
                biggest = MathF.Abs(area);
                outerSign = MathF.Sign(area);
            }
        }

        var outlines = new List<Vector2[]>();
        foreach (var loop in loops) {
            var area = SignedArea(loop);
            if (MathF.Sign(area) == outerSign && MathF.Abs(area) >= minArea) {
                outlines.Add(EnsurePositive(loop));
            }
        }

        return outlines;
    }

    private static IEnumerable<(int, int)> Segments(int index, bool centreInside, int top, int right, int bottom, int left)
    {
        // Corner bits: top left 8, top right 4, bottom right 2, bottom left 1.
        switch (index) {
            case 1 or 14: yield return (left, bottom); break;
            case 2 or 13: yield return (bottom, right); break;
            case 3 or 12: yield return (left, right); break;
            case 4 or 11: yield return (top, right); break;
            case 6 or 9: yield return (top, bottom); break;
            case 7 or 8: yield return (left, top); break;
            case 5: // top right and bottom left inside
                if (centreInside) {
                    yield return (left, top);
                    yield return (bottom, right);
                }
                else {
                    yield return (top, right);
                    yield return (left, bottom);
                }

                break;
            case 10: // top left and bottom right inside
                if (centreInside) {
                    yield return (top, right);
                    yield return (left, bottom);
                }
                else {
                    yield return (left, top);
                    yield return (bottom, right);
                }

                break;
        }
    }

    /// <summary>Shoelace area: positive when the points go clockwise on screen (y down).</summary>
    public static float SignedArea(IReadOnlyList<Vector2> points)
    {
        var sum = 0f;
        for (var i = 0; i < points.Count; i++) {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }

        return sum / 2f;
    }

    /// <summary>
    /// Area divided by perimeter: about half the thickness of a long strip, half the radius of a disc. How far inside
    /// the outline a glow can start without reaching the far side.
    /// </summary>
    public static float AreaOverPerimeter(IReadOnlyList<Vector2> points)
    {
        var perimeter = 0f;
        for (var i = 0; i < points.Count; i++) {
            perimeter += Vector2.Distance(points[i], points[(i + 1) % points.Count]);
        }

        return perimeter > 0f ? MathF.Abs(SignedArea(points)) / perimeter : 0f;
    }

    public static Vector2[] EnsurePositive(IReadOnlyList<Vector2> points)
    {
        var copy = new Vector2[points.Count];
        for (var i = 0; i < copy.Length; i++) {
            copy[i] = points[i];
        }

        if (SignedArea(copy) < 0) {
            Array.Reverse(copy);
        }

        return copy;
    }

    /// <summary>Drops points that stray less than <paramref name="epsilon"/> from a straight line (Ramer–Douglas–Peucker).</summary>
    public static Vector2[] Simplify(Vector2[] loop, float epsilon)
    {
        if (loop.Length <= 4) {
            return loop;
        }

        // Split the loop at its two farthest-apart points (approximately) and simplify both halves.
        var far = 0;
        var farDistance = 0f;
        for (var i = 1; i < loop.Length; i++) {
            var d = Vector2.DistanceSquared(loop[0], loop[i]);
            if (d > farDistance) {
                farDistance = d;
                far = i;
            }
        }

        var keep = new bool[loop.Length];
        keep[0] = keep[far] = true;
        Mark(loop, 0, far, epsilon, keep);
        Mark(loop, far, loop.Length, epsilon, keep);

        var result = new List<Vector2>();
        for (var i = 0; i < loop.Length; i++) {
            if (keep[i]) {
                result.Add(loop[i]);
            }
        }

        return result.Count >= 3 ? result.ToArray() : loop;
    }

    private static void Mark(Vector2[] loop, int first, int last, float epsilon, bool[] keep)
    {
        // last may be loop.Length, meaning point 0 again.
        var a = loop[first];
        var b = loop[last % loop.Length];
        var worst = -1;
        var worstDistance = epsilon;
        for (var i = first + 1; i < last; i++) {
            var d = DistanceToSegment(loop[i], a, b);
            if (d > worstDistance) {
                worstDistance = d;
                worst = i;
            }
        }

        if (worst >= 0) {
            keep[worst] = true;
            Mark(loop, first, worst, epsilon, keep);
            Mark(loop, worst, last, epsilon, keep);
        }
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var length = ab.LengthSquared();
        var t = length < 1e-12f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / length, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>The outline reflected around x = width / 2, still with a positive area.</summary>
    public static Vector2[] Mirror(IReadOnlyList<Vector2> points, float width)
    {
        var result = new Vector2[points.Count];
        for (var i = 0; i < result.Length; i++) {
            var p = points[result.Length - 1 - i];
            result[i] = new Vector2(width - p.X, p.Y);
        }

        return result;
    }

    /// <summary>
    /// Outward direction at every point, scaled so an offset ring keeps a constant distance from straight edges (the
    /// scale is capped at 2 at sharp corners).
    /// </summary>
    public static Vector2[] OffsetDirections(IReadOnlyList<Vector2> points)
    {
        var n = points.Count;
        var result = new Vector2[n];
        for (var i = 0; i < n; i++) {
            var prev = points[(i + n - 1) % n];
            var here = points[i];
            var next = points[(i + 1) % n];
            var n1 = EdgeNormal(prev, here);
            var n2 = EdgeNormal(here, next);
            var sum = n1 + n2;
            if (sum.LengthSquared() < 1e-8f) {
                result[i] = n1;
                continue;
            }

            var dir = Vector2.Normalize(sum);
            var cos = Vector2.Dot(dir, n1);
            result[i] = dir / MathF.Max(cos, 0.5f);
        }

        return result;
    }

    private static Vector2 EdgeNormal(Vector2 a, Vector2 b)
    {
        var d = b - a;
        var length = d.Length();
        return length < 1e-6f ? Vector2.Zero : new Vector2(d.Y, -d.X) / length;
    }

    /// <summary>The ring at <paramref name="distance"/> outside the outline.</summary>
    public static Vector2[] Offset(IReadOnlyList<Vector2> points, IReadOnlyList<Vector2> directions, float distance)
    {
        var result = new Vector2[points.Count];
        for (var i = 0; i < result.Length; i++) {
            result[i] = points[i] + directions[i] * distance;
        }

        return result;
    }

    /// <summary>Triangles filling the outline (ear clipping), as indices into it.</summary>
    public static int[] Triangulate(IReadOnlyList<Vector2> points)
    {
        var remaining = new List<int>();
        for (var i = 0; i < points.Count; i++) {
            remaining.Add(i);
        }

        var triangles = new List<int>();
        var guard = points.Count * points.Count;
        while (remaining.Count > 3 && guard-- > 0) {
            var clipped = false;
            for (var i = 0; i < remaining.Count; i++) {
                var ia = remaining[(i + remaining.Count - 1) % remaining.Count];
                var ib = remaining[i];
                var ic = remaining[(i + 1) % remaining.Count];
                if (!IsEar(points, remaining, ia, ib, ic)) {
                    continue;
                }

                triangles.Add(ia);
                triangles.Add(ib);
                triangles.Add(ic);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped) {
                // Degenerate (self-touching) outline: clip the next vertex anyway rather than loop forever.
                triangles.Add(remaining[^1]);
                triangles.Add(remaining[0]);
                triangles.Add(remaining[1]);
                remaining.RemoveAt(0);
            }
        }

        if (remaining.Count == 3) {
            triangles.AddRange(remaining);
        }

        return triangles.ToArray();
    }

    private static bool IsEar(IReadOnlyList<Vector2> p, List<int> remaining, int ia, int ib, int ic)
    {
        Vector2 a = p[ia], b = p[ib], c = p[ic];
        if (Cross(b - a, c - b) <= 0) {
            return false; // reflex (or straight) corner for a positive-area outline
        }

        foreach (var j in remaining) {
            if (j != ia && j != ib && j != ic && InTriangle(p[j], a, b, c)) {
                return false;
            }
        }

        return true;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
        Cross(b - a, p - a) >= 0 && Cross(c - b, p - b) >= 0 && Cross(a - c, p - c) >= 0;
}
