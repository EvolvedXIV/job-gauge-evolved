using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using JobGaugeEvolved.Configuration;

namespace JobGaugeEvolved.Rendering;

/// <summary>
/// Draws art layers into an ImGui draw list, in gauge units: <see cref="Origin"/> is where unit (0, 0) lands on
/// screen and <see cref="Scale"/> how many pixels one unit is.
/// </summary>
/// <remarks>
/// Each layer is a textured quad whose four corners carry a colour. ImGui multiplies the texture by that colour,
/// so white mask art takes the colour setting exactly, and a gradient is just different corner colours.
/// </remarks>
internal readonly struct GaugeCanvas
{
    private static readonly Vector4 White = Vector4.One;

    private readonly ImDrawListPtr drawList;
    private readonly ArtAtlas atlas;
    private readonly int resolution;

    public GaugeCanvas(ImDrawListPtr drawList, ArtAtlas atlas, Vector2 origin, float scale)
    {
        this.drawList = drawList;
        this.atlas = atlas;
        Origin = origin;
        Scale = scale;
        resolution = atlas.Layout.PickScale(scale);
    }

    public Vector2 Origin { get; }
    public float Scale { get; }
    public ArtLayout Layout => atlas.Layout;

    public Vector2 ToScreen(Vector2 units) => Origin + units * Scale;

    /// <summary>Draws a layer.</summary>
    /// <param name="name">Layer name from layout.json.</param>
    /// <param name="color">Colour for mask layers (null draws the art as is).</param>
    /// <param name="alpha">Opacity, multiplied into the colour.</param>
    /// <param name="mirror">Reflect it around the middle of the canvas (the right half of the twinblade).</param>
    /// <param name="cropTop">Leave out this fraction of it from the top (a level filling from the bottom).</param>
    /// <param name="offset">Move it by this many units (stone slots, the liquid surface).</param>
    /// <param name="widthScale">Narrow or widen it around its centre (the liquid surface inside the orb).</param>
    public void Layer(string name, ColorSpec? color = null, float alpha = 1f, bool mirror = false, float cropTop = 0f,
        Vector2 offset = default, float widthScale = 1f)
    {
        cropTop = Math.Clamp(cropTop, 0f, 1f);
        alpha = Math.Clamp(alpha, 0f, 1f);
        if (cropTop >= 1f || alpha <= 0f || widthScale <= 0f || !atlas.TryGetTexture(name, resolution, out var texture)) {
            return;
        }

        var box = atlas.Layout.Layer(name).Box.Offset(offset);
        if (mirror) {
            box = box.Mirror(atlas.Layout.Width);
        }

        if (widthScale != 1f) {
            var width = box.W * widthScale;
            box = box with { X = box.X + (box.W - width) / 2f, W = width };
        }

        var min = ToScreen(new Vector2(box.X, box.Y + box.H * cropTop));
        var max = ToScreen(new Vector2(box.Right, box.Bottom));

        // Texture coordinates: a crop keeps the bottom part; a mirrored layer reads the texture right to left.
        var u0 = mirror ? 1f : 0f;
        var u1 = mirror ? 0f : 1f;
        var v0 = cropTop;

        // The colour is spread over the whole layer, so a cropped layer shows its slice of a gradient, and a
        // mirrored layer mirrors a left-to-right gradient too (tip to hilt on both blades).
        var (tl, tr, br, bl) = color?.Corners(0f, 1f, cropTop, 1f, mirror) ?? (White, White, White, White);

        drawList.PushTextureID(texture.Handle);
        drawList.PrimReserve(6, 4);
        var index = drawList.VtxCurrentIdx;
        drawList.PrimWriteIdx((ushort)index);
        drawList.PrimWriteIdx((ushort)(index + 1));
        drawList.PrimWriteIdx((ushort)(index + 2));
        drawList.PrimWriteIdx((ushort)index);
        drawList.PrimWriteIdx((ushort)(index + 2));
        drawList.PrimWriteIdx((ushort)(index + 3));
        drawList.PrimWriteVtx(min, new Vector2(u0, v0), ColorSpec.Pack(tl, alpha));
        drawList.PrimWriteVtx(new Vector2(max.X, min.Y), new Vector2(u1, v0), ColorSpec.Pack(tr, alpha));
        drawList.PrimWriteVtx(max, new Vector2(u1, 1f), ColorSpec.Pack(br, alpha));
        drawList.PrimWriteVtx(new Vector2(min.X, max.Y), new Vector2(u0, 1f), ColorSpec.Pack(bl, alpha));
        drawList.PopTextureID();
    }

    /// <summary>
    /// Fills a layer's outline with a colour added to what is underneath at <paramref name="strength"/> (0.5 is like
    /// GIMP's Addition mode at 50%), so the texture shows through.
    /// </summary>
    public void Tint(string layer, ColorSpec color, float strength, bool additive, bool mirror = false, Vector2 offset = default)
    {
        strength = Math.Clamp(strength, 0f, 1f);
        if (strength <= 0f || !atlas.Shapes.TryGet(layer, mirror, out var shape)) {
            return;
        }

        additive = BeginBlend(additive);
        var white = ImGui.GetFontTexUvWhitePixel();
        for (var k = 0; k < shape.Outlines.Length; k++) {
            var points = shape.Outlines[k];
            var triangles = shape.Triangles[k];
            drawList.PrimReserve(triangles.Length, points.Length);
            var first = drawList.VtxCurrentIdx;
            foreach (var index in triangles) {
                drawList.PrimWriteIdx((ushort)(first + index));
            }

            foreach (var p in points) {
                drawList.PrimWriteVtx(ToScreen(p + offset), white, ColorSpec.Pack(ColorAt(color, shape, p, mirror), strength));
            }

            // A one-pixel fade outside the edge keeps it smooth.
            Strip(shape, k, color, mirror, offset, 0f, 1f / Scale, strength, 0f);
        }

        EndBlend(additive);
    }

    /// <summary>
    /// A glow around a layer's outline, pulsing as <paramref name="style"/> says: it comes out from behind the shape
    /// (drawn before the art, which hides the part still inside), drifts out until there is a gap, fades and starts
    /// again. <paramref name="size"/> scales its width and gap (small parts such as stones glow at half size).
    /// </summary>
    public void Glow(string layer, ColorSpec color, GlowStyle style, double time, float strength = 1f, bool mirror = false,
        Vector2 offset = default, float size = 1f)
    {
        var width = style.Width * size;
        var (_, pulse) = GlowPulse.At(time, style.Period, style.Gap * size, style.Pulse);
        var alpha = Math.Clamp(style.Intensity * strength * pulse, 0f, 1f);
        if (alpha <= 0.004f || !atlas.Shapes.TryGet(layer, mirror, out var shape)) {
            return;
        }

        var additive = BeginBlend(style.Additive);
        for (var k = 0; k < shape.Outlines.Length; k++) {
            // Starts inside this outline, but never further in than about half its thickness.
            var inset = MathF.Min(shape.Insets[k], width);
            var (gap, _) = GlowPulse.At(time, style.Period, style.Gap * size, style.Pulse, inset);
            var (inner, peak, outer) = GlowPulse.Band(gap, width);
            Strip(shape, k, color, mirror, offset, inner, peak, 0f, alpha);
            Strip(shape, k, color, mirror, offset, peak, outer, alpha, 0f);
        }

        EndBlend(additive);
    }

    /// <summary>A ring between two distances outside an outline, its alpha fading from one edge to the other.</summary>
    private void Strip(Shape shape, int k, ColorSpec color, bool mirror, Vector2 offset, float from, float to,
        float alphaFrom, float alphaTo)
    {
        var points = shape.Outlines[k];
        var directions = shape.Directions[k];
        var n = points.Length;
        var white = ImGui.GetFontTexUvWhitePixel();

        drawList.PrimReserve(6 * n, 2 * n);
        var first = drawList.VtxCurrentIdx;
        for (var i = 0; i < n; i++) {
            var j = (i + 1) % n;
            drawList.PrimWriteIdx((ushort)(first + i));
            drawList.PrimWriteIdx((ushort)(first + n + i));
            drawList.PrimWriteIdx((ushort)(first + n + j));
            drawList.PrimWriteIdx((ushort)(first + i));
            drawList.PrimWriteIdx((ushort)(first + n + j));
            drawList.PrimWriteIdx((ushort)(first + j));
        }

        for (var i = 0; i < n; i++) {
            var p = points[i] + directions[i] * from;
            drawList.PrimWriteVtx(ToScreen(p + offset), white, ColorSpec.Pack(ColorAt(color, shape, p, mirror), alphaFrom));
        }

        for (var i = 0; i < n; i++) {
            var p = points[i] + directions[i] * to;
            drawList.PrimWriteVtx(ToScreen(p + offset), white, ColorSpec.Pack(ColorAt(color, shape, p, mirror), alphaTo));
        }
    }

    private static Vector4 ColorAt(ColorSpec color, Shape shape, Vector2 p, bool mirror)
    {
        var b = shape.Bounds;
        var across = b.W > 0 ? Math.Clamp((p.X - b.X) / b.W, 0f, 1f) : 0f;
        var down = b.H > 0 ? Math.Clamp((p.Y - b.Y) / b.H, 0f, 1f) : 0f;
        return color.AtPosition(across, down, mirror);
    }

    /// <summary>Starts additive blending if asked for and available; returns whether it did.</summary>
    private bool BeginBlend(bool additive)
    {
        if (!additive || !AdditiveBlend.Available) {
            return false;
        }

        AdditiveBlend.Begin(drawList);
        return true;
    }

    private void EndBlend(bool additive)
    {
        if (additive) {
            AdditiveBlend.End(drawList);
        }
    }

    /// <summary>Text centred on a point, <paramref name="height"/> units tall, with an outline.</summary>
    public void Text(GaugeText text, string value, Vector2 centre, float height)
    {
        text.Draw(drawList, value, ToScreen(centre), height * Scale);
    }
}
