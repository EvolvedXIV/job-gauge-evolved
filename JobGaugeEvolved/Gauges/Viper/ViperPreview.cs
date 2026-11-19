using System;

namespace JobGaugeEvolved.Gauges.Viper;

/// <summary>Made-up game states for the settings window's preview, one per row of the combo rules.</summary>
internal static class ViperPreview
{
    public static readonly (string Name, ViperSnapshot Snapshot)[] States =
    [
        ("No buffs: nothing lit", Make(0, ViperVenom.None)),
        ("Honed Steel: Steel Fangs", Make(0, ViperVenom.Hindstung, honedSteel: true)),
        ("Honed Reavers: Reaving Fangs", Make(0, ViperVenom.Flanksbane, honedReavers: true)),
        ("Step 2, flank venom: Hunter's Sting", Make(ViperIds.SteelFangs, ViperVenom.Flankstung, honedReavers: true)),
        ("Step 2, rear venom: Swiftskin's Sting", Make(ViperIds.ReavingFangs, ViperVenom.Hindstung, honedSteel: true)),
        ("Flank, Flankstung: Flanksting Strike", Make(ViperIds.HuntersSting, ViperVenom.Flankstung, honedReavers: true)),
        ("Flank, Flanksbane: Flanksbane Fang", Make(ViperIds.HuntersSting, ViperVenom.Flanksbane, honedReavers: true)),
        ("Rear, Hindstung: Hindsting Strike", Make(ViperIds.SwiftskinsSting, ViperVenom.Hindstung, honedSteel: true)),
        ("Rear, Hindsbane: Hindsbane Fang", Make(ViperIds.SwiftskinsSting, ViperVenom.Hindsbane, honedSteel: true)),
        ("Reawaken", Make(0, ViperVenom.Flankstung, honedSteel: true) with
        {
            ReawakenedTime = 21.4f, AnguineTribute = 3, SerpentOffering = 20, RattlingCoils = 1,
        }),
    ];

    /// <summary>The state shown when cycling automatically: one every two seconds.</summary>
    public static int CycleIndex(double seconds) => (int)(Math.Max(0, seconds) / 2.0) % States.Length;

    private static ViperSnapshot Make(uint combo, ViperVenom venom, bool honedSteel = false, bool honedReavers = false) =>
        new(
            Level: 100,
            ComboAction: combo,
            ComboTimer: combo == 0 ? 0f : 25f,
            HonedSteel: honedSteel,
            HonedReavers: honedReavers,
            Venom: venom,
            ReawakenedTime: 0f,
            SerpentOffering: 65,
            AnguineTribute: 0,
            RattlingCoils: 2);
}
