using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JobGaugeEvolved.Rendering;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class OutlineTests
{
    /// <summary>An RGBA image whose alpha is 255 where <paramref name="inside"/> says so.</summary>
    private static (byte[] Pixels, int Width, int Height) Image(int width, int height, Func<int, int, bool> inside)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                if (inside(x, y)) {
                    pixels[(y * width + x) * 4 + 3] = 255;
                }
            }
        }

        return (pixels, width, height);
    }

    private static List<Vector2[]> Trace((byte[] Pixels, int Width, int Height) img) =>
        Outline.Trace(img.Pixels, img.Width, img.Height, img.Width * 4);

    [Fact]
    public void ADiscGivesACircle()
    {
        var img = Image(64, 64, (x, y) => (x + 0.5 - 32) * (x + 0.5 - 32) + (y + 0.5 - 32) * (y + 0.5 - 32) <= 20 * 20);
        var outlines = Trace(img);
        var outline = Assert.Single(outlines);

        var area = Outline.SignedArea(outline);
        Assert.True(area > 0, "outlines have a positive area");
        Assert.InRange(area, Math.PI * 400 * 0.97, Math.PI * 400 * 1.03);
        foreach (var p in outline) {
            Assert.InRange(Vector2.Distance(p, new Vector2(32, 32)), 19.2f, 20.8f);
        }
    }

    [Fact]
    public void TwoBlobsGiveTwoOutlinesAndHolesAreIgnored()
    {
        // A ring (square with a hole) and a separate square.
        var img = Image(60, 30, (x, y) =>
            (x is >= 2 and < 22 && y is >= 2 and < 22 && !(x is >= 8 and < 16 && y is >= 8 and < 16))
            || (x is >= 40 and < 50 && y is >= 5 and < 15));
        var outlines = Trace(img);
        Assert.Equal(2, outlines.Count);
        var areas = outlines.Select(Outline.SignedArea).OrderBy(a => a).ToArray();
        Assert.InRange(areas[0], 95, 105);    // 10 x 10
        Assert.InRange(areas[1], 395, 405);   // 20 x 20, the hole not subtracted
    }

    [Fact]
    public void SpecksAreDropped()
    {
        var img = Image(20, 20, (x, y) => x == 10 && y == 10);
        Assert.Empty(Trace(img));
    }

    [Fact]
    public void TheAlphaChannelCanSitAnywhere()
    {
        // Alpha first (ARGB-like) with padded rows.
        const int width = 10, height = 10, pitch = 48;
        var pixels = new byte[pitch * height];
        for (var y = 2; y < 8; y++) {
            for (var x = 2; x < 8; x++) {
                pixels[y * pitch + x * 4] = 255;
            }
        }

        var outline = Assert.Single(Outline.Trace(pixels, width, height, pitch, 4, 0));
        Assert.InRange(Outline.SignedArea(outline), 34, 38);
    }

    [Fact]
    public void SimplifyStaysWithinEpsilon()
    {
        var img = Image(80, 80, (x, y) => (x - 40) * (x - 40) + (y - 40) * (y - 40) <= 30 * 30);
        var outline = Assert.Single(Trace(img));
        var simple = Outline.Simplify(outline, 0.5f);
        Assert.True(simple.Length < outline.Length / 2, $"{simple.Length} of {outline.Length} points kept");
        foreach (var p in outline) {
            var nearest = Enumerable.Range(0, simple.Length)
                .Min(i => Outline.DistanceToSegment(p, simple[i], simple[(i + 1) % simple.Length]));
            Assert.True(nearest <= 0.5f + 1e-3f);
        }
    }

    [Fact]
    public void AreaOverPerimeterIsHalfTheRadiusOfADisc()
    {
        var img = Image(64, 64, (x, y) => (x + 0.5 - 32) * (x + 0.5 - 32) + (y + 0.5 - 32) * (y + 0.5 - 32) <= 20 * 20);
        var outline = Assert.Single(Trace(img));
        Assert.InRange(Outline.AreaOverPerimeter(outline), 9.3f, 10.3f);   // the traced edge is a little longer than a true circle
    }

    [Fact]
    public void AreaOverPerimeterIsAboutHalfTheThicknessOfAStrip()
    {
        var strip = new[] { new Vector2(0, 0), new Vector2(200, 0), new Vector2(200, 6), new Vector2(0, 6) };
        Assert.InRange(Outline.AreaOverPerimeter(strip), 2.8f, 3f);
        Assert.InRange(Outline.AreaOverPerimeter(strip.Reverse().ToArray()), 2.8f, 3f);   // winding does not matter
        Assert.Equal(0f, Outline.AreaOverPerimeter(new[] { Vector2.One, Vector2.One }));
    }

    [Fact]
    public void AnOffsetSquareGrowsByTheDistance()
    {
        var square = new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) };
        var ring = Outline.Offset(square, Outline.OffsetDirections(square), 3);
        Assert.Equal(16 * 16, Outline.SignedArea(ring), 0.01f);
        Assert.Equal(new Vector2(-3, -3), ring[0]);
    }

    [Fact]
    public void MirroringKeepsAPositiveArea()
    {
        var shape = new[] { new Vector2(1, 0), new Vector2(4, 0), new Vector2(4, 2) };
        var mirrored = Outline.Mirror(Outline.EnsurePositive(shape), 10);
        Assert.True(Outline.SignedArea(mirrored) > 0);
        Assert.Contains(new Vector2(9, 0), mirrored);
        Assert.Contains(new Vector2(6, 2), mirrored);
    }

    [Fact]
    public void TriangulationCoversAConcaveShape()
    {
        // An L shape.
        var l = Outline.EnsurePositive(new[]
        {
            new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 1), new Vector2(1, 1), new Vector2(1, 4), new Vector2(0, 4),
        });
        var indices = Outline.Triangulate(l);
        Assert.Equal((l.Length - 2) * 3, indices.Length);
        var total = 0f;
        for (var i = 0; i < indices.Length; i += 3) {
            var tri = new[] { l[indices[i]], l[indices[i + 1]], l[indices[i + 2]] };
            var area = Outline.SignedArea(tri);
            Assert.True(area > 0, "every triangle keeps the outline's winding");
            total += area;
        }

        Assert.Equal(Outline.SignedArea(l), total, 0.001f);
    }
}
