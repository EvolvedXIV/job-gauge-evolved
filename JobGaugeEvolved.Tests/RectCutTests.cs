using System.Linq;
using System.Numerics;
using JobGaugeEvolved.Rendering;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class RectCutTests
{
    private static ScreenRect R(float x0, float y0, float x1, float y1) => new(new Vector2(x0, y0), new Vector2(x1, y1));

    private static readonly ScreenRect Gauge = R(0, 0, 100, 50);

    [Fact]
    public void NoOverlapLeavesTheWholeGauge()
    {
        Assert.Equal(new[] { Gauge }, RectCut.Visible(Gauge, [R(200, 0, 300, 50)]));
    }

    [Fact]
    public void AWindowOverTheWholeGaugeHidesIt()
    {
        Assert.Empty(RectCut.Visible(Gauge, [R(-10, -10, 200, 100)]));
    }

    [Fact]
    public void AWindowOverOneEdgeLeavesOnePiece()
    {
        var pieces = RectCut.Visible(Gauge, [R(60, -20, 300, 80)]);
        Assert.Equal(new[] { R(0, 0, 60, 50) }, pieces);
    }

    [Fact]
    public void AWindowInTheMiddleLeavesFourPieces()
    {
        var pieces = RectCut.Visible(Gauge, [R(40, 20, 60, 30)]);
        Assert.Equal(4, pieces.Count);
        Assert.Equal(100 * 50 - 20 * 10, pieces.Sum(p => p.Area), 3);
        Assert.DoesNotContain(pieces, p => p.Overlaps(R(40, 20, 60, 30)));
    }

    [Fact]
    public void SeveralWindowsLeaveDisjointPiecesCoveringTheRest()
    {
        ScreenRect[] covers = [R(-5, -5, 30, 20), R(70, 10, 120, 40), R(20, 35, 80, 60)];
        var pieces = RectCut.Visible(Gauge, covers);

        for (var i = 0; i < pieces.Count; i++) {
            Assert.All(covers, c => Assert.False(pieces[i].Overlaps(c)));
            for (var j = i + 1; j < pieces.Count; j++) {
                Assert.False(pieces[i].Overlaps(pieces[j]));
            }
        }

        // Count what's left on a fine grid and compare with the pieces' area.
        var free = 0;
        for (var y = 0.25f; y < 50; y += 0.5f) {
            for (var x = 0.25f; x < 100; x += 0.5f) {
                var p = R(x, y, x + 0.01f, y + 0.01f);
                if (!covers.Any(c => c.Overlaps(p))) {
                    free++;
                }
            }
        }

        Assert.Equal(free * 0.25f, pieces.Sum(p => p.Area), 1);
    }

    [Fact]
    public void ThePieceCountIsCapped()
    {
        // Many small windows scattered over the gauge.
        var covers = Enumerable.Range(0, 40).Select(i => R(i * 2.4f + 0.5f, 10 + i % 3 * 10, i * 2.4f + 1.5f, 15 + i % 3 * 10));
        Assert.InRange(RectCut.Visible(Gauge, covers).Count, 1, RectCut.MaxPieces);
    }
}
