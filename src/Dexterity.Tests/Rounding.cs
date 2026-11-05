using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed class RoundingTests
{
    [Theory]
    [ClassData(typeof(Amounts))]
    public void Apply_Should_OptionallyRoundTheEffectAmount(bool round, double amount, double expected)
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = round;
        g.Orc.Stats[HP].Current = 50;
        var result = g.Hit(amount: amount);
        var original = result.Items.Single();

        Ok(g.Effects.Apply(g.Orc.Id, result));

        Current(g.Orc).ShouldBe(50 + expected);
        result.Items.Single().Amount.ShouldBe(expected);
        original.Amount.ShouldBe(amount);
    }

    [Fact]
    public void NewWorld_Should_KeepFractionalApplicationByDefault()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts.ShouldBeFalse();
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit(amount: -1.25)));
        Current(g.Orc).ShouldBe(98.75);
    }

    [Fact]
    public void Apply_Should_RoundOncePerStatAfterCombiningEffectTypes()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var result = g.Hit(amount: -0.6);
        result.Items.Add(g.Hit(Fire, -0.6).Items.Single());
        result.Items.Add(g.Hit(StunEffect, 1.6).Items.Single());

        Ok(g.Effects.Apply(g.Orc.Id, result));

        Current(g.Orc).ShouldBe(99);
        Current(g.Orc, Stun).ShouldBe(2);
        result.SumStat(HP, g.World).ShouldBe(-1);
        result.Sum(StunEffect).ShouldBe(2);
        result.Items.Select(x => x.Amount).ShouldBe(new[] { -1d, 0d, 2d });
    }

    [Fact]
    public void Apply_Should_RoundTheDeltaThenRespectExistingStatBounds()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        Set(g.Orc, HP, 10.25, 8.25);

        Ok(g.Effects.Apply(g.Orc.Id, g.Hit(Heal, 1.4)));
        Current(g.Orc).ShouldBe(9.25);
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit(Heal, 3.6)));
        Current(g.Orc).ShouldBe(10.25);
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit(amount: -20.6)));
        Current(g.Orc).ShouldBe(0);
    }

    [Fact]
    public void Attack_Should_KeepCalculationPrecisionUntilAfterMitigation()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData[0].Data["value"] = 5;
        var ward = g.Own("WardRing", RingLeft, g.Orc);
        ward.ModifierData = new() { Mod("preventpercent", Physical, 50, "percent", Scope.Passive, Phase.Incoming) };

        var reports = Ok(g.Effects.Attack(g.Hero.Id, sword.Id, g.Orc.Id));

        Current(g.Orc).ShouldBe(98);
        reports.Single().Sum(Physical).ShouldBe(-2);
    }

    [Fact]
    public void Tick_Should_RoundEachApplicationWithoutChangingStoredAmountsOrDurations()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var result = g.Hit(Poison, -2.5, 2);
        Ok(g.Effects.Apply(g.Orc.Id, result));
        Current(g.Orc).ShouldBe(100);
        g.Orc.Effects.Single().Items.Single().Amount.ShouldBe(-2.5);

        var first = g.Tick();
        Current(g.Orc).ShouldBe(98);
        first.Single().Sum(Poison).ShouldBe(-2);
        var stored = g.Orc.Effects.Single().Items.Single();
        stored.Amount.ShouldBe(-2.5);
        stored.Duration.ShouldBe(1);

        var second = g.Tick();
        Current(g.Orc).ShouldBe(96);
        second.Single().Sum(Poison).ShouldBe(-2);
        g.Orc.Effects.ShouldBeEmpty();
        result.Items.Single().Duration.ShouldBe(2);
    }

    [Fact]
    public void Steal_Should_AlsoApplyWholeAmountsToItsRecipient()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var dagger = g.Own("VampDagger", MainHand);
        dagger.ModifierData[0].Data["value"] = 3;
        g.Hero.Stats[HP].Current = 80;

        var reports = Ok(g.Effects.Attack(g.Hero.Id, dagger.Id, g.Orc.Id));

        Current(g.Orc).ShouldBe(97);
        Current(g.Hero).ShouldBe(82);
        reports.Single(x => x.TargetId == g.Hero.Id).Sum(Physical).ShouldBe(2);
    }

    [Fact]
    public void Use_Should_ReturnRoundedHealingAndPreserveReportIdentity()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var potion = g.Own("HealingPotion");
        potion.ModifierData[0].Data["value"] = 2.5;
        g.Hero.Stats[HP].Current = 50;

        var report = Ok(g.Effects.Use(g.Hero.Id, potion.Id, g.Hero.Id)).Single();

        Current(g.Hero).ShouldBe(52);
        report.Sum(Heal).ShouldBe(2);
        report.TargetId.ShouldBe(g.Hero.Id);
        report.SourceId.ShouldBe(g.Hero.Id);
        report.InstrumentId.ShouldBe(potion.Id);
        var item = report.Items.Single();
        item.SourceId.ShouldBe(g.Hero.Id);
        item.InstrumentId.ShouldBe(potion.Id);
        item.EffectTypeId.ShouldBe(Heal);
        item.Duration.ShouldBe(0);
    }

    [Fact]
    public void Tick_Should_GroupAlreadyRoundedApplicationsRatherThanRoundingTheCombinedReport()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit(Poison, -2.5, 2)));

        var reports = g.Tick(elapsed: 2);

        Current(g.Orc).ShouldBe(96);
        reports.Single().Sum(Poison).ShouldBe(-4);
        reports.Single().Items.Select(x => x.Amount).ShouldBe(new[] { -2d, -2d });
    }

    [Fact]
    public void Apply_Should_RoundOnlyImmediateContributionsInAMixedResult()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var result = g.Hit(Poison, -2.5);
        var timed = g.Hit(Poison, -1.5, 2.5).Items.Single();
        result.Items.Add(timed);

        Ok(g.Effects.Apply(g.Orc.Id, result));

        Current(g.Orc).ShouldBe(98);
        result.Items[0].Amount.ShouldBe(-2);
        result.Items[1].ShouldBeSameAs(timed);
        g.Orc.Effects.Single().Items.Single().Amount.ShouldBe(-1.5);
        g.Orc.Effects.Single().Items.Single().Duration.ShouldBe(2.5);
    }

    [Theory]
    [ClassData(typeof(Amounts))]
    public void Incoming_Should_ReturnFinalRoundedAmountsWithoutApplyingOrMutatingInput(bool round, double amount, double expected)
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = round;
        g.Orc.Stats[HP].Current = 50;
        var input = g.Hit(amount: amount);
        var before = g.Snapshot();

        var preview = Ok(g.Effects.Incoming(g.Orc.Id, input)).Single();

        preview.Sum(Physical).ShouldBe(expected);
        preview.Items.Single().Amount.ShouldBe(expected);
        input.Items.Single().Amount.ShouldBe(amount);
        g.Snapshot().ShouldBe(before);
        Ok(g.Effects.Apply(g.Orc.Id, preview));
        preview.Sum(Physical).ShouldBe(expected);
        Current(g.Orc).ShouldBe(50 + expected);
    }

    [Fact]
    public void Incoming_Should_RoundAfterMitigationAndKeepTimedInputsUnchanged()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var ward = g.Own("WardRing", RingLeft, g.Orc);
        ward.ModifierData = new() { Mod("preventpercent", Physical, 50, "percent", Scope.Passive, Phase.Incoming) };
        var input = g.Hit(amount: -5);
        input.Items.Add(g.Hit(Poison, -1.5, 2.5).Items.Single());
        var before = g.Snapshot();

        var preview = Ok(g.Effects.Incoming(g.Orc.Id, input)).Single();

        preview.Sum(Physical).ShouldBe(-2);
        preview.Items.Single(x => x.Duration > 0).Amount.ShouldBe(-1.5);
        preview.Items.Single(x => x.Duration > 0).Duration.ShouldBe(2.5);
        input.Sum(Physical).ShouldBe(-5);
        g.Snapshot().ShouldBe(before);
    }

    [Fact]
    public void Incoming_Should_ReturnRoundedStealBasedOnRoundedDamage()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var dagger = g.Own("VampDagger", MainHand);
        dagger.ModifierData[0].Data["value"] = 5.4;
        g.Hero.Stats[HP].Current = 50;
        var outgoing = g.Out(dagger).Single();
        var before = g.Snapshot();

        var previews = Ok(g.Effects.Incoming(g.Orc.Id, outgoing));

        outgoing.Sum(Physical).ShouldBe(-5.4);
        previews.Single(x => x.TargetId == g.Orc.Id).Sum(Physical).ShouldBe(-5);
        previews.Single(x => x.TargetId == g.Hero.Id).Sum(Physical).ShouldBe(2);
        g.Snapshot().ShouldBe(before);
        foreach (var preview in previews)
            Ok(g.Effects.Apply(preview.TargetId!.Value, preview));
        Current(g.Orc).ShouldBe(95);
        Current(g.Hero).ShouldBe(52);
    }

    [Fact]
    public void Incoming_Should_KeepPreviewCopiesConsistentWithDeferredApplication()
    {
        var g = new Game();
        g.World.Settings.RoundEffectAmounts = true;
        var input = g.Hit(amount: -0.6);
        input.Items.Add(g.Hit(Fire, -0.6).Items.Single());
        var pending = Ok(g.Effects.Incoming(g.Orc.Id, input)).Single();
        var presentation = new EffectResult
        {
            SourceId = pending.SourceId, TargetId = pending.TargetId, InstrumentId = pending.InstrumentId,
            Items = pending.Items.Select(x => x with { }).ToList()
        };

        presentation.SumStat(HP, g.World).ShouldBe(-1);
        presentation.Items.Select(x => x.Amount).ShouldBe(new[] { -1d, 0d });
        Current(g.Orc).ShouldBe(100);
        Ok(g.Effects.Apply(g.Orc.Id, pending));
        presentation.Items.ShouldBe(pending.Items);
        Current(g.Orc).ShouldBe(99);
    }

    public sealed class Amounts : TheoryData<bool, double, double>
    {
        public Amounts()
        {
            Add(false, 1.6, 1.6);
            Add(false, -1.6, -1.6);
            Add(true, 0, 0);
            Add(true, 0.4, 0);
            Add(true, -0.4, 0);
            Add(true, 0.5, 0);
            Add(true, -0.5, 0);
            Add(true, 1.4, 1);
            Add(true, -1.4, -1);
            Add(true, 1.5, 2);
            Add(true, -1.5, -2);
            Add(true, 1.6, 2);
            Add(true, -1.6, -2);
            Add(true, 2.5, 2);
            Add(true, -2.5, -2);
            Add(true, 3.5, 4);
            Add(true, -3.5, -4);
        }
    }
}
