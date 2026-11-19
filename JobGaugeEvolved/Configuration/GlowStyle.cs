using System;

namespace JobGaugeEvolved.Configuration;

/// <summary>How a gauge's highlights glow. Sizes are in gauge units (pixels at 100% size).</summary>
/// <remarks>Only uses the BCL, so it is compiled into the tests as well.</remarks>
[Serializable]
public sealed class GlowStyle
{
    public const float MinWidth = 2f, MaxWidth = 40f;
    public const float MaxGap = 40f;
    public const float MinPeriod = 0.5f, MaxPeriod = 6f;
    public const float MaxIntensity = 1f;

    /// <summary>How wide the glow band is.</summary>
    public float Width = 12f;

    /// <summary>How far the glow drifts away from the shape at the end of a pulse.</summary>
    public float Gap = 10f;

    /// <summary>Seconds per pulse.</summary>
    public float Period = 2f;

    /// <summary>Brightness, 0 to 1.</summary>
    public float Intensity = 0.9f;

    /// <summary>Pulse the glow; otherwise it stays at the shape's edge.</summary>
    public bool Pulse = true;

    /// <summary>
    /// Add the glow and colours to what is behind them (like GIMP's Addition mode) rather than painting over it.
    /// </summary>
    public bool Additive = true;

    public void Sanitize()
    {
        Width = Clamp(Width, MinWidth, MaxWidth, 12f);
        Gap = Clamp(Gap, 0f, MaxGap, 10f);
        Period = Clamp(Period, MinPeriod, MaxPeriod, 2f);
        Intensity = Clamp(Intensity, 0f, MaxIntensity, 0.9f);
    }

    private static float Clamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
