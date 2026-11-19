using System.Numerics;
using JobGaugeEvolved.Configuration;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class ColorSpecTests
{
    private static readonly Vector4 Red = new(1, 0, 0, 1);
    private static readonly Vector4 Blue = new(0, 0, 1, 1);

    [Fact]
    public void SingleColourIsTheSameEverywhere()
    {
        var spec = new ColorSpec(Red) { B = Blue };
        Assert.Equal(Red, spec.At(0.7f));
        Assert.Equal((Red, Red, Red, Red), spec.Corners(0.2f, 0.4f, 0.1f, 0.9f));
    }

    [Fact]
    public void VerticalGradientRunsTopToBottom()
    {
        var spec = new ColorSpec(Red, Blue);
        var (tl, tr, br, bl) = spec.Corners();
        Assert.Equal(Red, tl);
        Assert.Equal(Red, tr);
        Assert.Equal(Blue, br);
        Assert.Equal(Blue, bl);
    }

    [Fact]
    public void ACroppedQuadGetsItsSliceOfTheGradient()
    {
        // A bar filled to 50% with a left-to-right gradient ends half way between the two colours.
        var spec = new ColorSpec(Red, Blue, GradientDirection.LeftToRight);
        var (tl, tr, br, bl) = spec.Corners(0f, 0.5f);
        Assert.Equal(Red, tl);
        Assert.Equal(Red, bl);
        Assert.Equal(new Vector4(0.5f, 0, 0.5f, 1), tr);
        Assert.Equal(tr, br);
    }

    [Fact]
    public void AMirroredShapeMirrorsAHorizontalGradient()
    {
        // The right blade is the left one mirrored: its gradient must still start at its tip (the right end).
        var spec = new ColorSpec(Red, Blue, GradientDirection.LeftToRight);
        var (tl, tr, br, bl) = spec.Corners(mirrored: true);
        Assert.Equal((Blue, Red, Red, Blue), (tl, tr, br, bl));

        var vertical = new ColorSpec(Red, Blue);
        Assert.Equal(vertical.Corners(), vertical.Corners(mirrored: true));
    }

    [Fact]
    public void PackIsAbgrWithRedInTheLowByte()
    {
        Assert.Equal(0xFF0000FFu, ColorSpec.Pack(Red));
        Assert.Equal(0x80FF0000u, ColorSpec.Pack(Blue, 0.5f));
        Assert.Equal(0x00000000u, ColorSpec.Pack(Vector4.Zero));
    }

    [Fact]
    public void HexParsesRgb()
    {
        Assert.Equal(new Vector4(0x4C / 255f, 0xD9 / 255f, 0x64 / 255f, 1f), ColorSpec.Hex("#4CD964"));
    }

    [Fact]
    public void SanitizeClampsChannels()
    {
        var spec = new ColorSpec(new Vector4(2, -1, 0.5f, 1)) { Mode = (ColorMode)42 };
        spec.Sanitize();
        Assert.Equal(new Vector4(1, 0, 0.5f, 1), spec.A);
        Assert.Equal(ColorMode.Single, spec.Mode);
    }
}
