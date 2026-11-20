using System;

namespace JobGaugeEvolved.Configuration;

[Serializable]
public sealed class ViperConfig : GaugeConfig
{
    // Highlights.
    public ColorSpec RecommendColor = DefaultRecommend();
    public ColorSpec FlankColor = DefaultFlank();
    public ColorSpec RearColor = DefaultRear();

    // The centre (the orb) at the finishers: its own colours and glow.
    public ColorSpec FlankCenterColor = DefaultFlankCenter();
    public ColorSpec RearCenterColor = DefaultRearCenter();
    public GlowStyle CenterGlow = new();

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

    public static ColorSpec DefaultFlankCenter() => new(ColorSpec.Hex("#3DDC5A"), ColorSpec.Hex("#FFE15A"));

    public static ColorSpec DefaultRearCenter() => new(ColorSpec.Hex("#FF4B3E"), ColorSpec.Hex("#FFC94A"));

    public static ColorSpec DefaultRear() => new(ColorSpec.Hex("#FF4B3E"), ColorSpec.Hex("#FFC94A"));

    // The game's liquid: a bright cyan surface over a violet-blue body.
    public static ColorSpec DefaultLiquid() => new(ColorSpec.Hex("#47D8FF"), ColorSpec.Hex("#6450E6"));

    public static ColorSpec DefaultTribute() => new(ColorSpec.Hex("#3FA0FF"));

    public static ColorSpec DefaultCoil() => new(ColorSpec.Hex("#FF2B3D"));

    public override float GlowReach => MathF.Max(base.GlowReach, CenterGlow.Gap + CenterGlow.Width);

    public override void Sanitize()
    {
        base.Sanitize();

        // A colour removed by a hand edit falls back to its default rather than crashing the gauge.
        RecommendColor ??= DefaultRecommend();
        FlankColor ??= DefaultFlank();
        RearColor ??= DefaultRear();
        FlankCenterColor ??= DefaultFlankCenter();
        RearCenterColor ??= DefaultRearCenter();
        CenterGlow ??= new GlowStyle();
        CenterGlow.Sanitize();
        LiquidColor ??= DefaultLiquid();
        TributeColor ??= DefaultTribute();
        CoilColor ??= DefaultCoil();
        foreach (var color in new[]
                 { RecommendColor, FlankColor, RearColor, FlankCenterColor, RearCenterColor, LiquidColor, TributeColor, CoilColor }) {
            color.Sanitize();
        }

        BladeTintStrength = float.IsFinite(BladeTintStrength) ? Math.Clamp(BladeTintStrength, 0f, 1f) : 0.5f;
    }
}
