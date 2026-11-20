using System;
using System.Collections.Generic;
using System.Numerics;
using JobGaugeEvolved.Native;
using JobGaugeEvolved.Rendering;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class CoverRulesTests
{
    private const int GaugeLayer = 4;
    private static readonly Vector2 Screen = new(2560, 1440);
    private static readonly HashSet<string> Own = new(StringComparer.Ordinal) { "JobHudRDB0", "JobHudRDB1" };
    private static readonly HashSet<string> NoneIgnored = new(StringComparer.Ordinal);
    private static readonly ScreenRect Box = new(new Vector2(100, 100), new Vector2(700, 600));

    private static CoverVerdict Judge(GameUnit unit, int? gaugeLayer = GaugeLayer, IReadOnlySet<string>? ignored = null) =>
        CoverRules.Judge(unit, gaugeLayer, Own, ignored ?? NoneIgnored, Screen);

    [Theory]
    [InlineData(GaugeLayer)]        // the gauge's own layer, wherever it is in that layer's list
    [InlineData(GaugeLayer - 1)]    // a lower layer
    [InlineData(GaugeLayer + 2)]    // a higher layer
    public void WindowsWithAFrameAlwaysCover(int layer)
    {
        Assert.Equal(CoverVerdict.Covers, Judge(new GameUnit("Character", layer, true, true, Box)));
        Assert.Equal(CoverVerdict.Covers, Judge(new GameUnit("ContentsFinder", layer, true, true, Box)));
    }

    [Fact]
    public void WindowsWithAFrameCoverEvenWhenTheGaugeAddonWasNotFound()
    {
        Assert.Equal(CoverVerdict.Covers, Judge(new GameUnit("Character", 3, true, true, Box), gaugeLayer: null));
    }

    [Fact]
    public void UnitsWithoutAFrameCoverOnlyFromAHigherLayer()
    {
        Assert.Equal(CoverVerdict.Covers, Judge(new GameUnit("Tooltip", GaugeLayer + 1, false, true, Box)));
        Assert.Equal(CoverVerdict.NotAboveGauge, Judge(new GameUnit("_ParameterWidget", GaugeLayer, false, true, Box)));
        Assert.Equal(CoverVerdict.NotAboveGauge, Judge(new GameUnit("NamePlate", GaugeLayer - 2, false, true, Box)));

        // Without the gauge's layer, they can't be placed above it.
        Assert.Equal(CoverVerdict.NotAboveGauge, Judge(new GameUnit("Tooltip", 9, false, true, Box), gaugeLayer: null));
    }

    [Fact]
    public void TheGaugesOwnAddonsHiddenAndIgnoredUnitsNeverCover()
    {
        Assert.Equal(CoverVerdict.OwnGauge, Judge(new GameUnit("JobHudRDB1", GaugeLayer + 1, true, true, Box)));
        Assert.Equal(CoverVerdict.Hidden, Judge(new GameUnit("Character", GaugeLayer, true, false, Box)));
        var ignored = new HashSet<string>(StringComparer.Ordinal) { "ChatLog" };
        Assert.Equal(CoverVerdict.Ignored, Judge(new GameUnit("ChatLog", GaugeLayer, true, true, Box), ignored: ignored));
    }

    [Fact]
    public void FullScreenAndEmptyRectanglesNeverCover()
    {
        var full = new ScreenRect(Vector2.Zero, Screen);
        Assert.Equal(CoverVerdict.FullScreen, Judge(new GameUnit("FadeMiddle", GaugeLayer + 3, false, true, full)));
        Assert.Equal(CoverVerdict.FullScreen, Judge(new GameUnit("BigWindow", GaugeLayer, true, true, full)));
        Assert.Equal(CoverVerdict.Empty, Judge(new GameUnit("Character", GaugeLayer, true, true, default)));

        // Before the screen size is known, nothing is ruled full-screen.
        Assert.Equal(CoverVerdict.Covers,
            CoverRules.Judge(new GameUnit("BigWindow", GaugeLayer, true, true, full), GaugeLayer, Own, NoneIgnored, Vector2.Zero));
    }

    [Fact]
    public void SelectKeepsOnlyTheCoveringUnits()
    {
        var units = new[]
        {
            new GameUnit("JobHudRDB0", GaugeLayer, false, false, Box),
            new GameUnit("Character", GaugeLayer, true, true, Box),
            new GameUnit("_ActionBar", GaugeLayer, false, true, Box),
            new GameUnit("Inventory", GaugeLayer, true, false, Box),
            new GameUnit("Tooltip", GaugeLayer + 2, false, true, Box),
        };

        var covering = CoverRules.Select(units, GaugeLayer, Own, NoneIgnored, Screen);
        Assert.Equal(
            new[] { new CoveringWindow("Character", GaugeLayer, Box), new CoveringWindow("Tooltip", GaugeLayer + 2, Box) },
            covering);
    }
}
