using System.Linq;
using System.Numerics;
using JobGaugeEvolved.Configuration;
using JobGaugeEvolved.Gauges;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class PlacementAndConfigTests
{
    private static readonly Vector2 Default = new(960, 800);

    [Fact]
    public void FreeUsesThePositionOrTheDefault()
    {
        var config = new ViperConfig { Size = 1.5f };
        Assert.Equal(new GaugePlacement(Default, 1.5f), GaugePlacement.Resolve(config, null, Default));

        config.Position = new Vector2(100, 200);
        Assert.Equal(new GaugePlacement(new Vector2(100, 200), 1.5f), GaugePlacement.Resolve(config, null, Default));
    }

    [Fact]
    public void FollowHudAddsOffsetAndMultipliesTheHudScale()
    {
        var config = new ViperConfig { Size = 2f, PositionMode = PositionMode.FollowHud, HudOffset = new Vector2(10, -5) };
        var hud = new GaugePlacement(new Vector2(500, 600), 0.8f);
        Assert.Equal(new GaugePlacement(new Vector2(510, 595), 1.6f), GaugePlacement.Resolve(config, hud, Default));
    }

    [Fact]
    public void FollowHudFallsBackToFreeWithoutTheGameGauge()
    {
        var config = new ViperConfig { PositionMode = PositionMode.FollowHud, Position = new Vector2(1, 2) };
        Assert.Equal(new GaugePlacement(new Vector2(1, 2), 1f), GaugePlacement.Resolve(config, null, Default));
    }

    [Fact]
    public void DraggingMovesTheSettingOfTheCurrentMode()
    {
        var config = new ViperConfig();
        GaugePlacement.Drag(config, new Vector2(5, 6), followingHud: false, currentCentre: new Vector2(100, 100));
        Assert.Equal(new Vector2(105, 106), config.Position);

        GaugePlacement.Drag(config, new Vector2(1, 1), followingHud: true, currentCentre: Vector2.Zero);
        Assert.Equal(new Vector2(1, 1), config.HudOffset);
        Assert.Equal(new Vector2(105, 106), config.Position);
    }

    [Theory]
    [InlineData(0.1f, 0.5f)]
    [InlineData(1.25f, 1.25f)]
    [InlineData(9f, 3f)]
    [InlineData(float.NaN, 1f)]
    public void SizeIsClampedTo50To300Percent(float size, float expected)
    {
        var config = new ViperConfig { Size = size };
        config.Sanitize();
        Assert.Equal(expected, config.Size);
    }

    [Fact]
    public void SanitizeRepairsHandEditedValues()
    {
        var config = new ViperConfig
        {
            Position = new Vector2(float.NaN, 1),
            HudAnchor = -3,
            FlankColor = null!,
            PositionMode = (PositionMode)99,
        };
        config.Sanitize();
        Assert.Null(config.Position);
        Assert.Equal(0, config.HudAnchor);
        Assert.Equal(ViperConfig.DefaultFlank().A, config.FlankColor.A);
        Assert.Equal(PositionMode.Free, config.PositionMode);
    }

    [Fact]
    public void GlowSettingsAreClamped()
    {
        var config = new ViperConfig
        {
            Glow = new GlowStyle { Width = 500, Gap = -3, Period = 0.01f, Intensity = float.NaN },
            BladeTintStrength = 7,
        };
        config.Sanitize();
        Assert.Equal(GlowStyle.MaxWidth, config.Glow.Width);
        Assert.Equal(0f, config.Glow.Gap);
        Assert.Equal(GlowStyle.MinPeriod, config.Glow.Period);
        Assert.Equal(0.9f, config.Glow.Intensity);
        Assert.Equal(1f, config.BladeTintStrength);

        config.Glow = null!;
        config.Sanitize();
        Assert.NotNull(config.Glow);
    }

    [Fact]
    public void TheCentreHasItsOwnColoursAndGlow()
    {
        var config = new ViperConfig { FlankCenterColor = null!, CenterGlow = new GlowStyle { Period = 99 } };
        config.Sanitize();
        Assert.Equal(ViperConfig.DefaultFlankCenter().A, config.FlankCenterColor.A);
        Assert.Equal(GlowStyle.MaxPeriod, config.CenterGlow.Period);
        Assert.NotSame(config.Glow, config.CenterGlow);

        config.CenterGlow.Width = 30;
        Assert.NotEqual(30, config.Glow.Width);
        Assert.True(config.RearCenterColor.A.X > config.RearCenterColor.A.Y, "rear centre starts red");
    }

    [Fact]
    public void IgnoredWindowsAreParsed()
    {
        var names = NameList.Parse(" _ScreenText, Tooltip ,,ScreenFrame");
        Assert.Equal(new[] { "ScreenFrame", "Tooltip", "_ScreenText" }, names.Order(System.StringComparer.Ordinal).ToArray());
        Assert.Empty(NameList.Parse(null));
    }

    [Fact]
    public void GlowDefaultsPulseEveryTwoSecondsWithAGap()
    {
        var glow = new ViperConfig().Glow;
        Assert.True(glow.Pulse);
        Assert.Equal(2f, glow.Period);
        Assert.True(glow.Gap > 0);
        Assert.True(glow.Additive);
        Assert.Equal(0.5f, new ViperConfig().BladeTintStrength);
    }

    [Fact]
    public void DefaultsMatchTheGame()
    {
        var config = new ViperConfig();
        Assert.Equal(ColorMode.Gradient, config.FlankColor.Mode);
        Assert.Equal(ColorMode.Gradient, config.RearColor.Mode);
        Assert.True(config.FlankColor.A.Y > config.FlankColor.A.X, "flank starts green");
        Assert.True(config.RearColor.A.X > config.RearColor.A.Y, "rear starts red");
        Assert.True(config.LiquidColor.A.Z > config.LiquidColor.A.X, "liquid is blue");
        Assert.True(config.TributeColor.A.Z > config.TributeColor.A.X, "tributes are blue");
        Assert.True(config.CoilColor.A.X > 0.9f && config.CoilColor.A.Y < 0.3f, "coils are rubies");
        Assert.True(config.HideGameGauge);
        Assert.Equal(1f, config.Size);
    }
}
