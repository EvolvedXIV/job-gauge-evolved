using JobGaugeEvolved.Rendering;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class GlowPulseTests
{
    [Fact]
    public void StartsAtTheEdgeAndDriftsOutToAGap()
    {
        var (gap0, strength0) = GlowPulse.At(0, 2f, 10f, true);
        Assert.Equal(0f, gap0);
        Assert.Equal(0f, strength0);   // invisible at the very start, fading in at the edge

        var (gapEarly, strengthEarly) = GlowPulse.At(0.3, 2f, 10f, true);
        Assert.InRange(gapEarly, 0.1f, 5f);
        Assert.Equal(1f, strengthEarly);

        var (gapLate, strengthLate) = GlowPulse.At(1.95, 2f, 10f, true);
        Assert.InRange(gapLate, 9.9f, 10f);    // the widest gap near the end of the pulse
        Assert.InRange(strengthLate, 0f, 0.05f); // fading out before starting again
    }

    [Fact]
    public void GapGrowsThroughoutAPulse()
    {
        var last = -1f;
        for (var t = 0.0; t < 2.0; t += 0.05) {
            var (gap, _) = GlowPulse.At(t, 2f, 10f, true);
            Assert.True(gap >= last);
            last = gap;
        }
    }

    [Fact]
    public void RepeatsEveryPeriod()
    {
        Assert.Equal(GlowPulse.At(0.7, 2f, 10f, true), GlowPulse.At(4.7, 2f, 10f, true));
        Assert.Equal(GlowPulse.At(0.7, 2f, 10f, true), GlowPulse.At(10.7, 2f, 10f, true));
    }

    [Fact]
    public void WithoutAnimationTheGlowHugsTheEdge()
    {
        Assert.Equal((0f, 1f), GlowPulse.At(1.234, 2f, 10f, false));
    }

    [Fact]
    public void TheBandStartsAtTheGap()
    {
        Assert.Equal((5f, 5f + 12f * GlowPulse.Peak, 17f), GlowPulse.Band(5f, 12f));
    }
}
