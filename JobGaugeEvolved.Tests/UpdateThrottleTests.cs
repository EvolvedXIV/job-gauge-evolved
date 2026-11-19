using System;
using JobGaugeEvolved.Gauges;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class UpdateThrottleTests
{
    private static int Runs(UpdateThrottle throttle, double fps, double seconds)
    {
        var runs = 0;
        for (var t = 0.0; t < seconds; t += 1.0 / fps) {
            if (throttle.ShouldRun(TimeSpan.FromSeconds(t))) {
                runs++;
            }
        }

        return runs;
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    [InlineData(240)]
    public void RunsTwentyTimesASecondWhateverTheFrameRate(double fps)
    {
        var runs = Runs(new UpdateThrottle(TimeSpan.FromMilliseconds(50)), fps, 10);
        Assert.InRange(runs, 198, 202);
    }

    [Fact]
    public void ASlowFrameRateRunsEveryFrame()
    {
        Assert.InRange(Runs(new UpdateThrottle(TimeSpan.FromMilliseconds(50)), 15, 10), 149, 151);
    }

    [Fact]
    public void DoesNotCatchUpAfterAStall()
    {
        var throttle = new UpdateThrottle(TimeSpan.FromMilliseconds(50));
        Assert.True(throttle.ShouldRun(TimeSpan.Zero));
        Assert.True(throttle.ShouldRun(TimeSpan.FromSeconds(5)));      // after a 5 s hitch: one run, not a burst
        Assert.False(throttle.ShouldRun(TimeSpan.FromSeconds(5.01)));
        Assert.True(throttle.ShouldRun(TimeSpan.FromSeconds(5.05)));
    }

    [Fact]
    public void RunNextSkipsTheWait()
    {
        var throttle = new UpdateThrottle(TimeSpan.FromMilliseconds(50));
        Assert.True(throttle.ShouldRun(TimeSpan.Zero));
        Assert.False(throttle.ShouldRun(TimeSpan.FromMilliseconds(10)));
        throttle.RunNext();
        Assert.True(throttle.ShouldRun(TimeSpan.FromMilliseconds(11)));
    }
}
