namespace JobGaugeEvolved.Gauges.Viper;

/// <summary>Names of the Viper art layers and anchors in Assets/Viper/layout.json (see art/viper/viper.svg).</summary>
/// <remarks>
/// Glows and colour fills are not layers: they are drawn from the outlines of <see cref="Blade"/>,
/// <see cref="VesselRing"/>, <see cref="GemSocket"/> and <see cref="CoilSocket"/>, traced from their transparency.
/// </remarks>
internal static class ViperLayers
{
    public const string Blade = "blade";
    public const string Hilt = "hilt";
    public const string VesselBack = "vessel-back";
    public const string Liquid = "liquid";
    public const string LiquidSurface = "liquid-surface";
    public const string VesselGlass = "vessel-glass";
    public const string VesselMounts = "vessel-mounts";
    public const string VesselRing = "vessel-ring";
    public const string GemSocket = "gem-socket";
    public const string GemLit = "gem-lit";
    public const string CoilSocket = "coil-socket";
    public const string CoilLit = "coil-lit";
    public const string LabelPill = "label-pill";

    public const string GemSlot = "gem";
    public const string CoilSlot = "coil";

    public const string CentreAnchor = "centre";
    public const string LiquidAnchor = "liquid";
    public const string ValueAnchor = "value";
    public const string TimerAnchor = "timer";

    public static readonly string[] All =
    [
        Blade, Hilt, VesselBack, Liquid, LiquidSurface, VesselGlass, VesselMounts, VesselRing,
        GemSocket, GemLit, CoilSocket, CoilLit, LabelPill,
    ];

    public static readonly string[] Anchors = [CentreAnchor, LiquidAnchor, ValueAnchor, TimerAnchor, GemSlot, CoilSlot];

    /// <summary>Layers drawn twice, the second time mirrored (the right blade and hilt).</summary>
    public static bool IsMirrored(string layer) => layer is Blade or Hilt;
}
