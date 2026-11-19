using System;

namespace JobGaugeEvolved.Gauges;

/// <summary>
/// Lets work run at a fixed rate (e.g. 20 times a second) whatever the frame rate: <see cref="ShouldRun"/> says yes
/// at most once per interval.
/// </summary>
/// <remarks>Only uses the BCL, so it is unit-tested.</remarks>
internal sealed class UpdateThrottle
{
    private readonly TimeSpan interval;
    private TimeSpan next = TimeSpan.MinValue;

    public UpdateThrottle(TimeSpan interval)
    {
        this.interval = interval > TimeSpan.Zero ? interval : throw new ArgumentOutOfRangeException(nameof(interval));
    }

    /// <summary>Whether to run now. Keeps a steady rhythm, and doesn't try to catch up after a stall.</summary>
    public bool ShouldRun(TimeSpan now)
    {
        if (now < next) {
            return false;
        }

        next = next == TimeSpan.MinValue || now - next >= interval ? now + interval : next + interval;
        return true;
    }

    /// <summary>Makes the next call run, so a change (e.g. a setting) shows without waiting.</summary>
    public void RunNext() => next = TimeSpan.MinValue;
}
