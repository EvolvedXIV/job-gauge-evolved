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

    /// <summary>How strongly a lit blade's colour is added to the steel: 0.5 is like GIMP's Addition at 50%.</summary>
    public float BladeTintStrength = 0.5f;

    // Serpent Offerings.
    public ColorSpec LiquidColor = DefaultLiquid();
    public ColorSpec TributeColor = DefaultTribute();
    public bool ShowValue = true;
    public bool ShowReawakenTimer = true;

    // Rattling Coils.
    public bool ShowCoils = true;
    public ColorSpec CoilColor = DefaultCoil();

    // Defaults follow the game: a lit Vipersight blade is blue-violet at the tip and orange at the hilt.
    public static ColorSpec DefaultRecommend() =>
        new(ColorSpec.Hex("#7C86FF"), ColorSpec.Hex("#FFA040"), GradientDirection.LeftToRight);

    public static ColorSpec DefaultFlank() =>
        new(ColorSpec.Hex("#3DDC5A"), ColorSpec.Hex("#FFE15A"), GradientDirection.LeftToRight);

    public static ColorSpec DefaultRear() => new(ColorSpec.Hex("#FF4B3E"), ColorSpec.Hex("#FFC94A"));

    // The game's liquid: a bright cyan surface over a violet-blue body.
    public static ColorSpec DefaultLiquid() => new(ColorSpec.Hex("#47D8FF"), ColorSpec.Hex("#6450E6"));

    public static ColorSpec DefaultTribute() => new(ColorSpec.Hex("#3FA0FF"));

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
        BladeTintStrength = float.IsFinite(BladeTintStrength) ? Math.Clamp(BladeTintStrength, 0f, 1f) : 0.5f;
    }
}
