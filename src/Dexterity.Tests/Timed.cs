using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // APP-01: Given the Orc at HP 100, when `Physical −10` is applied, then HP is 90.
    [Fact]
    public void Apply_Should_ChangeHealthByPhysicalDamage()
    {
        var g = new Game();
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit()));
        Current(g.Orc).ShouldBe(90);
    }

    // RSL-02: Given a valid EffectResult containing `Physical −10` for the Orc at HP 100, when Apply is called, then it returns a successful non-generic Result and the Orc has HP 90. Success does not require a data payload (APP-01).
    [Fact]
    public void Apply_Should_ReturnASuccessfulNonGenericResult()
    {
        var g = new Game();
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit()));
        Current(g.Orc).ShouldBe(90);
    }

    // APP-02: When `Physical −10, Fire −4` is applied, then HP is 86.
    [Fact]
    public void Apply_Should_SumDamageTypesTargetingTheSameStat()
    {
        var g = new Game();
        var hit = g.Hit();
        hit.Items.Add(g.Hit(Fire, -4).Items[0]);
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        Current(g.Orc).ShouldBe(86);
    }

    // APP-03: Given Hero at HP 95/100, when `Heal +10` is applied, then HP is 100 (D18).
    [Fact]
    public void Apply_Should_ClampHealingAtModifiedMaximum()
    {
        var g = new Game();
        g.Hero.Stats[HP].Current = 95;
        Ok(g.Effects.Apply(g.Hero.Id, g.Hit(Heal, 10, target: g.Hero)));
        Current(g.Hero).ShouldBe(100);
    }

    // APP-04: Given the Orc at HP 5, when `Physical −10` is applied, then HP is 0 (D18).
    [Fact]
    public void Apply_Should_ClampDamageAtZero()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 5;
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit()));
        Current(g.Orc).ShouldBe(0);
    }

    // APP-05: Apply changes only `Current`. Max, ModifiedMax and Bonus are unchanged.
    [Fact]
    public void Apply_Should_ChangeOnlyCurrentStatValues()
    {
        var g = new Game();
        var stat = g.Orc.Stats[HP];
        var before = (stat.Max, stat.ModifiedMax, stat.Bonus);
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit()));
        (stat.Max, stat.ModifiedMax, stat.Bonus).ShouldBe(before);
        Current(g.Orc).ShouldBe(90);
    }

    // APP-06: Given an EffectResult containing items with `Duration > 0`, when Apply runs, then it stores a copy of that EffectResult containing only those timed items in `Component.Effects`, retaining TargetId, SourceId, InstrumentId and item provenance Ids. Those items do not change HP until Tick (D15, D36).
    [Fact]
    public void Apply_Should_StoreIndependentTimedCopiesWithProvenance()
    {
        var g = new Game();
        var bomb = g.Make("FireBomb");
        var hit = g.Hit(Fire, -3, instrument: bomb);
        hit.Items.Add(g.Hit(Fire, -1, 2, instrument: bomb).Items[0]);
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        Current(g.Orc).ShouldBe(97);
        var stored = g.Orc.Effects.Single();
        stored.ShouldNotBeSameAs(hit);
        stored.TargetId.ShouldBe(g.Orc.Id);
        stored.SourceId.ShouldBe(g.Hero.Id);
        stored.InstrumentId.ShouldBe(bomb.Id);
        stored.Items.ShouldBe(new[] { hit.Items[1] });
        stored.Items.ShouldNotBeSameAs(hit.Items);
    }

    // APP-11: Given an item with Duration 0 and one with Duration 3 in the same result, then only the first changes HP now. The stored EffectResult contains only the Duration 3 item, with the original result's TargetId, SourceId and InstrumentId.
    [Fact]
    public void Apply_Should_SeparateImmediateAndTimedContributions()
    {
        var g = new Game();
        var bomb = g.Make("FireBomb");
        var hit = g.Hit(Fire, -3, instrument: bomb);
        hit.Items.Add(g.Hit(Fire, -1, 2, instrument: bomb).Items[0]);
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        Current(g.Orc).ShouldBe(97);
        var stored = g.Orc.Effects.Single();
        stored.ShouldNotBeSameAs(hit);
        stored.TargetId.ShouldBe(g.Orc.Id);
        stored.SourceId.ShouldBe(g.Hero.Id);
        stored.InstrumentId.ShouldBe(bomb.Id);
        stored.Items.ShouldBe(new[] { hit.Items[1] });
        stored.Items.ShouldNotBeSameAs(hit.Items);
    }

    // APP-07: Given an item whose stat the target lacks (a Physical item applied to the Longsword), then it is ignored, other items are still applied, and there is no error (A8).
    [Fact]
    public void Apply_Should_IgnoreMissingTargetStats()
    {
        var g = new Game();
        var sword = g.Make("Longsword");
        var hit = g.Hit(target: sword);
        hit.Items.Add(g.Hit(Recharge, 1, target: sword).Items[0]);
        Set(sword, Charges, 3, 1);
        Ok(g.Effects.Apply(sword.Id, hit));
        sword.Stats.ShouldNotContainKey(HP);
        Current(sword, Charges).ShouldBe(2);
    }

    // APP-08: Given an empty result, then nothing changes.
    [Fact]
    public void Apply_Should_NotChangeStateForEmptyResults()
    {
        var g = new Game();
        var hit = g.Hit();
        hit.Items.Clear();
        var before = g.Snapshot();
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        g.Snapshot().ShouldBe(before);
    }

    // APP-09: Given the Orc at STUN 0/10, when `Stun +2` is applied twice, then STUN is 4 (D34, A12).
    [Fact]
    public void Apply_Should_StackImmediateStunAmounts()
    {
        var g = new Game();
        var hit = g.Hit(StunEffect, 2);
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        Current(g.Orc, Stun).ShouldBe(4);
    }

    // APP-10: Given Hero at STR 10/10, when `StrBonus −2` (drain) is applied, then STR Current is 8 and ModifiedMax is 10 (A13).
    [Fact]
    public void Apply_Should_DrainStrengthWithoutChangingMaximum()
    {
        var g = new Game();
        Ok(g.Effects.Apply(g.Hero.Id, g.Hit(StrBonus, -2, target: g.Hero)));
        Current(g.Hero, Str).ShouldBe(8);
        g.Hero.Stats[Str].ModifiedMax.ShouldBe(10);
    }

    // APP-12: Given `result.TargetId` doesn't match targetId, including a null TargetId, then the call fails and nothing changes.
    [Fact]
    public void Apply_Should_RejectMismatchedAndMissingTargetIds()
    {
        var g = new Game();
        foreach (var target in new int? []
        {
            null,
            g.Goblin1.Id
        }

        )
        {
            var hit = g.Hit();
            hit.TargetId = target;
            var before = g.Snapshot();
            Fail(g.Effects.Apply(g.Orc.Id, hit));
            g.Snapshot().ShouldBe(before);
        }
    }

    // RSL-04: Given an EffectResult targeting Goblin1, when Apply is called for the Orc, then it returns a failed Result without throwing, and neither component's stats or stored effects change (A5, APP-12).
    [Fact]
    public void InvalidApply_Should_PreserveAllTargetState()
    {
        var g = new Game();
        foreach (var target in new int? []
        {
            null,
            g.Goblin1.Id
        }

        )
        {
            var hit = g.Hit();
            hit.TargetId = target;
            var before = g.Snapshot();
            Fail(g.Effects.Apply(g.Orc.Id, hit));
            g.Snapshot().ShouldBe(before);
        }
    }

    // APP-13: Given an unknown targetId or a null result (separate cases), then the call fails.
    [Fact]
    public void Apply_Should_RejectUnknownTargetsAndNullResults()
    {
        var g = new Game();
        var before = g.Snapshot();
        Fail(g.Effects.Apply(999, g.Hit()));
        Fail(g.Effects.Apply(g.Orc.Id, null !));
        g.Snapshot().ShouldBe(before);
    }

    // APP-14: Given the Orc at STUN 9/10, when `Stun +2` is applied, then STUN is 10 (clamped like any Pool, A12).
    [Fact]
    public void Apply_Should_ClampCountersAtModifiedMaximum()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 9;
        Ok(g.Effects.Apply(g.Orc.Id, g.Hit(StunEffect, 2)));
        Current(g.Orc, Stun).ShouldBe(10);
    }

    // APP-15: Given one EffectResult contains `Poison −2 × 3`, when it is applied twice, then two independent stored EffectResults are created. After a tick, both stored durations are 2 while the earlier input result still has Duration 3 and the original amount and provenance. No stored result or item aliases the input for countdown purposes (A11, A14).
    [Fact]
    public void Apply_Should_StoreEachApplicationIndependently()
    {
        var g = new Game();
        var hit = g.Hit(Poison, -2, 3);
        var original = System.Text.Json.JsonSerializer.Serialize(hit);
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        g.Orc.Effects.Count.ShouldBe(2);
        g.Orc.Effects[0].ShouldNotBeSameAs(g.Orc.Effects[1]);
        g.Tick();
        g.Orc.Effects.ShouldAllBe(x => x.Items[0].Duration == 2);
        System.Text.Json.JsonSerializer.Serialize(hit).ShouldBe(original);
    }

    // DOT-01: After Apply, `Orc.Effects` contains one EffectResult with TargetId Orc, SourceId Hero and InstrumentId PoisonDagger. Its only item is `Poison −2, remaining 3, SourceId Hero, InstrumentId PoisonDagger`; no immediate Physical item is stored (D15, D33).
    [Fact]
    public void PoisonDagger_Should_StoreActionOrigins()
    {
        var g = new Game();
        var dagger = g.Own("PoisonDagger", MainHand);
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        var stored = g.Orc.Effects.Single();
        stored.TargetId.ShouldBe(g.Orc.Id);
        stored.SourceId.ShouldBe(g.Hero.Id);
        stored.InstrumentId.ShouldBe(dagger.Id);
        stored.Items.Single().ShouldBe(new EffectResultItem { EffectTypeId = Poison, Amount = -2, Duration = 3, SourceId = g.Hero.Id, InstrumentId = dagger.Id });
    }

    // DOT-02: `Tick(Orc, 1)` three times gives HP 94, 92, 90 and remaining 2, 1, 0. After the third tick the entry is removed.
    [Fact]
    public void Poison_Should_ApplyDamageAndCountDownEachTick()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 96;
        g.Store(g.Hit(Poison, -2, 3));
        for (var i = 1; i <= 3; i++)
        {
            g.Tick();
            Current(g.Orc).ShouldBe(96 - 2 * i);
            if (i < 3)
                g.Orc.Effects.Single().Items[0].Duration.ShouldBe(3 - i);
        }

        g.Orc.Effects.ShouldBeEmpty();
        g.Tick().ShouldBeEmpty();
        Current(g.Orc).ShouldBe(90);
    }

    // DOT-03: A fourth `Tick(Orc, 1)` changes nothing and returns an empty list.
    [Fact]
    public void ExpiredPoison_Should_NotApplyAgain()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 96;
        g.Store(g.Hit(Poison, -2, 3));
        for (var i = 1; i <= 3; i++)
        {
            g.Tick();
            Current(g.Orc).ShouldBe(96 - 2 * i);
            if (i < 3)
                g.Orc.Effects.Single().Items[0].Duration.ShouldBe(3 - i);
        }

        g.Orc.Effects.ShouldBeEmpty();
        g.Tick().ShouldBeEmpty();
        Current(g.Orc).ShouldBe(90);
    }

    // DOT-04: `Tick(Orc, 0.5)` is one partial step: remaining 3 > 0, so it deals −2 (HP 94, remaining 2.5). A second `Tick(Orc, 0.5)` deals −2 again (HP 92, remaining 2) (D17).
    [Fact]
    public void PartialTick_Should_ApplyFullStoredAmounts()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 96;
        g.Store(g.Hit(Poison, -2, 3));
        g.Tick(elapsed: .5);
        Current(g.Orc).ShouldBe(94);
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(2.5);
        g.Tick(elapsed: .5);
        Current(g.Orc).ShouldBe(92);
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(2);
    }

    // DOT-05: `Tick(Orc, 3)` runs three steps and deals −2 three times (HP 90), and the entry is removed.
    [Fact]
    public void Tick_Should_ProcessEachUnitStep()
    {
        foreach (var elapsed in new[]
        {
            3d,
            5d
        }

        )
        {
            var g = new Game();
            g.Orc.Stats[HP].Current = 96;
            g.Store(g.Hit(Poison, -2, 3));
            g.Tick(elapsed: elapsed);
            Current(g.Orc).ShouldBe(90);
            g.Orc.Effects.ShouldBeEmpty();
        }
    }

    // DOT-06: `Tick(Orc, 5)` deals only three times (HP 90). After the third step remaining is 0, so the effect is removed and later steps deal nothing.
    [Fact]
    public void Tick_Should_StopApplyingExpiredItemsDuringLongIntervals()
    {
        foreach (var elapsed in new[]
        {
            3d,
            5d
        }

        )
        {
            var g = new Game();
            g.Orc.Stats[HP].Current = 96;
            g.Store(g.Hit(Poison, -2, 3));
            g.Tick(elapsed: elapsed);
            Current(g.Orc).ShouldBe(90);
            g.Orc.Effects.ShouldBeEmpty();
        }
    }

    // DOT-07: Given two applications, then there are two stored EffectResults, each containing its own Poison item, and with no Poison prevention each tick deals −4 (A11).
    [Fact]
    public void RepeatedPoisonApplications_Should_RemainSeparate()
    {
        var g = new Game();
        g.Store(g.Hit(Poison, -2, 3));
        g.Store(g.Hit(Poison, -2, 3));
        g.Tick();
        Current(g.Orc).ShouldBe(96);
        g.Orc.Effects.Count.ShouldBe(2);
    }

    // DOT-08: Given PoisonPlateArmour (PreventFlat Poison 1) is equipped after the poison was stored, then the next tick resolves `Poison −2` to −1, taking the Orc from HP 96 to 95. The stored amount remains −2; the armour is evaluated on that tick (D16).
    [Fact]
    public void PoisonTicks_Should_UseNewlyEquippedArmour()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 96;
        g.Store(g.Hit(Poison, -2, 3));
        var armour = g.Make("PoisonPlateArmour");
        Ok(g.Relations.Add(g.Orc.Id, armour.Id));
        Ok(g.Relations.Equip(g.Orc.Id, armour.Id, Body));
        g.Tick()[0].Sum(Poison).ShouldBe(-1);
        Current(g.Orc).ShouldBe(95);
        g.Orc.Effects[0].Items[0].Amount.ShouldBe(-2);
        Ok(g.Relations.Unequip(g.Orc.Id, armour.Id));
        g.Tick()[0].Sum(Poison).ShouldBe(-2);
        Current(g.Orc).ShouldBe(93);
    }

    // DOT-18: Given the stored `Poison −2 × 3` and PoisonPlateArmour, when one tick runs, then damage is −1 while the stored amount stays −2 and duration becomes 2. If the armour is unequipped before the next tick, that tick deals −2 rather than −1 (D16, D36).
    [Fact]
    public void PoisonArmour_Should_NotRewriteStoredAmounts()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 96;
        g.Store(g.Hit(Poison, -2, 3));
        var armour = g.Make("PoisonPlateArmour");
        Ok(g.Relations.Add(g.Orc.Id, armour.Id));
        Ok(g.Relations.Equip(g.Orc.Id, armour.Id, Body));
        g.Tick()[0].Sum(Poison).ShouldBe(-1);
        Current(g.Orc).ShouldBe(95);
        g.Orc.Effects[0].Items[0].Amount.ShouldBe(-2);
        Ok(g.Relations.Unequip(g.Orc.Id, armour.Id));
        g.Tick()[0].Sum(Poison).ShouldBe(-2);
        Current(g.Orc).ShouldBe(93);
    }

    // DOT-09: `Tick(Orc, 1)` returns one result with `TargetId = Orc`, `SourceId = Hero`, `InstrumentId = PoisonDagger`, and one item `Poison −2, Duration 0`. `Sum(HP) = −2` (A18).
    [Fact]
    public void Tick_Should_ReturnImmediateContributionsWithActionOrigins()
    {
        var g = new Game();
        var dagger = g.Make("PoisonDagger");
        g.Store(g.Hit(Poison, -2, 3, instrument: dagger));
        var result = g.Tick().Single();
        result.TargetId.ShouldBe(g.Orc.Id);
        result.SourceId.ShouldBe(g.Hero.Id);
        result.InstrumentId.ShouldBe(dagger.Id);
        result.Items.Single().Duration.ShouldBe(0);
        result.SumStat(HP, g.World).ShouldBe(-2);
    }

    // DOT-10: Given RegenPotion on Hero at HP 80, then `Tick(Hero, 1)` twice gives HP 85, then 90.
    [Fact]
    public void RegenPotion_Should_HealOnEachTick()
    {
        var g = new Game();
        g.Hero.Stats[HP].Current = 80;
        var potion = g.Own("RegenPotion");
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, potion.Id, g.Hero.Id));
        g.Tick(g.Hero);
        Current(g.Hero).ShouldBe(85);
        g.Tick(g.Hero);
        Current(g.Hero).ShouldBe(90);
    }

    // DOT-11: Given the Orc at HP 1 with a poison tick, then HP is 0 and the effect keeps ticking until it expires.
    [Fact]
    public void Poison_Should_KeepTickingAfterHealthReachesZero()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 1;
        g.Store(g.Hit(Poison, -2, 3));
        g.Tick();
        Current(g.Orc).ShouldBe(0);
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(2);
        g.Tick();
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(1);
        g.Tick();
        g.Orc.Effects.ShouldBeEmpty();
    }

    // DOT-16: Given the Orc dies (HP 0), then nothing is removed from Effects automatically (death is out of scope).
    [Fact]
    public void Death_Should_NotRemovePendingEffects()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 1;
        g.Store(g.Hit(Poison, -2, 3));
        g.Tick();
        Current(g.Orc).ShouldBe(0);
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(2);
        g.Tick();
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(1);
        g.Tick();
        g.Orc.Effects.ShouldBeEmpty();
    }

    // DOT-12: Given the Orc at STUN 2 with nothing in Effects, then `Tick(Orc, 1)` gives STUN 1, then 0, then 0 (counts down to a floor of 0, A12).
    [Fact]
    public void Tick_Should_CountDownStunWithoutStoredEffects()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 2;
        foreach (var expected in new[]
        {
            1d,
            0d,
            0d
        }

        )
        {
            g.Tick();
            Current(g.Orc, Stun).ShouldBe(expected);
        }
    }

    // DOT-13: `Tick(Orc, 0)` runs no steps and changes nothing.
    [Fact]
    public void ZeroElapsedTick_Should_NotChangeState()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 2;
        g.Store(g.Hit(Poison, -2, 3));
        var before = g.Snapshot();
        g.Tick(elapsed: 0).ShouldBeEmpty();
        g.Snapshot().ShouldBe(before);
    }

    // RSL-08: Given the Orc has stored poison and STUN 2, when `Tick(Orc, 0)` is called, then it returns a successful `Result<List<EffectResult>>` with Data equal to an empty list. HP, STUN and stored effect amounts and durations are unchanged (DOT-13).
    [Fact]
    public void ZeroElapsedTick_Should_ReturnASuccessfulEmptyList()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 2;
        g.Store(g.Hit(Poison, -2, 3));
        var before = g.Snapshot();
        g.Tick(elapsed: 0).ShouldBeEmpty();
        g.Snapshot().ShouldBe(before);
    }

    // DOT-14: Tick on a component with no stored Effects and no positive Counter stats changes nothing and returns a successful Result with an empty Data list.
    [Fact]
    public void Tick_Should_ReturnEmptyResultsWithoutEffectsOrCounters()
    {
        var g = new Game();
        var before = g.Snapshot();
        g.Tick().ShouldBeEmpty();
        g.Snapshot().ShouldBe(before);
    }

    // DOT-15: `Tick(Orc, −1)` fails and nothing changes.
    [Fact]
    public void NegativeElapsedTick_Should_FailWithoutMutation()
    {
        var g = new Game();
        g.Store(g.Hit(Poison, -2, 3));
        var before = g.Snapshot();
        Fail(g.Effects.Tick(g.Orc.Id, -1));
        g.Snapshot().ShouldBe(before);
    }

    // DOT-17: Given Hero is at HP 80 and wears BlightRing, when RegenPotion is used on Hero and applied, then one EffectResult containing raw `Heal +5 × 2` is stored and HP stays 80. The first tick heals 2.5 (HP 82.5). If BlightRing is then unequipped, the second tick heals 5 (HP 87.5), and the stored result expires (D16, D36).
    [Fact]
    public void RegenPotion_Should_NotHealDuringInitialApplication()
    {
        var g = new Game();
        g.Hero.Stats[HP].Current = 80;
        var ring = g.Own("BlightRing", RingLeft);
        var potion = g.Own("RegenPotion");
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, potion.Id, g.Hero.Id));
        Current(g.Hero).ShouldBe(80);
        g.Hero.Effects[0].Items.Single().Amount.ShouldBe(5);
        g.Tick(g.Hero);
        Current(g.Hero).ShouldBe(82.5);
        Ok(g.Relations.Unequip(g.Hero.Id, ring.Id));
        g.Tick(g.Hero);
        Current(g.Hero).ShouldBe(87.5);
        g.Hero.Effects.ShouldBeEmpty();
    }

    // DOT-19: Given the Orc has the PoisonDagger's poison and the FireBomb's burn stored, when `Tick(Orc, 1)` runs, then it returns two results: one with `InstrumentId = PoisonDagger` (`Poison −2`) and one with `InstrumentId = FireBomb` (`Fire −1`).
    [Fact]
    public void Tick_Should_KeepResultsFromDifferentInstrumentsSeparate()
    {
        var g = new Game();
        var dagger = g.Make("PoisonDagger");
        var bomb = g.Make("FireBomb");
        g.Store(g.Hit(Poison, -2, 3, instrument: dagger));
        g.Store(g.Hit(Fire, -1, 2, instrument: bomb));
        var results = g.Tick();
        results.Count.ShouldBe(2);
        results.Single(x => x.InstrumentId == dagger.Id).Sum(Poison).ShouldBe(-2);
        results.Single(x => x.InstrumentId == bomb.Id).Sum(Fire).ShouldBe(-1);
    }

    // DOT-20: Given the PoisonDagger's poison was applied twice, when `Tick(Orc, 1)` runs with no Poison prevention, then the two stored applications are resolved independently and the returned output is grouped into **one** result (same TargetId Orc, SourceId Hero and InstrumentId PoisonDagger) with two `Poison −2` items and `Sum(HP) = −4`.
    [Fact]
    public void Tick_Should_GroupIndependentApplicationsWithSharedOrigins()
    {
        var g = new Game();
        g.Store(g.Hit(Poison, -2, 3));
        g.Store(g.Hit(Poison, -2, 3));
        var result = g.Tick().Single();
        result.Items.Count.ShouldBe(2);
        result.SumStat(HP, g.World).ShouldBe(-4);
    }

    // DOT-21: Given the Orc at STUN 2 and nothing in Effects, when `Tick(Orc, 1)` runs, then it returns an empty list, while STUN still drops to 1 (counter countdowns aren't reported, A18).
    [Fact]
    public void CounterCountdown_Should_NotProduceEffectResults()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 2;
        g.Tick().ShouldBeEmpty();
        Current(g.Orc, Stun).ShouldBe(1);
    }

    // RSL-09: Given the Orc has STUN 2 and no stored Effects, when `Tick(Orc, 1)` is called, then it returns a successful `Result<List<EffectResult>>` with Data equal to an empty list while STUN drops to 1. Empty effect output does not imply failure or absence of state changes (A18, DOT-21).
    [Fact]
    public void CounterOnlyTick_Should_ReturnASuccessfulEmptyList()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 2;
        g.Tick().ShouldBeEmpty();
        Current(g.Orc, Stun).ShouldBe(1);
    }

    // DOT-22: Given the StunGrenade at the Orc, after the full pipeline the Orc's STUN is 2, nothing is added to `Orc.Effects` (Duration 0), and the result item is `Stun +2 (SourceId Hero, InstrumentId StunGrenade)`.
    [Fact]
    public void StunGrenade_Should_ApplyStunWithoutTimedStorage()
    {
        var g = new Game();
        var grenade = g.Own("StunGrenade");
        var results = Ok(Pipeline.Run(g.Effects, g.Hero.Id, grenade.Id, g.Orc.Id));
        Current(g.Orc, Stun).ShouldBe(2);
        g.Orc.Effects.ShouldBeEmpty();
        results[0].Items.Single().ShouldBe(new EffectResultItem { EffectTypeId = StunEffect, Amount = 2, SourceId = g.Hero.Id, InstrumentId = grenade.Id });
    }

    // DOT-23: Given the Orc at STUN 2, then `Tick(Orc, 0.5)` gives 1.5, `Tick(Orc, 1)` gives 0.5, and `Tick(Orc, 1)` gives 0 (no rounding, floor 0, D17).
    [Fact]
    public void Tick_Should_CountDownFractionalStunWithoutRounding()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 2;
        g.Tick(elapsed: .5);
        Current(g.Orc, Stun).ShouldBe(1.5);
        g.Tick();
        Current(g.Orc, Stun).ShouldBe(.5);
        g.Tick();
        Current(g.Orc, Stun).ShouldBe(0);
    }

    // DOT-24: Given HP (Pool) and STR (Fixed), then Tick never lowers them on its own. Only Counter stats count down (A12).
    [Fact]
    public void Tick_Should_NotCountDownPoolOrFixedStats()
    {
        var g = new Game();
        var hp = Current(g.Orc);
        var str = Current(g.Orc, Str);
        g.Tick(elapsed: 5);
        Current(g.Orc).ShouldBe(hp);
        Current(g.Orc, Str).ShouldBe(str);
    }

    // DOT-25: Given a stored `Poison −2` with Duration 2.5, then three `Tick(Orc, 1)` calls deal −2 each (it triggers 3 times, −6 in total), with remaining 1.5, then 0.5, then removed. The third tick still triggers because 0.5 > 0 (D17).
    [Fact]
    public void FractionalDuration_Should_TriggerWhileAnyDurationRemains()
    {
        var g = new Game();
        g.Store(g.Hit(Poison, -2, 2.5));
        foreach (var remaining in new[]
        {
            1.5,
            .5
        }

        )
        {
            g.Tick();
            g.Orc.Effects[0].Items[0].Duration.ShouldBe(remaining);
        }

        g.Tick();
        g.Orc.Effects.ShouldBeEmpty();
        Current(g.Orc).ShouldBe(94);
    }

    // DOT-26: Given the same `Poison −2 × 2.5`, then one `Tick(Orc, 2.5)` runs steps 1, 1 and 0.5 and also deals −6, the same as DOT-25.
    [Fact]
    public void LongPartialTick_Should_MatchSeparateUnitTicks()
    {
        var g = new Game();
        g.Store(g.Hit(Poison, -2, 2.5));
        g.Tick(elapsed: 2.5);
        Current(g.Orc).ShouldBe(94);
        g.Orc.Effects.ShouldBeEmpty();
    }

    // DOT-27: Given the Orc at STUN 0 with a stored `Stun +1 × 3` (an over-time stun, like any other effect type), then each of three `Tick(Orc, 1)` calls applies +1 (STUN 1) and then counts STUN down, so STUN is 0 after each call (A34). The effect is removed after the third call.
    [Fact]
    public void TimedStun_Should_ApplyBeforeCounterCountdown()
    {
        var g = new Game();
        g.Store(g.Hit(StunEffect, 1, 3));
        for (var i = 0; i < 3; i++)
        {
            g.Tick();
            Current(g.Orc, Stun).ShouldBe(0);
        }

        g.Orc.Effects.ShouldBeEmpty();
    }

    // DOT-28: Given the Orc at STUN 2 with a stored `Stun +1 × 1`, then `Tick(Orc, 1)` applies +1 (STUN 3) and then counts down (STUN 2), and the effect is removed.
    [Fact]
    public void ExpiringTimedStun_Should_ApplyBeforeRemoval()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 2;
        g.Store(g.Hit(StunEffect, 1, 1));
        g.Tick();
        Current(g.Orc, Stun).ShouldBe(2);
        g.Orc.Effects.ShouldBeEmpty();
    }

    // DOT-29: Given the stored result from INC-11, when `Tick(Orc, 1)` runs, then it returns one EffectResult with TargetId Orc, SourceId Hero and InstrumentId PoisonDagger. Its items are `Poison −2` from Hero/PoisonDagger and `Poison +1` from Orc/PoisonPlateArmour, both at Duration 0, so `Sum(Poison) = −1`. The armour contribution is visible; the stored result still contains only its original timed item, now at duration 2 (D36, A18).
    [Fact]
    public void Tick_Should_PreserveStoredActionIdentity()
    {
        var g = new Game();
        var dagger = g.Make("PoisonDagger");
        var armour = g.Own("PoisonPlateArmour", Body, g.Orc);
        g.Store(g.Hit(Poison, -2, 3, instrument: dagger));
        var result = g.Tick().Single();
        result.TargetId.ShouldBe(g.Orc.Id);
        result.SourceId.ShouldBe(g.Hero.Id);
        result.InstrumentId.ShouldBe(dagger.Id);
        result.Items.ShouldContain(x => x.Amount == -2 && x.SourceId == g.Hero.Id && x.InstrumentId == dagger.Id && x.Duration == 0);
        result.Items.ShouldContain(x => x.Amount == 1 && x.SourceId == g.Orc.Id && x.InstrumentId == armour.Id && x.Duration == 0);
        result.Sum(Poison).ShouldBe(-1);
        g.Orc.Effects[0].Items.Count.ShouldBe(1);
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(2);
    }

    // DOT-30: Given one stored EffectResult contains `Poison −2 × 1` and `Fire −1 × 3`, and the Orc has no Incoming modifiers, when it ticks once, then both amounts apply, the Poison item expires, and the result remains stored with only `Fire −1 × 2`. Two more unit ticks apply only Fire and then remove the empty stored result. Expired items are never sent to Incoming again (A11).
    [Fact]
    public void Tick_Should_ExpireItemsIndividuallyWithinStoredResults()
    {
        var g = new Game();
        var hit = g.Hit(Poison, -2, 1);
        hit.Items.Add(g.Hit(Fire, -1, 3).Items[0]);
        g.Store(hit);
        g.Tick();
        Current(g.Orc).ShouldBe(97);
        g.Orc.Effects.Single().Items.Single().EffectTypeId.ShouldBe(Fire);
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(2);
        g.Tick();
        Current(g.Orc).ShouldBe(96);
        g.Tick();
        Current(g.Orc).ShouldBe(95);
        g.Orc.Effects.ShouldBeEmpty();
    }

    // DOT-31: Given a Ruby variant is Connected to FireBomb instead of Longsword, when FireBomb is used and applied, then the stored result contains the original `Fire −1 × 2` and Ruby's separate `Fire −1 × 2` contribution, retaining their item provenance Ids and the action InstrumentId FireBomb. If Ruby is detached after Apply and before Consume, FireBomb is then consumed, and the Orc wears WardRing, the next tick still resolves the stored total −2 to −1. The source's Outgoing amount is neither recalculated nor multiplied again (D16, D36).
    [Fact]
    public void TimedMultiplierContributions_Should_KeepDurationAndProvenance()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        var ruby = g.Make("Ruby");
        g.Link(bomb, ruby);
        g.Own("WardRing", RingLeft, g.Orc);
        foreach (var resolved in g.In(g.Out(bomb)[0]))
            Ok(g.Effects.Apply(resolved.TargetId!.Value, resolved));
        var stored = g.Orc.Effects.Single();
        stored.Items.Count.ShouldBe(2);
        stored.Items.ShouldAllBe(x => x.Amount == -1 && x.Duration == 2);
        stored.Items.Select(x => x.InstrumentId).ShouldBe(new[] { bomb.Id, ruby.Id });
        stored.InstrumentId.ShouldBe(bomb.Id);
        Ok(g.Relations.Remove(bomb.Id, ruby.Id));
        Ok(g.Effects.Consume(bomb.Id));
        g.Tick()[0].Sum(Fire).ShouldBe(-1);
    }

    // DOT-32: Given the Orc at HP 100 wears PoisonPlateArmour and has **one stored EffectResult** containing two `Poison −2 × 3` items, when it ticks once, then Incoming receives both active items together and applies flat prevention 1 only once: `−4 + 1 = −3`, leaving HP 97. Both stored amounts stay −2 and both durations become 2 (D12).
    [Fact]
    public void TickPrevention_Should_ShareOneBudgetWithinEachStoredResult()
    {
        var g = new Game();
        g.Own("PoisonPlateArmour", Body, g.Orc);
        var hit = g.Hit(Poison, -2, 3);
        hit.Items.Add(hit.Items[0] with { });
        g.Store(hit);
        g.Tick();
        Current(g.Orc).ShouldBe(97);
        g.Orc.Effects[0].Items.ShouldAllBe(x => x.Amount == -2 && x.Duration == 2);
    }

    // DOT-33: Given the Orc at HP 100 wears PoisonPlateArmour and has **two stored EffectResults** from the same SourceId and InstrumentId, each containing one `Poison −2 × 3`, when it ticks once, then each stored result receives its own Incoming calculation and prevention budget: `(−2 + 1) + (−2 + 1) = −2`, leaving HP 98. Only afterwards are their outputs grouped into one returned EffectResult, containing both original and both prevention items (D12, A18).
    [Fact]
    public void TickPrevention_Should_UseSeparateBudgetsAcrossStoredResults()
    {
        var g = new Game();
        g.Own("PoisonPlateArmour", Body, g.Orc);
        g.Store(g.Hit(Poison, -2, 3));
        g.Store(g.Hit(Poison, -2, 3));
        var result = g.Tick().Single();
        Current(g.Orc).ShouldBe(98);
        result.Items.Count.ShouldBe(4);
        result.Items.Count(x => x.Amount == -2).ShouldBe(2);
        result.Items.Count(x => x.Amount == 1).ShouldBe(2);
    }

    // DOT-34: Given LeechDagger's timed Poison is stored, the Orc at HP 100 wears PoisonPlateArmour, and Hero has HP 80, when `Tick(Orc, 1)` runs, then it applies damage −1 to the Orc (HP 99) and Steal `Poison +0.5` to Hero (HP 80.5). It returns two EffectResults: one targeting Orc with `−2` and `+1`, and one targeting Hero with `+0.5`. Both retain SourceId Hero and InstrumentId LeechDagger; different TargetIds prevent them from being merged. All returned items have Duration 0, and no Steal item is added to stored Effects (A10, A18).
    [Fact]
    public void TimedSteal_Should_UsePostMitigationDamage()
    {
        var g = new Game();
        var dagger = g.Own("LeechDagger", MainHand);
        g.Own("PoisonPlateArmour", Body, g.Orc);
        g.Hero.Stats[HP].Current = 80;
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        var results = g.Tick();
        results.Count.ShouldBe(2);
        Current(g.Orc).ShouldBe(99);
        Current(g.Hero).ShouldBe(80.5);
        results.Single(x => x.TargetId == g.Orc.Id).Items.Select(x => x.Amount).ShouldBe(new[] { -2d, 1d });
        results.Single(x => x.TargetId == g.Hero.Id).Sum(Poison).ShouldBe(.5);
        results.ShouldAllBe(x => x.SourceId == g.Hero.Id && x.InstrumentId == dagger.Id);
        results.SelectMany(x => x.Items).ShouldAllBe(x => x.Duration == 0);
        g.Orc.Effects[0].Items.Count.ShouldBe(1);
    }

    // DOT-35: Given the Orc at HP 96 has the stored result from INC-11 and keeps PoisonPlateArmour equipped, when `Tick(Orc, 3)` runs, then Incoming resolves the original −2 plus a fresh +1 on each of the three steps, for total damage −3 and HP 93. The returned result contains three original and three prevention items, all Duration 0; the expired stored result is removed. Prevention corrections never accumulate in storage or reduce subsequent ticks' raw amounts (D16, A14).
    [Fact]
    public void LongTick_Should_RecalculateMitigationAtEachStep()
    {
        var g = new Game();
        g.Orc.Stats[HP].Current = 96;
        g.Store(g.Hit(Poison, -2, 3));
        g.Own("PoisonPlateArmour", Body, g.Orc);
        var result = g.Tick(elapsed: 3).Single();
        result.Items.Count(x => x.Amount == -2).ShouldBe(3);
        result.Items.Count(x => x.Amount == 1).ShouldBe(3);
        result.Items.ShouldAllBe(x => x.Duration == 0);
        Current(g.Orc).ShouldBe(93);
        g.Orc.Effects.ShouldBeEmpty();
    }
}
