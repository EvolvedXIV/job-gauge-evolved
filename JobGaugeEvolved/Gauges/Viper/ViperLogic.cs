using System;

namespace JobGaugeEvolved.Gauges.Viper;

/// <summary>Where the single-target combo is.</summary>
public enum ViperStep
{
    /// <summary>No combo running: Steel Fangs or Reaving Fangs next.</summary>
    Start,

    /// <summary>After Steel or Reaving Fangs: Hunter's Sting or Swiftskin's Sting next.</summary>
    Second,

    /// <summary>After Hunter's Sting: Flanksting Strike or Flanksbane Fang (flank positional) next.</summary>
    FlankFinisher,

    /// <summary>After Swiftskin's Sting: Hindsting Strike or Hindsbane Fang (rear positional) next.</summary>
    RearFinisher,
}

/// <summary>How a blade is lit.</summary>
public enum BladeGlow
{
    None,

    /// <summary>This button is the one to press (steps 1 and 2).</summary>
    Recommended,

    /// <summary>Flank finisher, and this button's finisher is empowered by your venom.</summary>
    Flank,

    /// <summary>Flank finisher, but this button's finisher is not the empowered one.</summary>
    FlankDim,

    /// <summary>Rear finisher, and this button's finisher is empowered by your venom.</summary>
    Rear,
}

/// <summary>What the Viper gauge lights up. Left is the Steel Fangs button, right the Reaving Fangs button.</summary>
internal readonly record struct ViperHighlights(ViperStep Step, BladeGlow Left, BladeGlow Right, bool Center)
{
    public static readonly ViperHighlights None = new(ViperStep.Start, BladeGlow.None, BladeGlow.None, false);
}

/// <summary>
/// The Viper combo rules. Pure functions of a <see cref="ViperSnapshot"/>, so they are unit-tested.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Step 1: Honed Steel recommends Steel Fangs (left), Honed Reavers Reaving Fangs (right), neither both.</item>
/// <item>Step 2: a flank venom recommends Hunter's Sting (left), a rear venom Swiftskin's Sting (right), none both.</item>
/// <item>Flank finisher: both blades in the flank colour; the venom's button full strength, the other dimmed.</item>
/// <item>Rear finisher: the centre in the rear colour, plus the venom's button.</item>
/// </list>
/// The area-of-effect combo never changes this: it leaves the single-target combo at step 1.
/// </remarks>
internal static class ViperLogic
{
    public static ViperStep StepOf(in ViperSnapshot s)
    {
        if (s.ComboTimer <= 0f) {
            return ViperStep.Start;
        }

        return s.ComboAction switch
        {
            ViperIds.SteelFangs or ViperIds.ReavingFangs when s.Level >= ViperIds.HuntersStingLevel => ViperStep.Second,
            ViperIds.HuntersSting when s.Level >= ViperIds.FinisherLevel => ViperStep.FlankFinisher,
            ViperIds.SwiftskinsSting when s.Level >= ViperIds.FinisherLevel => ViperStep.RearFinisher,
            _ => ViperStep.Start,
        };
    }

    public static ViperHighlights Evaluate(in ViperSnapshot s)
    {
        var step = StepOf(s);
        switch (step) {
            case ViperStep.Start: {
                // Without Reaving Fangs (below level 10) Steel Fangs is the only opener.
                var hasRight = s.Level >= ViperIds.ReavingFangsLevel;
                var left = s.HonedSteel || !s.HonedReavers || !hasRight;
                var right = hasRight && (s.HonedReavers || !s.HonedSteel);
                return new ViperHighlights(step, Lit(left), Lit(right), false);
            }

            case ViperStep.Second: {
                var hasRight = s.Level >= ViperIds.SwiftskinsStingLevel;
                var (left, right) = s.Venom switch
                {
                    ViperVenom.Flankstung or ViperVenom.Flanksbane => (true, false),
                    ViperVenom.Hindstung or ViperVenom.Hindsbane => (false, true),
                    _ => (true, true),
                };

                if (!hasRight) {
                    (left, right) = (true, false);
                }

                return new ViperHighlights(step, Lit(left), Lit(right), false);
            }

            case ViperStep.FlankFinisher:
                return s.Venom switch
                {
                    ViperVenom.Flankstung => new ViperHighlights(step, BladeGlow.Flank, BladeGlow.FlankDim, false),
                    ViperVenom.Flanksbane => new ViperHighlights(step, BladeGlow.FlankDim, BladeGlow.Flank, false),
                    // No flank venom (e.g. the first combo): both finishers are equal.
                    _ => new ViperHighlights(step, BladeGlow.Flank, BladeGlow.Flank, false),
                };

            case ViperStep.RearFinisher:
                return s.Venom switch
                {
                    ViperVenom.Hindstung => new ViperHighlights(step, BladeGlow.Rear, BladeGlow.None, true),
                    ViperVenom.Hindsbane => new ViperHighlights(step, BladeGlow.None, BladeGlow.Rear, true),
                    _ => new ViperHighlights(step, BladeGlow.None, BladeGlow.None, true),
                };

            default:
                throw new ArgumentOutOfRangeException(nameof(s), step, null);
        }

        static BladeGlow Lit(bool on) => on ? BladeGlow.Recommended : BladeGlow.None;
    }

    /// <summary>Maximum Anguine Tributes at a level (0 before the Serpent Offerings gauge exists).</summary>
    public static int TributeMax(int level) =>
        level >= ViperIds.EnhancedLineageLevel ? 5 : level >= ViperIds.SerpentsLineageLevel ? 4 : 0;

    /// <summary>Maximum Rattling Coils at a level (0 before they exist).</summary>
    public static int CoilMax(int level) =>
        level >= ViperIds.EnhancedViperRattleLevel ? 3 : level >= ViperIds.ViperRattleLevel ? 2 : 0;

    /// <summary>Whether the Serpent Offerings gauge exists at a level.</summary>
    public static bool HasSerpentOfferings(int level) => level >= ViperIds.SerpentsLineageLevel;

    /// <summary>Reads the venom from status ids (the venoms cannot stack, so the first one found wins).</summary>
    public static ViperVenom VenomOf(uint statusId) => statusId switch
    {
        ViperIds.FlankstungVenom => ViperVenom.Flankstung,
        ViperIds.FlanksbaneVenom => ViperVenom.Flanksbane,
        ViperIds.HindstungVenom => ViperVenom.Hindstung,
        ViperIds.HindsbaneVenom => ViperVenom.Hindsbane,
        _ => ViperVenom.None,
    };
}
