using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using JobGaugeEvolved.Gauges.Viper;
using JobGaugeEvolved.Rendering;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class ArtLayoutTests
{
    private static string AssetFolder([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "JobGaugeEvolved", "Assets", "Viper");

    private static ArtLayout ViperLayout() => ArtLayout.Parse(File.ReadAllText(Path.Combine(AssetFolder(), "layout.json")));

    [Fact]
    public void EveryViperLayerExistsAtEveryResolution()
    {
        var layout = ViperLayout();
        Assert.Equal(new[] { 1, 2, 3 }, layout.Scales);
        foreach (var name in ViperLayers.All) {
            var layer = layout.Layer(name);
            Assert.True(layer.Box.W > 0 && layer.Box.H > 0, name);
            foreach (var scale in layout.Scales) {
                var file = Path.Combine(AssetFolder(), $"{name}@{scale}x.png");
                Assert.True(File.Exists(file), file);
            }
        }

        foreach (var anchor in ViperLayers.Anchors) {
            Assert.NotNull(layout.Anchor(anchor));
        }
    }

    [Fact]
    public void MasksAndSlotsAreMarked()
    {
        var layout = ViperLayout();
        Assert.True(layout.Layer(ViperLayers.Liquid).IsMask);
        Assert.False(layout.Layer(ViperLayers.BladeBase).IsMask);
        Assert.Equal(ViperLayers.GemSlot, layout.Layer(ViperLayers.GemLit).Slot);
        Assert.Equal(ViperLayers.CoilSlot, layout.Layer(ViperLayers.CoilSocket).Slot);
        Assert.Null(layout.Layer(ViperLayers.VesselFrame).Slot);
        Assert.True(layout.Anchor(ViperLayers.GemSlot).Value("step") > 0);
    }

    [Fact]
    public void StonesFollowArcsAroundTheOrb()
    {
        var layout = ViperLayout();
        var gem = layout.Anchor(ViperLayers.GemSlot);
        var radius = gem.Value("radius");
        var step = gem.Value("step") * MathF.PI / 180f;
        Assert.True(radius > 0, "tributes arc over the top");

        // The middle of five is the reference slot; the others turn around the orb's centre and drop.
        Assert.Equal(Vector2.Zero, layout.SlotOffset(ViperLayers.GemSlot, 2, 5));
        var first = layout.SlotOffset(ViperLayers.GemSlot, 0, 5);
        Assert.Equal(-radius * MathF.Sin(2 * step), first.X, 0.01f);
        Assert.Equal(radius * (1 - MathF.Cos(2 * step)), first.Y, 0.01f);
        Assert.Equal(-first.X, layout.SlotOffset(ViperLayers.GemSlot, 4, 5).X, 0.01f);

        // Every slot stays the same distance from the orb's centre.
        var centre = layout.Anchor(ViperLayers.CentreAnchor).Point;
        for (var i = 0; i < 4; i++) {
            var slot = gem.Point + layout.SlotOffset(ViperLayers.GemSlot, i, 4);
            Assert.Equal(radius, Vector2.Distance(slot, centre), 0.01f);
        }

        // Coils arc under the orb, so the outer ones rise.
        Assert.True(layout.Anchor(ViperLayers.CoilSlot).Value("radius") < 0);
        Assert.True(layout.SlotOffset(ViperLayers.CoilSlot, 0, 3).Y < 0);
    }

    [Fact]
    public void SlotsWithoutARadiusSitInARow()
    {
        var layout = ArtLayout.Parse("""
            {"width": 10, "height": 5, "scales": [1], "layers": {},
             "anchors": {"row": {"x": 5, "y": 2, "spacing": 4}}}
            """);
        Assert.Equal(new Vector2(-6, 0), layout.SlotOffset("row", 0, 4));
        Assert.Equal(new Vector2(6, 0), layout.SlotOffset("row", 3, 4));
    }

    [Fact]
    public void BoundsCoverBothBladesAndAllSlots()
    {
        var layout = ViperLayout();
        var bounds = layout.Bounds(l => ViperLayers.IsMirrored(l.Name), slot => slot == ViperLayers.GemSlot ? 5 : 3, []);
        var blade = layout.Layer(ViperLayers.BladeHalo).Box;
        Assert.True(bounds.X <= blade.X);
        Assert.True(bounds.Right >= blade.Mirror(layout.Width).Right);

        // Symmetric art gives symmetric bounds around the centre.
        var centre = layout.Anchor(ViperLayers.CentreAnchor).X;
        Assert.Equal(centre - bounds.X, bounds.Right - centre, 0.5f);
    }

    [Theory]
    [InlineData(0.5f, 1)]
    [InlineData(1f, 1)]
    [InlineData(1.1f, 1)]
    [InlineData(1.5f, 2)]
    [InlineData(2f, 2)]
    [InlineData(2.5f, 3)]
    [InlineData(6f, 3)]
    public void PicksTheSmallestSharpEnoughResolution(float pixelsPerUnit, int expected)
    {
        Assert.Equal(expected, ViperLayout().PickScale(pixelsPerUnit));
    }

    [Fact]
    public void ParsesAMinimalLayout()
    {
        var layout = ArtLayout.Parse("""
            {"width": 10, "height": 5, "scales": [2, 1],
             "layers": {"a": {"x": 1, "y": 2, "w": 3, "h": 4, "kind": "mask", "slot": null}},
             "anchors": {"p": {"x": 5, "y": 6, "spacing": 7}}}
            """);
        Assert.Equal(new[] { 1, 2 }, layout.Scales);
        Assert.Equal(new ArtBox(1, 2, 3, 4), layout.Layer("a").Box);
        Assert.Equal(new ArtBox(6, 2, 3, 4), layout.Layer("a").Box.Mirror(10));
        Assert.Equal(7, layout.Anchor("p").Value("spacing"));
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => layout.Layer("missing"));
    }
}
