namespace JobGaugeEvolved.Gauges.Viper;

public enum ViperVenom
{
    None,
    Flankstung,
    Flanksbane,
    Hindstung,
    Hindsbane,
}

/// <summary>Everything the Viper gauge needs from the game, read once per frame.</summary>
/// <param name="Level">Effective (synced) level.</param>
/// <param name="ComboAction">Last action of the running combo (0 if none).</param>
/// <param name="ComboTimer">Seconds left on the combo; 0 or less means no combo is running.</param>
/// <param name="ReawakenedTime">Seconds left on Reawakened (0 when not reawakened).</param>
internal readonly record struct ViperSnapshot(
    int Level,
    uint ComboAction,
    float ComboTimer,
    bool HonedSteel,
    bool HonedReavers,
    ViperVenom Venom,
    float ReawakenedTime,
    int SerpentOffering,
    int AnguineTribute,
    int RattlingCoils)
{
    public bool Reawakened => ReawakenedTime > 0f;
}
