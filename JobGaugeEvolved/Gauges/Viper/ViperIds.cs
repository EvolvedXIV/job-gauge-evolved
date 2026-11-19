namespace JobGaugeEvolved.Gauges.Viper;

/// <summary>
/// Game ids used by the Viper gauge, checked against the Action, Status and Trait sheets (patch 7.x).
/// </summary>
internal static class ViperIds
{
    public const uint Job = 41;

    // Single-target combo. The Steel Fangs button turns into Hunter's Sting, then Flanksting or Hindsting Strike;
    // the Reaving Fangs button into Swiftskin's Sting, then Flanksbane or Hindsbane Fang.
    public const uint SteelFangs = 34606;
    public const uint ReavingFangs = 34607;
    public const uint HuntersSting = 34608;
    public const uint SwiftskinsSting = 34609;
    public const uint FlankstingStrike = 34610;
    public const uint FlanksbaneFang = 34611;
    public const uint HindstingStrike = 34612;
    public const uint HindsbaneFang = 34613;

    // Statuses.
    public const uint HonedSteel = 3672;
    public const uint HonedReavers = 3772;
    public const uint FlankstungVenom = 3645;
    public const uint FlanksbaneVenom = 3646;
    public const uint HindstungVenom = 3647;
    public const uint HindsbaneVenom = 3648;
    public const uint Reawakened = 3670;

    // Levels at which actions and traits arrive.
    public const int ReavingFangsLevel = 10;
    public const int HuntersStingLevel = 5;
    public const int SwiftskinsStingLevel = 20;
    public const int FinisherLevel = 30;
    public const int ViperRattleLevel = 82;          // Rattling Coils, up to 2
    public const int EnhancedViperRattleLevel = 88;  // up to 3
    public const int SerpentsLineageLevel = 90;      // Serpent Offerings gauge, 4 Anguine Tributes
    public const int EnhancedLineageLevel = 96;      // 5 Anguine Tributes

    public const int SerpentOfferingMax = 100;
    public const int ReawakenCost = 50;
}
