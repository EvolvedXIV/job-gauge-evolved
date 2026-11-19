using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;

namespace JobGaugeEvolved.Rendering;

/// <summary>A layer's outline in gauge units, ready to glow or be filled.</summary>
internal sealed class Shape
{
    public Shape(Vector2[][] outlines, ArtBox bounds)
    {
        Outlines = outlines;
        Bounds = bounds;
        Directions = Array.ConvertAll(outlines, o => Outline.OffsetDirections(o));
        Triangles = Array.ConvertAll(outlines, o => Outline.Triangulate(o));
    }

    public Vector2[][] Outlines { get; }
    public Vector2[][] Directions { get; }
    public int[][] Triangles { get; }
    public ArtBox Bounds { get; }

    public int PointCount
    {
        get
        {
            var count = 0;
            foreach (var outline in Outlines) {
                count += outline.Length;
            }

            return count;
        }
    }

    /// <summary>The same shape reflected around the middle of a canvas this wide.</summary>
    public Shape Mirror(float canvasWidth) =>
        new(Array.ConvertAll(Outlines, o => Outline.Mirror(o, canvasWidth)), Bounds.Mirror(canvasWidth));
}

/// <summary>
/// Shapes from transparency: the outline of each art layer, traced once from its texture's alpha. Glows and colour
/// fills follow these outlines, so they fit whatever art the layers hold.
/// </summary>
internal sealed class ShapeLibrary
{
    // B8G8R8A8: alpha is the fourth byte.
    private const int ReadbackFormat = 87;

    private readonly ArtAtlas atlas;
    private readonly ConcurrentDictionary<(string Layer, bool Mirrored), Shape> shapes = new();
    private readonly ConcurrentDictionary<string, byte> pending = new();
    private readonly HashSet<string> failed = new();

    /// <summary>
    /// The texture's pixels as 32-bit colour with alpha in the fourth byte: converted to B8G8R8A8, or as stored if the
    /// conversion fails and the texture already is 32-bit (R8G8B8A8 and B8G8R8A8 both keep alpha there).
    /// </summary>
    private static async Task<(RawImageSpecification Spec, byte[] Pixels)> ReadPixels(
        Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap texture)
    {
        try {
            return await Service.TextureReadbackProvider.GetRawImageAsync(
                texture, new TextureModificationArgs { DxgiFormat = ReadbackFormat }, leaveWrapOpen: true);
        }
        catch (Exception e) {
            Service.Log.Debug(e, "Reading a gauge texture back as B8G8R8A8 failed; trying its own format");
            var (spec, pixels) = await Service.TextureReadbackProvider.GetRawImageAsync(
                texture, new TextureModificationArgs(), leaveWrapOpen: true);
            if (spec.BitsPerPixel != 32) {
                throw new NotSupportedException($"Gauge texture format {spec.DxgiFormat} is not 32-bit colour");
            }

            return (spec, pixels);
        }
    }

    public ShapeLibrary(ArtAtlas atlas)
    {
        this.atlas = atlas;
    }

    /// <summary>The layer's shape, or false while it is still being traced (the first frames after loading).</summary>
    public bool TryGet(string layer, bool mirrored, out Shape shape)
    {
        if (shapes.TryGetValue((layer, mirrored), out shape!)) {
            return true;
        }

        if (mirrored && shapes.TryGetValue((layer, false), out var plain)) {
            shape = shapes.GetOrAdd((layer, true), _ => plain.Mirror(atlas.Layout.Width));
            return true;
        }

        Request(layer);
        return false;
    }

    /// <summary>For the debug window: how many points each traced layer has.</summary>
    public IEnumerable<(string Layer, int Points)> Traced()
    {
        foreach (var ((layer, mirrored), shape) in shapes) {
            if (!mirrored) {
                yield return (layer, shape.PointCount);
            }
        }
    }

    private void Request(string layer)
    {
        if (failed.Contains(layer) || !pending.TryAdd(layer, 0)) {
            return;
        }

        // The sharpest image, for the most precise outline (it is traced once).
        var scale = atlas.Layout.Scales[^1];
        if (!atlas.TryGetTexture(layer, scale, out var texture)) {
            pending.TryRemove(layer, out _);
            return;
        }

        var box = atlas.Layout.Layer(layer).Box;
        _ = Task.Run(async () =>
        {
            try {
                var (spec, pixels) = await ReadPixels(texture);
                var pixelsPerUnit = spec.Width / box.W;
                var outlines = new List<Vector2[]>();
                foreach (var traced in Outline.Trace(pixels, spec.Width, spec.Height, spec.Pitch)) {
                    // A quarter of a gauge unit of detail is plenty for a glow.
                    var simple = Outline.Simplify(traced, 0.25f * pixelsPerUnit);
                    for (var i = 0; i < simple.Length; i++) {
                        simple[i] = box.Min + simple[i] / pixelsPerUnit;
                    }

                    outlines.Add(simple);
                }

                if (outlines.Count == 0) {
                    throw new InvalidOperationException("no opaque pixels");
                }

                shapes[(layer, false)] = new Shape(outlines.ToArray(), box);
            }
            catch (Exception e) {
                Service.Log.Error(e, $"Could not trace the outline of gauge layer {layer}");
                lock (failed) {
                    failed.Add(layer);
                }
            }
            finally {
                pending.TryRemove(layer, out _);
            }
        });
    }
}
