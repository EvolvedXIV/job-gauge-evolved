using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.JobGauge.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using JobGaugeEvolved.Configuration;
using JobGaugeEvolved.Rendering;
using JobGaugeEvolved.Windows;

namespace JobGaugeEvolved.Gauges.Viper;

/// <summary>
/// Viper: the Vipersight blades either side of the Serpent Offerings orb, with the Anguine Tribute coins on top
/// and the Rattling Coil diamonds hanging underneath.
/// </summary>
internal sealed unsafe class ViperGauge : IJobGauge
{
    private const string VipersightAddon = "JobHudRDB0";
    private const string SerpentOfferingsAddon = "JobHudRDB1";

    // Coins and rubies are small: their glow is half the blades'.
    private const float StoneGlowSize = 0.5f;

    private readonly ViperConfig config;
    private readonly ColorSpec surfaceColor = new();
    private ViperSnapshot snapshot;
    private ViperHighlights highlights = ViperHighlights.None;

    public ViperGauge(ViperConfig config)
    {
        this.config = config;
        Art = new ArtAtlas("Viper");

        var layout = Art.Layout;
        var timer = layout.Anchor(ViperLayers.TimerAnchor);
        var timerHeight = timer.Value("height");
        Bounds = layout.Bounds(
            mirrored: layer => ViperLayers.IsMirrored(layer.Name),
            maxSlots: slot => slot == ViperLayers.GemSlot ? ViperLogic.TributeMax(100) : ViperLogic.CoilMax(100),
            extra: [new ArtBox(timer.X - timerHeight * 2f, timer.Y - timerHeight / 2f - 2f, timerHeight * 4f, timerHeight + 4f)]);
        Centre = layout.Anchor(ViperLayers.CentreAnchor).Point;
    }

    public uint JobId => ViperIds.Job;
    public string Name => "Viper";
    public JobRole Role => JobRole.Melee;
    public GaugeConfig Config => config;
    public ArtAtlas Art { get; }
    public ArtBox Bounds { get; }
    public Vector2 Centre { get; }
    public IReadOnlyList<string> GameAddons { get; } = [VipersightAddon, SerpentOfferingsAddon];
    public IReadOnlyList<string> GameGaugeNames { get; } = ["Vipersight", "Serpent Offerings Gauge"];

    public (string Title, string Help) GlowSettingsText { get; } =
        ("Blade and stone glow", "The glow around lit blades, Anguine Tribute coins and Rattling Coils.");

    public IReadOnlyList<string> PreviewStates { get; } = ViperPreview.States.Select(s => s.Name).ToArray();

    public bool HasData { get; private set; }

    public void Dispose()
    {
    }

    public void Update(int? previewState)
    {
        if (previewState is { } index) {
            snapshot = ViperPreview.States[Math.Clamp(index, 0, ViperPreview.States.Length - 1)].Snapshot;
            HasData = true;
        }
        else {
            HasData = TryRead(out snapshot);
        }

        highlights = HasData ? ViperLogic.Evaluate(snapshot) : ViperHighlights.None;
    }

    private static bool TryRead(out ViperSnapshot result)
    {
        result = default;
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null || player.ClassJob.RowId != ViperIds.Job) {
            return false;
        }

        var honedSteel = false;
        var honedReavers = false;
        var venom = ViperVenom.None;
        var reawakened = 0f;
        foreach (var status in player.StatusList) {
            switch (status.StatusId) {
                case ViperIds.HonedSteel:
                    honedSteel = true;
                    break;
                case ViperIds.HonedReavers:
                    honedReavers = true;
                    break;
                case ViperIds.Reawakened:
                    reawakened = Math.Max(status.RemainingTime, 0.01f);
                    break;
                default:
                    if (venom == ViperVenom.None) {
                        venom = ViperLogic.VenomOf(status.StatusId);
                    }

                    break;
            }
        }

        var gauge = Service.JobGauges.Get<VPRGauge>();
        var actions = ActionManager.Instance();
        var level = Service.PlayerState.EffectiveLevel > 0 ? Service.PlayerState.EffectiveLevel : player.Level;

        result = new ViperSnapshot(
            Level: level,
            ComboAction: actions != null ? actions->Combo.Action : 0,
            ComboTimer: actions != null ? actions->Combo.Timer : 0f,
            HonedSteel: honedSteel,
            HonedReavers: honedReavers,
            Venom: venom,
            ReawakenedTime: reawakened,
            SerpentOffering: gauge.SerpentOffering,
            AnguineTribute: gauge.AnguineTribute,
            RattlingCoils: gauge.RattlingCoilStacks);
        return true;
    }

    public void Draw(GaugeCanvas canvas, GaugeText text, double time)
    {
        var s = snapshot;
        var h = highlights;
        var layout = canvas.Layout;
        var glow = config.Glow;

        ColorSpec GlowColor(BladeGlow g) => g switch
        {
            BladeGlow.Recommended => config.RecommendColor,
            BladeGlow.Rear => config.RearColor,
            _ => config.FlankColor,
        };

        float Strength(BladeGlow g) => g == BladeGlow.None ? 0f : 1f;

        var blades = new[] { (Glow: h.Left, Mirror: false), (Glow: h.Right, Mirror: true) };

        // 1. Glows behind everything: around the orb at a finisher (its own colours and glow), around each lit blade.
        if (h.Center) {
            var centre = h.Step == ViperStep.FlankFinisher ? config.FlankCenterColor : config.RearCenterColor;
            canvas.Glow(ViperLayers.VesselRing, centre, config.CenterGlow, time);
        }

        foreach (var (g, mirror) in blades) {
            if (Strength(g) > 0f) {
                canvas.Glow(ViperLayers.Blade, GlowColor(g), glow, time, Strength(g), mirror);
            }
        }

        // 2. Blades, their colour added on top (the steel shows through), then the hilts over the blades' bases.
        foreach (var (g, mirror) in blades) {
            canvas.Layer(ViperLayers.Blade, mirror: mirror);
            if (Strength(g) > 0f) {
                canvas.Tint(ViperLayers.Blade, GlowColor(g), config.BladeTintStrength * Strength(g), glow.Additive, mirror);
            }
        }

        foreach (var (_, mirror) in blades) {
            canvas.Layer(ViperLayers.Hilt, mirror: mirror);
        }

        // 3. Serpent Offerings: the orb fills from the bottom; 50 (enough to Reawaken) is the equator.
        var hasOfferings = ViperLogic.HasSerpentOfferings(s.Level);
        canvas.Layer(ViperLayers.VesselBack);
        if (hasOfferings) {
            var fill = Math.Clamp(s.SerpentOffering / (float)ViperIds.SerpentOfferingMax, 0f, 1f);
            if (fill > 0f) {
                var liquid = layout.Anchor(ViperLayers.LiquidAnchor);
                var level = liquid.Y + liquid.H * (1f - fill);
                var body = layout.Layer(ViperLayers.Liquid).Box;
                var cropTop = (level - body.Y) / body.H;
                canvas.Layer(ViperLayers.Liquid, config.LiquidColor, cropTop: cropTop);

                // The glowing surface sits at the level, as wide as the glass is there, in the liquid's colour there.
                if (fill < 0.98f) {
                    var radius = liquid.W / 2f;
                    var fromCentre = level - (liquid.Y + liquid.H / 2f);
                    var halfChord = MathF.Sqrt(MathF.Max(0f, radius * radius - fromCentre * fromCentre));
                    var bob = glow.Pulse ? (float)Math.Sin(time * 2.3) * 0.35f : 0f;
                    surfaceColor.A = config.LiquidColor.Direction == GradientDirection.TopToBottom
                        ? config.LiquidColor.At(cropTop)
                        : config.LiquidColor.At(0.5f);
                    canvas.Layer(ViperLayers.LiquidSurface, surfaceColor, 0.9f,
                        offset: new Vector2(0f, fromCentre + bob), widthScale: halfChord / radius);
                }
            }
        }

        canvas.Layer(ViperLayers.VesselGlass);
        canvas.Layer(ViperLayers.VesselMounts);
        canvas.Layer(ViperLayers.VesselRing);

        // 4. Anguine Tributes: metal coins that turn into glowing stones while Reawakened.
        var tributes = ViperLogic.TributeMax(s.Level);
        for (var i = 0; i < tributes; i++) {
            var offset = layout.SlotOffset(ViperLayers.GemSlot, i, tributes);
            var lit = s.Reawakened && i < s.AnguineTribute;
            if (lit) {
                canvas.Glow(ViperLayers.GemSocket, config.TributeColor, glow, time, 1f, offset: offset, size: StoneGlowSize);
            }

            canvas.Layer(ViperLayers.GemSocket, offset: offset);
            if (lit) {
                canvas.Layer(ViperLayers.GemLit, config.TributeColor, offset: offset);
            }
        }

        // 5. Rattling Coils: rubies, lit from behind while available.
        var coils = config.ShowCoils ? ViperLogic.CoilMax(s.Level) : 0;
        for (var i = 0; i < coils; i++) {
            var offset = layout.SlotOffset(ViperLayers.CoilSlot, i, coils);
            var lit = i < s.RattlingCoils;
            if (lit) {
                canvas.Glow(ViperLayers.CoilSocket, config.CoilColor, glow, time, 1f, offset: offset, size: StoneGlowSize);
            }

            canvas.Layer(ViperLayers.CoilSocket, offset: offset);
            if (lit) {
                canvas.Layer(ViperLayers.CoilLit, config.CoilColor, offset: offset);
            }
        }

        // 6. Numbers.
        if (config.ShowValue && hasOfferings) {
            var anchor = layout.Anchor(ViperLayers.ValueAnchor);
            canvas.Text(text, s.SerpentOffering.ToString(), anchor.Point, anchor.Value("height"));
        }

        if (config.ShowReawakenTimer && s.Reawakened) {
            var anchor = layout.Anchor(ViperLayers.TimerAnchor);
            canvas.Layer(ViperLayers.LabelPill);
            canvas.Text(text, MathF.Ceiling(s.ReawakenedTime).ToString("0"), anchor.Point, anchor.Value("height"));
        }
    }

    public bool DrawSettings()
    {
        var changed = false;

        Widgets.SectionHeader("Combo highlights");
        changed |= Widgets.ColorSetting("Recommended button", config.RecommendColor, ViperConfig.DefaultRecommend,
            "Steps 1 and 2: the blade of the button to press next. Left is Steel Fangs, right is Reaving Fangs.");
        changed |= Widgets.ColorSetting("Flank: blade", config.FlankColor, ViperConfig.DefaultFlank,
            "After Hunter's Sting: the blade of the flank finisher your venom empowers (Flanksting Strike left, " +
            "Flanksbane Fang right).");
        changed |= Widgets.ColorSetting("Flank: centre", config.FlankCenterColor, ViperConfig.DefaultFlankCenter,
            "After Hunter's Sting: the glow around the orb, because the finisher is a flank attack.");
        changed |= Widgets.ColorSetting("Rear: blade", config.RearColor, ViperConfig.DefaultRear,
            "After Swiftskin's Sting: the blade of the rear finisher your venom empowers (Hindsting Strike left, " +
            "Hindsbane Fang right).");
        changed |= Widgets.ColorSetting("Rear: centre", config.RearCenterColor, ViperConfig.DefaultRearCenter,
            "After Swiftskin's Sting: the glow around the orb, because the finisher is a rear attack.");

        var tint = config.BladeTintStrength * 100f;
        ImGui.SetNextItemWidth(Widgets.ItemWidth);
        if (ImGui.SliderFloat("Blade colour strength", ref tint, 0f, 100f, "%.0f%%")) {
            config.BladeTintStrength = tint / 100f;
            changed = true;
        }

        Widgets.HelpMarker("A lit blade's colour is added to the steel, like GIMP's Addition mode at this opacity, so the " +
                           "texture shows through. The glow around the blade is set under \"Blade and stone glow\".");

        changed |= Widgets.GlowSettings("Centre glow", config.CenterGlow,
            "The glow around the orb at the flank and rear finishers, set apart from the blades' glow.");

        Widgets.SectionHeader("Serpent Offerings");
        changed |= Widgets.ColorSetting("Liquid", config.LiquidColor, ViperConfig.DefaultLiquid);
        changed |= Widgets.ColorSetting("Anguine Tributes", config.TributeColor, ViperConfig.DefaultTribute,
            "The coins on top of the orb. They turn into glowing stones only while Reawakened.");
        changed |= ImGui.Checkbox("Show the gauge value", ref config.ShowValue);
        changed |= ImGui.Checkbox("Show the Reawaken timer above the tributes", ref config.ShowReawakenTimer);

        Widgets.SectionHeader("Rattling Coils");
        changed |= ImGui.Checkbox("Show Rattling Coils", ref config.ShowCoils);
        using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!config.ShowCoils)) {
            changed |= Widgets.ColorSetting("Coils", config.CoilColor, ViperConfig.DefaultCoil);
        }

        return changed;
    }

    public void DrawDebug()
    {
        var s = snapshot;
        ImGui.TextUnformatted($"Has data: {HasData}   Level: {s.Level}");
        ImGui.TextUnformatted($"Combo: {s.ComboAction} ({s.ComboTimer:0.0}s)   Step: {highlights.Step}");
        ImGui.TextUnformatted($"Honed Steel: {s.HonedSteel}   Honed Reavers: {s.HonedReavers}   Venom: {s.Venom}");
        ImGui.TextUnformatted($"Serpent Offering: {s.SerpentOffering}   Anguine Tribute: {s.AnguineTribute}   " +
                              $"Reawakened: {s.ReawakenedTime:0.0}s   Rattling Coils: {s.RattlingCoils}");
        ImGui.TextUnformatted($"Plugin: left {highlights.Left}, right {highlights.Right}, centre {highlights.Center}");
        ImGui.TextUnformatted($"Maxima from level: tributes {ViperLogic.TributeMax(s.Level)}, coils {ViperLogic.CoilMax(s.Level)}");
        ImGui.TextUnformatted($"Additive blending: {(AdditiveBlend.Available ? "on" : "unavailable")}   Shapes traced: " +
                              string.Join(", ", Art.Shapes.Traced().Select(t => $"{t.Layer} ({t.Points} points)")));

        Widgets.SectionHeader("The game's own view");
        var actions = ActionManager.Instance();
        if (actions != null) {
            var steel = actions->GetAdjustedActionId(ViperIds.SteelFangs);
            var reaving = actions->GetAdjustedActionId(ViperIds.ReavingFangs);
            ImGui.TextUnformatted(
                $"Hotbar highlight: left button {steel} = {actions->IsActionHighlighted(ActionType.Action, steel)}, " +
                $"right button {reaving} = {actions->IsActionHighlighted(ActionType.Action, reaving)}");
        }

        var sight = (AddonJobHudRDB0*)Service.GameGui.GetAddonByName(VipersightAddon).Address;
        if (sight != null && sight->IsGaugeInitialized) {
            var d = sight->DataCurrent;
            ImGui.TextUnformatted($"Vipersight: left glow {d.LeftGlow}, right glow {d.RightGlow}, combo step {d.ComboStep}, " +
                                  $"coils {d.CoilStacks}/{d.CoilMax}");
        }
        else {
            ImGui.TextDisabled("Vipersight addon not loaded.");
        }

        var offerings = (AddonJobHudRDB1*)Service.GameGui.GetAddonByName(SerpentOfferingsAddon).Address;
        if (offerings != null && offerings->IsGaugeInitialized) {
            var d = offerings->DataCurrent;
            ImGui.TextUnformatted($"Serpent Offerings: {d.GaugeValue}/{d.GaugeMax} (mid {d.GaugeMid}), " +
                                  $"tributes {d.TributeStacks}/{d.TributeMax}, time {d.TributeTimeLeft}/{d.TributeMaxTime}");
        }
        else {
            ImGui.TextDisabled("Serpent Offerings addon not loaded.");
        }
    }
}
