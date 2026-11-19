using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace JobGaugeEvolved.Rendering;

/// <summary>A rectangle in gauge units (1 unit = 1 pixel at 100% size).</summary>
internal readonly record struct ArtBox(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;
    public Vector2 Min => new(X, Y);
    public Vector2 Size => new(W, H);

    public ArtBox Offset(Vector2 by) => this with { X = X + by.X, Y = Y + by.Y };

    /// <summary>The same box reflected around the vertical line in the middle of a canvas this wide.</summary>
    public ArtBox Mirror(float canvasWidth) => this with { X = canvasWidth - X - W };

    public ArtBox Union(ArtBox other)
    {
        var x = MathF.Min(X, other.X);
        var y = MathF.Min(Y, other.Y);
        return new ArtBox(x, y, MathF.Max(Right, other.Right) - x, MathF.Max(Bottom, other.Bottom) - y);
    }
}

/// <summary>One image layer of the art, as rendered by scripts/render_art.js.</summary>
/// <param name="IsMask">White/grey art that is coloured by a setting; otherwise drawn as is.</param>
/// <param name="Slot">For gems and coils: the anchor the layer is drawn relative to, once per slot.</param>
internal sealed record ArtLayer(string Name, ArtBox Box, bool IsMask, string? Slot);

/// <summary>A reference point (or rectangle) in the art, with optional extra numbers such as spacing.</summary>
internal sealed record ArtAnchor(float X, float Y, float W, float H, IReadOnlyDictionary<string, float> Values)
{
    public Vector2 Point => new(X, Y);

    public float Value(string name, float fallback = 0f) => Values.TryGetValue(name, out var v) ? v : fallback;
}

/// <summary>
/// The layout.json written next to the art images: canvas size, every layer's box and the anchors.
/// </summary>
/// <remarks>Only uses the BCL, so it is compiled into the tests as well.</remarks>
internal sealed class ArtLayout
{
    private ArtLayout(int width, int height, int[] scales, Dictionary<string, ArtLayer> layers,
        Dictionary<string, ArtAnchor> anchors)
    {
        Width = width;
        Height = height;
        Scales = scales;
        Layers = layers;
        Anchors = anchors;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>The resolutions each layer was rendered at (1, 2, 3).</summary>
    public int[] Scales { get; }

    public IReadOnlyDictionary<string, ArtLayer> Layers { get; }
    public IReadOnlyDictionary<string, ArtAnchor> Anchors { get; }

    public ArtLayer Layer(string name) =>
        Layers.TryGetValue(name, out var layer) ? layer : throw new KeyNotFoundException($"No art layer '{name}'");

    public ArtAnchor Anchor(string name) =>
        Anchors.TryGetValue(name, out var anchor) ? anchor : throw new KeyNotFoundException($"No art anchor '{name}'");

    public static ArtLayout Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var scales = new List<int>();
        foreach (var s in root.GetProperty("scales").EnumerateArray()) {
            scales.Add(s.GetInt32());
        }

        scales.Sort();
        if (scales.Count == 0) {
            throw new FormatException("layout.json lists no scales");
        }

        var layers = new Dictionary<string, ArtLayer>();
        foreach (var p in root.GetProperty("layers").EnumerateObject()) {
            var v = p.Value;
            var box = new ArtBox(v.GetProperty("x").GetSingle(), v.GetProperty("y").GetSingle(),
                v.GetProperty("w").GetSingle(), v.GetProperty("h").GetSingle());
            var isMask = v.TryGetProperty("kind", out var kind) && kind.GetString() == "mask";
            var slot = v.TryGetProperty("slot", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
            layers[p.Name] = new ArtLayer(p.Name, box, isMask, slot);
        }

        var anchors = new Dictionary<string, ArtAnchor>();
        foreach (var p in root.GetProperty("anchors").EnumerateObject()) {
            var values = new Dictionary<string, float>();
            foreach (var field in p.Value.EnumerateObject()) {
                if (field.Value.ValueKind == JsonValueKind.Number) {
                    values[field.Name] = field.Value.GetSingle();
                }
            }

            anchors[p.Name] = new ArtAnchor(
                values.GetValueOrDefault("x"), values.GetValueOrDefault("y"),
                values.GetValueOrDefault("w"), values.GetValueOrDefault("h"), values);
        }

        return new ArtLayout(root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32(),
            scales.ToArray(), layers, anchors);
    }

    /// <summary>
    /// Offset of slot <paramref name="index"/> of <paramref name="count"/> from the anchor's reference slot: the
    /// slots are spaced evenly and centred on the anchor.
    /// </summary>
    public Vector2 SlotOffset(string slot, int index, int count) =>
        new((index - (count - 1) / 2f) * Anchor(slot).Value("spacing"), 0f);

    /// <summary>
    /// Everything the gauge can draw: every layer (mirrored ones twice, slot layers spread over their most slots)
    /// plus <paramref name="extra"/> boxes such as text. The overlay window is this big.
    /// </summary>
    public ArtBox Bounds(Func<ArtLayer, bool> mirrored, Func<string, int> maxSlots, IEnumerable<ArtBox> extra)
    {
        ArtBox? bounds = null;
        void Add(ArtBox box) => bounds = bounds is { } b ? b.Union(box) : box;

        foreach (var layer in Layers.Values) {
            if (layer.Slot is { } slot) {
                var count = Math.Max(1, maxSlots(slot));
                Add(layer.Box.Offset(SlotOffset(slot, 0, count)));
                Add(layer.Box.Offset(SlotOffset(slot, count - 1, count)));
            }
            else {
                Add(layer.Box);
                if (mirrored(layer)) {
                    Add(layer.Box.Mirror(Width));
                }
            }
        }

        foreach (var box in extra) {
            Add(box);
        }

        var result = bounds ?? new ArtBox(0, 0, Width, Height);

        // Whole units, so the window lands on whole pixels at 100%.
        var x = MathF.Floor(result.X);
        var y = MathF.Floor(result.Y);
        return new ArtBox(x, y, MathF.Ceiling(result.Right) - x, MathF.Ceiling(result.Bottom) - y);
    }

    /// <summary>
    /// The resolution to draw with at <paramref name="pixelsPerUnit"/>: the smallest one at least that sharp
    /// (with a little slack, as slight upscaling is invisible), else the largest.
    /// </summary>
    public int PickScale(float pixelsPerUnit)
    {
        foreach (var scale in Scales) {
            if (pixelsPerUnit <= scale * 1.15f) {
                return scale;
            }
        }

        return Scales[^1];
    }
}
