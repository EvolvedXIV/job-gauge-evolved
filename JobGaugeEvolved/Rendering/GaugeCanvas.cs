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
    /// <param name="mirror">Reflect it around the middle of the canvas (the right blade).</param>
    /// <param name="cropRight">Draw only this fraction of it, from the left (the liquid's fill level).</param>
    /// <param name="offset">Move it by this many units (gem and coil slots).</param>
    public void Layer(string name, ColorSpec? color = null, float alpha = 1f, bool mirror = false, float cropRight = 1f,
        Vector2 offset = default)
    {
        cropRight = Math.Clamp(cropRight, 0f, 1f);
        alpha = Math.Clamp(alpha, 0f, 1f);
        if (cropRight <= 0f || alpha <= 0f || !atlas.TryGetTexture(name, resolution, out var texture)) {
            return;
        }

        var box = atlas.Layout.Layer(name).Box.Offset(offset);
        if (mirror) {
            box = box.Mirror(atlas.Layout.Width);
        }

        var min = ToScreen(box.Min);
        var max = min + new Vector2(box.W * cropRight, box.H) * Scale;

        // Texture coordinates: crop keeps the left part; a mirrored layer reads the texture right to left.
        var u0 = mirror ? 1f : 0f;
        var u1 = mirror ? 1f - cropRight : cropRight;

        var (tl, tr, br, bl) = color?.Corners(0f, cropRight) ?? (White, White, White, White);

        drawList.PushTextureID(texture.Handle);
        drawList.PrimReserve(6, 4);
        var index = drawList.VtxCurrentIdx;
        drawList.PrimWriteIdx((ushort)index);
        drawList.PrimWriteIdx((ushort)(index + 1));
        drawList.PrimWriteIdx((ushort)(index + 2));
        drawList.PrimWriteIdx((ushort)index);
        drawList.PrimWriteIdx((ushort)(index + 2));
        drawList.PrimWriteIdx((ushort)(index + 3));
        drawList.PrimWriteVtx(min, new Vector2(u0, 0f), ColorSpec.Pack(tl, alpha));
        drawList.PrimWriteVtx(new Vector2(max.X, min.Y), new Vector2(u1, 0f), ColorSpec.Pack(tr, alpha));
        drawList.PrimWriteVtx(max, new Vector2(u1, 1f), ColorSpec.Pack(br, alpha));
        drawList.PrimWriteVtx(new Vector2(min.X, max.Y), new Vector2(u0, 1f), ColorSpec.Pack(bl, alpha));
        drawList.PopTextureID();
    }

    /// <summary>Text centred on a point, <paramref name="height"/> units tall, with an outline.</summary>
    public void Text(GaugeText text, string value, Vector2 centre, float height)
    {
        text.Draw(drawList, value, ToScreen(centre), height * Scale);
    }
}
