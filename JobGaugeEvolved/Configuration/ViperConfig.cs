using System;

namespace JobGaugeEvolved.Configuration;

[Serializable]
public sealed class ViperConfig : GaugeConfig
{
    // Highlights.
    public ColorSpec RecommendColor = DefaultRecommend();
    public ColorSpec FlankColor = DefaultFlank();
    public ColorSpec RearColor = DefaultRear();

    /// <summary>How bright the blade whose finisher is not empowered by your venom is at the flank step.</summary>
    public float OtherBladeIntensity = 0.4f;

    /// <summary>Let lit highlights pulse gently.</summary>
    public bool AnimateGlow = true;

    // Serpent Offerings.
    public ColorSpec LiquidColor = DefaultLiquid();
    public ColorSpec TributeColor = DefaultTribute();
    public bool ShowValue = true;
    public bool ShowReawakenTimer = true;

    // Rattling Coils.
    public bool ShowCoils = true;
    public ColorSpec CoilColor = DefaultCoil();

    public static ColorSpec DefaultRecommend() => new(ColorSpec.Hex("#FFD45C"));

    public static ColorSpec DefaultFlank() => new(ColorSpec.Hex("#4CD964"), ColorSpec.Hex("#FFE066"));

    public static ColorSpec DefaultRear() => new(ColorSpec.Hex("#FF4B3E"), ColorSpec.Hex("#FFD45C"));

    public static ColorSpec DefaultLiquid() => new(ColorSpec.Hex("#3F94FF"));

    public static ColorSpec DefaultTribute() => new(ColorSpec.Hex("#4FA8FF"));

    public static ColorSpec DefaultCoil() => new(ColorSpec.Hex("#FF2B3D"));

    public override void Sanitize()
    {
        base.Sanitize();

        // A colour removed by a hand edit falls back to its default rather than crashing the gauge.
        RecommendColor ??= DefaultRecommend();
        FlankColor ??= DefaultFlank();
        RearColor ??= DefaultRear();
        LiquidColor ??= DefaultLiquid();
        TributeColor ??= DefaultTribute();
        CoilColor ??= DefaultCoil();
        foreach (var color in new[] { RecommendColor, FlankColor, RearColor, LiquidColor, TributeColor, CoilColor }) {
            color.Sanitize();
        }

        OtherBladeIntensity = float.IsFinite(OtherBladeIntensity) ? Math.Clamp(OtherBladeIntensity, 0f, 1f) : 0.4f;
    }
}
