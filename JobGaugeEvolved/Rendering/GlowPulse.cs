using System;

namespace JobGaugeEvolved.Rendering;

/// <summary>
/// The pulse of a glow: over each period the glow leaves the shape's edge, drifts outward until there is an empty gap
/// between the two, and fades, then starts again at the edge, like pulses of energy coming off the shape.
/// </summary>
/// <remarks>Only uses the BCL, so it is unit-tested.</remarks>
internal static class GlowPulse
{
    /// <summary>Fraction of the period spent fading in at the edge.</summary>
    public const float FadeIn = 0.08f;

    /// <summary>Fraction of the period, at the end, spent fading out.</summary>
    public const float FadeOut = 0.35f;

    /// <summary>
    /// Gap between the shape and the glow (in gauge units) and the glow's strength (0 to 1) at a moment in time.
    /// Without animation the glow sits at the edge at full strength.
    /// </summary>
    public static (float Gap, float Strength) At(double seconds, float period, float maxGap, bool animate)
    {
        if (!animate || period <= 0f) {
            return (0f, 1f);
        }

        var t = (float)(seconds / period - Math.Floor(seconds / period));

        // Fast at first, slowing as it reaches the furthest point.
        var gap = maxGap * (1f - (1f - t) * (1f - t));

        var strength = Math.Min(1f, t / FadeIn);
        if (t > 1f - FadeOut) {
            var u = (t - (1f - FadeOut)) / FadeOut;
            strength *= 1f - u * u * (3f - 2f * u);
        }

        return (gap, strength);
    }

    /// <summary>
    /// The band's profile across its width: transparent at both edges, strongest at <see cref="Peak"/> of the way
    /// out. Returns the offsets (from the shape, in gauge units) of the band's inner edge, peak and outer edge.
    /// </summary>
    public static (float Inner, float Peak, float Outer) Band(float gap, float width) =>
        (gap, gap + width * Peak, gap + width);

    public const float Peak = 0.3f;
}
