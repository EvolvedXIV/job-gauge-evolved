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
/// Viper: the Vipersight blades either side of the Serpent Offerings vessel, with the Anguine Tribute gems on top
/// and the Rattling Coils underneath.
/// </summary>
internal sealed unsafe class ViperGauge : IJobGauge
{
    private const string VipersightAddon = "JobHudRDB0";
    private const string SerpentOfferingsAddon = "JobHudRDB1";

    // Seconds for one pulse of a lit highlight.
    private const double PulsePeriod = 1.6;

    private readonly ViperConfig config;
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

        float Pulse(double phase) =>
            config.AnimateGlow ? (float)(0.82 + 0.18 * Math.Sin(time * 2 * Math.PI / PulsePeriod + phase)) : 1f;

        ColorSpec GlowColor(BladeGlow glow) => glow switch
        {
            BladeGlow.Recommended => config.RecommendColor,
            BladeGlow.Rear => config.RearColor,
            _ => config.FlankColor,
        };

        float Strength(BladeGlow glow) => glow switch
        {
            BladeGlow.None => 0f,
            BladeGlow.FlankDim => config.OtherBladeIntensity,
            _ => 1f,
        };

        // Rear: the vessel glows behind everything.
        if (h.Center) {
            canvas.Layer(ViperLayers.VesselHalo, config.RearColor, Pulse(0));
        }

        // Blades: left is the Steel Fangs button, right (mirrored) the Reaving Fangs button.
        foreach (var (glow, mirror) in new[] { (h.Left, false), (h.Right, true) }) {
            var strength = Strength(glow);
            if (strength > 0f) {
                canvas.Layer(ViperLayers.BladeHalo, GlowColor(glow), strength * Pulse(mirror ? 1.2 : 0), mirror);
            }

            canvas.Layer(ViperLayers.BladeBase, mirror: mirror);
            if (strength > 0f) {
                canvas.Layer(ViperLayers.BladeTint, GlowColor(glow), strength, mirror);
            }
        }

        // Serpent Offerings: the orb fills from the bottom; 50 (enough to Reawaken) is the equator.
        var hasOfferings = ViperLogic.HasSerpentOfferings(s.Level);
        canvas.Layer(ViperLayers.VesselBack);
        if (hasOfferings) {
            var fill = Math.Clamp(s.SerpentOffering / (float)ViperIds.SerpentOfferingMax, 0f, 1f);
            if (fill > 0f) {
                var liquid = layout.Anchor(ViperLayers.LiquidAnchor);
                var level = liquid.Y + liquid.H * (1f - fill);
                var body = layout.Layer(ViperLayers.Liquid).Box;
                canvas.Layer(ViperLayers.Liquid, config.LiquidColor, cropTop: (level - body.Y) / body.H);

                // The surface sits at the level, as wide as the glass is there.
                if (fill < 0.98f) {
                    var radius = liquid.W / 2f;
                    var fromCentre = level - (liquid.Y + liquid.H / 2f);
                    var halfChord = MathF.Sqrt(MathF.Max(0f, radius * radius - fromCentre * fromCentre));
                    var bob = config.AnimateGlow ? (float)Math.Sin(time * 2.3) * 0.35f : 0f;
                    canvas.Layer(ViperLayers.LiquidSurface, config.LiquidColor, 0.9f,
                        offset: new Vector2(0f, fromCentre + bob), widthScale: halfChord / radius);
                }
            }
        }

        canvas.Layer(ViperLayers.VesselGlass);
        canvas.Layer(ViperLayers.VesselFrame);
        if (h.Center) {
            canvas.Layer(ViperLayers.VesselRim, config.RearColor, Pulse(0));
        }

        // Anguine Tributes: lit only while Reawakened.
        var tributes = ViperLogic.TributeMax(s.Level);
        for (var i = 0; i < tributes; i++) {
            var offset = layout.SlotOffset(ViperLayers.GemSlot, i, tributes);
            canvas.Layer(ViperLayers.GemSocket, offset: offset);
            if (s.Reawakened && i < s.AnguineTribute) {
                canvas.Layer(ViperLayers.GemHalo, config.TributeColor, 0.8f * Pulse(i * 0.6), offset: offset);
                canvas.Layer(ViperLayers.GemLit, config.TributeColor, offset: offset);
            }
        }

        // Rattling Coils.
        var coils = config.ShowCoils ? ViperLogic.CoilMax(s.Level) : 0;
        for (var i = 0; i < coils; i++) {
            var offset = layout.SlotOffset(ViperLayers.CoilSlot, i, coils);
            canvas.Layer(ViperLayers.CoilSocket, offset: offset);
            if (i < s.RattlingCoils) {
                canvas.Layer(ViperLayers.CoilHalo, config.CoilColor, 0.7f, offset: offset);
                canvas.Layer(ViperLayers.CoilLit, config.CoilColor, offset: offset);
            }
        }

        if (config.ShowValue && hasOfferings) {
            var anchor = layout.Anchor(ViperLayers.ValueAnchor);
            canvas.Text(text, s.SerpentOffering.ToString(), anchor.Point, anchor.Value("height"));
        }

        if (config.ShowReawakenTimer && s.Reawakened) {
            var anchor = layout.Anchor(ViperLayers.TimerAnchor);
            canvas.Text(text, MathF.Ceiling(s.ReawakenedTime).ToString("0"), anchor.Point, anchor.Value("height"));
        }
    }

    public bool DrawSettings()
    {
        var changed = false;

        Widgets.SectionHeader("Combo highlights");
        changed |= Widgets.ColorSetting("Recommended button", config.RecommendColor, ViperConfig.DefaultRecommend,
            "Steps 1 and 2: the blade of the button to press next. Left is Steel Fangs, right is Reaving Fangs.");
        changed |= Widgets.ColorSetting("Flank finisher", config.FlankColor, ViperConfig.DefaultFlank,
            "After Hunter's Sting: both blades light up, because Flanksting Strike and Flanksbane Fang are flank attacks.");
        changed |= Widgets.ColorSetting("Rear finisher", config.RearColor, ViperConfig.DefaultRear,
            "After Swiftskin's Sting: the Serpent Offerings vessel lights up, because Hindsting Strike and Hindsbane Fang are rear attacks.");
        var intensity = config.OtherBladeIntensity * 100f;
        ImGui.SetNextItemWidth(Widgets.ItemWidth);
        if (ImGui.SliderFloat("Other blade at the flank step", ref intensity, 0f, 100f, "%.0f%%")) {
            config.OtherBladeIntensity = intensity / 100f;
            changed = true;
        }

        Widgets.HelpMarker("At the flank finisher, the blade whose finisher your venom does not empower is drawn this bright.");
        changed |= ImGui.Checkbox("Pulse lit highlights", ref config.AnimateGlow);

        Widgets.SectionHeader("Serpent Offerings");
        changed |= Widgets.ColorSetting("Liquid", config.LiquidColor, ViperConfig.DefaultLiquid);
        changed |= Widgets.ColorSetting("Anguine Tributes", config.TributeColor, ViperConfig.DefaultTribute,
            "The gems on top of the vessel. They light up only while Reawakened.");
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
