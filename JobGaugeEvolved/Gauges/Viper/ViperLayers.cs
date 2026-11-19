namespace JobGaugeEvolved.Gauges.Viper;

/// <summary>Names of the Viper art layers and anchors in Assets/Viper/layout.json (see art/viper/viper.svg).</summary>
internal static class ViperLayers
{
    public const string BladeHalo = "blade-halo";
    public const string BladeBase = "blade-base";
    public const string BladeTint = "blade-tint";
    public const string VesselHalo = "vessel-halo";
    public const string VesselBack = "vessel-back";
    public const string Liquid = "liquid";
    public const string LiquidEdge = "liquid-edge";
    public const string VesselFrame = "vessel-frame";
    public const string VesselRim = "vessel-rim";
    public const string GemSocket = "gem-socket";
    public const string GemHalo = "gem-halo";
    public const string GemLit = "gem-lit";
    public const string CoilSocket = "coil-socket";
    public const string CoilHalo = "coil-halo";
    public const string CoilLit = "coil-lit";

    public const string GemSlot = "gem";
    public const string CoilSlot = "coil";

    public const string CentreAnchor = "centre";
    public const string LiquidAnchor = "liquid";
    public const string ValueAnchor = "value";
    public const string TimerAnchor = "timer";

    public static readonly string[] All =
    [
        BladeHalo, BladeBase, BladeTint, VesselHalo, VesselBack, Liquid, LiquidEdge, VesselFrame, VesselRim,
        GemSocket, GemHalo, GemLit, CoilSocket, CoilHalo, CoilLit,
    ];

    public static readonly string[] Anchors = [CentreAnchor, LiquidAnchor, ValueAnchor, TimerAnchor, GemSlot, CoilSlot];

    /// <summary>Layers drawn twice, the second time mirrored (the right blade).</summary>
    public static bool IsMirrored(string layer) => layer is BladeHalo or BladeBase or BladeTint;
}
