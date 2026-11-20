using JobGaugeEvolved.Gauges.Viper;
using Xunit;

namespace JobGaugeEvolved.Tests;

public class ViperLogicTests
{
    private const BladeGlow Off = BladeGlow.None;
    private const BladeGlow Rec = BladeGlow.Recommended;

    private static ViperSnapshot State(
        uint combo = 0, ViperVenom venom = ViperVenom.None, bool steel = false, bool reavers = false, int level = 100,
        float timer = -1f) =>
        new(level, combo, timer >= 0 ? timer : combo == 0 ? 0f : 20f, steel, reavers, venom, 0f, 0, 0, 0);

    [Theory]
    [InlineData(false, false, Off, Off)]   // no buff: nothing recommended
    [InlineData(true, false, Rec, Off)]    // Honed Steel: Steel Fangs
    [InlineData(false, true, Off, Rec)]    // Honed Reavers: Reaving Fangs
    public void Step1FollowsTheHonedBuff(bool steel, bool reavers, BladeGlow left, BladeGlow right)
    {
        var result = ViperLogic.Evaluate(State(steel: steel, reavers: reavers));
        Assert.Equal(new ViperHighlights(ViperStep.Start, left, right, false), result);
    }

    [Theory]
    [InlineData(ViperVenom.None, Rec, Rec)]       // no venom yet (the opener): either button
    [InlineData(ViperVenom.Flankstung, Rec, Off)]
    [InlineData(ViperVenom.Flanksbane, Rec, Off)]
    [InlineData(ViperVenom.Hindstung, Off, Rec)]
    [InlineData(ViperVenom.Hindsbane, Off, Rec)]
    public void Step2FollowsTheVenom(ViperVenom venom, BladeGlow left, BladeGlow right)
    {
        foreach (var opener in new[] { ViperIds.SteelFangs, ViperIds.ReavingFangs }) {
            var result = ViperLogic.Evaluate(State(opener, venom));
            Assert.Equal(new ViperHighlights(ViperStep.Second, left, right, false), result);
        }
    }

    [Theory]
    [InlineData(ViperVenom.Flankstung, BladeGlow.Flank, Off)]
    [InlineData(ViperVenom.Flanksbane, Off, BladeGlow.Flank)]
    [InlineData(ViperVenom.None, BladeGlow.Flank, BladeGlow.Flank)]        // no venom yet (the opener): either finisher
    [InlineData(ViperVenom.Hindstung, BladeGlow.Flank, BladeGlow.Flank)]   // a rear venom empowers neither
    [InlineData(ViperVenom.Hindsbane, BladeGlow.Flank, BladeGlow.Flank)]
    public void FlankFinisherLightsTheCentreAndTheVenomsBlade(ViperVenom venom, BladeGlow left, BladeGlow right)
    {
        var result = ViperLogic.Evaluate(State(ViperIds.HuntersSting, venom));
        Assert.Equal(new ViperHighlights(ViperStep.FlankFinisher, left, right, true), result);
    }

    [Theory]
    [InlineData(ViperVenom.Hindstung, BladeGlow.Rear, Off)]
    [InlineData(ViperVenom.Hindsbane, Off, BladeGlow.Rear)]
    [InlineData(ViperVenom.None, BladeGlow.Rear, BladeGlow.Rear)]
    [InlineData(ViperVenom.Flankstung, BladeGlow.Rear, BladeGlow.Rear)]
    [InlineData(ViperVenom.Flanksbane, BladeGlow.Rear, BladeGlow.Rear)]
    public void RearFinisherLightsTheCentreAndTheVenomsBlade(ViperVenom venom, BladeGlow left, BladeGlow right)
    {
        var result = ViperLogic.Evaluate(State(ViperIds.SwiftskinsSting, venom));
        Assert.Equal(new ViperHighlights(ViperStep.RearFinisher, left, right, true), result);
    }

    [Fact]
    public void AnExpiredComboIsStep1()
    {
        var result = ViperLogic.Evaluate(State(ViperIds.HuntersSting, ViperVenom.Flankstung, steel: true, timer: 0f));
        Assert.Equal(new ViperHighlights(ViperStep.Start, Rec, Off, false), result);
    }

    [Theory]
    [InlineData(34614u)] // Steel Maw
    [InlineData(34615u)] // Reaving Maw
    [InlineData(34616u)] // Hunter's Bite
    [InlineData(34617u)] // Swiftskin's Bite
    [InlineData(34618u)] // Jagged Maw
    [InlineData(34619u)] // Bloodied Maw
    [InlineData(34620u)] // Vicewinder
    public void OtherCombosLeaveTheBladesAtStep1(uint action)
    {
        var result = ViperLogic.Evaluate(State(action, ViperVenom.Hindstung, reavers: true));
        Assert.Equal(new ViperHighlights(ViperStep.Start, Off, Rec, false), result);
    }

    [Fact]
    public void NoFinisherBelowLevel30()
    {
        Assert.Equal(ViperStep.Start, ViperLogic.StepOf(State(ViperIds.HuntersSting, level: 29)));
        Assert.Equal(ViperStep.FlankFinisher, ViperLogic.StepOf(State(ViperIds.HuntersSting, level: 30)));
    }

    [Fact]
    public void LosingTheBuffsTurnsTheBladesOff()
    {
        // At step 1, buffs gone: nothing is recommended any more, with no cast in between.
        Assert.Equal((Rec, Off), Blades(ViperLogic.Evaluate(State(steel: true))));
        Assert.Equal((Off, Off), Blades(ViperLogic.Evaluate(State())));

        // Mid-combo, a venom gone: both of the next buttons are recommended again, as in the opener.
        Assert.Equal((Off, Rec), Blades(ViperLogic.Evaluate(State(ViperIds.ReavingFangs, ViperVenom.Hindsbane))));
        Assert.Equal((Rec, Rec), Blades(ViperLogic.Evaluate(State(ViperIds.ReavingFangs))));
        var flank = ViperLogic.Evaluate(State(ViperIds.HuntersSting));
        Assert.Equal((BladeGlow.Flank, BladeGlow.Flank, true), (flank.Left, flank.Right, flank.Center));

        static (BladeGlow, BladeGlow) Blades(ViperHighlights h) => (h.Left, h.Right);
    }

    [Fact]
    public void OnlyTheButtonsThatExistAreRecommendedAtLowLevels()
    {
        // Level 9: no Reaving Fangs yet (Honed Reavers can't exist then anyway).
        Assert.Equal(new ViperHighlights(ViperStep.Start, Off, Off, false), ViperLogic.Evaluate(State(reavers: true, level: 9)));

        // Level 19: no Swiftskin's Sting yet, even with a rear venom (which can't exist then anyway).
        // Without a venom it can use, step 2 recommends Hunter's Sting alone.
        Assert.Equal(
            new ViperHighlights(ViperStep.Second, Rec, Off, false),
            ViperLogic.Evaluate(State(ViperIds.SteelFangs, ViperVenom.Hindstung, level: 19)));
        Assert.Equal(
            new ViperHighlights(ViperStep.Second, Rec, Off, false),
            ViperLogic.Evaluate(State(ViperIds.SteelFangs, level: 19)));

        // Level 4: Steel Fangs has no follow-up yet.
        Assert.Equal(ViperStep.Start, ViperLogic.StepOf(State(ViperIds.SteelFangs, level: 4)));
    }

    [Fact]
    public void TheOpenersFirstComboLightsBothBladesLikeTheGame()
    {
        // Steel Fangs (grants Honed Reavers) -> Hunter's Sting (grants Hunter's Instinct) -> a flank finisher, no venom yet.
        Assert.Equal(new ViperHighlights(ViperStep.Second, Rec, Rec, false),
            ViperLogic.Evaluate(State(ViperIds.SteelFangs, reavers: true)));
        Assert.Equal(new ViperHighlights(ViperStep.FlankFinisher, BladeGlow.Flank, BladeGlow.Flank, true),
            ViperLogic.Evaluate(State(ViperIds.HuntersSting, reavers: true)));

        // Reaving Fangs (grants Honed Steel) -> Swiftskin's Sting (grants Swiftscaled) -> a rear finisher, no venom yet.
        Assert.Equal(new ViperHighlights(ViperStep.Second, Rec, Rec, false),
            ViperLogic.Evaluate(State(ViperIds.ReavingFangs, steel: true)));
        Assert.Equal(new ViperHighlights(ViperStep.RearFinisher, BladeGlow.Rear, BladeGlow.Rear, true),
            ViperLogic.Evaluate(State(ViperIds.SwiftskinsSting, steel: true)));

        // Before the first weaponskill there is no buff: nothing lit.
        Assert.Equal(ViperHighlights.None, ViperLogic.Evaluate(State()));
    }

    [Fact]
    public void AFullRotationAlternatesAsExpected()
    {
        // Steel Fangs -> Hunter's Sting -> Flanksting Strike grants Hindstung Venom and (from Steel Fangs) Honed Reavers.
        var next = ViperLogic.Evaluate(State(reavers: true, venom: ViperVenom.Hindstung));
        Assert.Equal((Off, Rec), (next.Left, next.Right)); // Reaving Fangs
        next = ViperLogic.Evaluate(State(ViperIds.ReavingFangs, ViperVenom.Hindstung, steel: true));
        Assert.Equal((Off, Rec), (next.Left, next.Right)); // Swiftskin's Sting
        next = ViperLogic.Evaluate(State(ViperIds.SwiftskinsSting, ViperVenom.Hindstung, steel: true));
        Assert.Equal((BladeGlow.Rear, Off, true), (next.Left, next.Right, next.Center)); // Hindsting Strike
    }

    [Theory]
    [InlineData(89, 0, 3)]
    [InlineData(90, 4, 3)]
    [InlineData(95, 4, 3)]
    [InlineData(96, 5, 3)]
    [InlineData(81, 0, 0)]
    [InlineData(82, 0, 2)]
    [InlineData(87, 0, 2)]
    [InlineData(88, 0, 3)]
    public void MaximaFollowTheTraits(int level, int tributes, int coils)
    {
        Assert.Equal(tributes, ViperLogic.TributeMax(level));
        Assert.Equal(coils, ViperLogic.CoilMax(level));
    }

    [Fact]
    public void EveryPreviewStateShowsWhatItsNameSays()
    {
        var expected = new[]
        {
            new ViperHighlights(ViperStep.Start, Off, Off, false),
            new ViperHighlights(ViperStep.Start, Rec, Off, false),
            new ViperHighlights(ViperStep.Start, Off, Rec, false),
            new ViperHighlights(ViperStep.Second, Rec, Rec, false),
            new ViperHighlights(ViperStep.FlankFinisher, BladeGlow.Flank, BladeGlow.Flank, true),
            new ViperHighlights(ViperStep.Second, Rec, Off, false),
            new ViperHighlights(ViperStep.Second, Off, Rec, false),
            new ViperHighlights(ViperStep.FlankFinisher, BladeGlow.Flank, Off, true),
            new ViperHighlights(ViperStep.FlankFinisher, Off, BladeGlow.Flank, true),
            new ViperHighlights(ViperStep.RearFinisher, BladeGlow.Rear, Off, true),
            new ViperHighlights(ViperStep.RearFinisher, Off, BladeGlow.Rear, true),
            new ViperHighlights(ViperStep.Start, Rec, Off, false),
        };

        Assert.Equal(expected.Length, ViperPreview.States.Length);
        for (var i = 0; i < expected.Length; i++) {
            Assert.Equal(expected[i], ViperLogic.Evaluate(ViperPreview.States[i].Snapshot));
        }

        Assert.True(ViperPreview.States[^1].Snapshot.Reawakened);
    }
}
