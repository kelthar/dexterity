using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // COL-11: Given the Orc has PlateArmour Equipped, then its Passive/Incoming modifier is included when the Orc is hit.
    [Fact]
    public void Incoming_Should_CollectTargetEquippedPassives()
    {
        var g = new Game();
        var armour = g.Own("PlateArmour", Body, g.Orc);
        var result = g.In(g.Hit())[0];
        result.Sum(Physical).ShouldBe(-7);
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Amount == 3 && x.SourceId == g.Orc.Id && x.InstrumentId == armour.Id);
    }

    // INC-01: Given the Orc wears PlateArmour, when it receives `Physical −10`, then the result has `−10` and `+3 (SourceId Orc, InstrumentId PlateArmour)`, and `Sum(Physical) = −7`.
    [Fact]
    public void PlateArmour_Should_AddPhysicalPreventionWithProvenance()
    {
        var g = new Game();
        var armour = g.Own("PlateArmour", Body, g.Orc);
        var result = g.In(g.Hit())[0];
        result.Sum(Physical).ShouldBe(-7);
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Amount == 3 && x.SourceId == g.Orc.Id && x.InstrumentId == armour.Id);
    }

    // COL-12: Given PlateArmour is in the Orc's Inventory, then it is not included.
    [Fact]
    public void Incoming_Should_NotCollectInventoryArmour()
    {
        var name = "PlateArmour";
        var slot = 0;
        var hero = false;
        var g = new Game();
        g.Own(name, slot, hero ? g.Hero : g.Orc);
        var hit = g.In(g.Hit())[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-13: Given the Orc has the Longsword equipped, when the Orc is hit, then the Longsword's Use modifiers are not included (no damage is added to the hit).
    [Fact]
    public void Incoming_Should_NotCollectTargetWeaponUseModifiers()
    {
        var name = "Longsword";
        var slot = MainHand;
        var hero = false;
        var g = new Game();
        g.Own(name, slot, hero ? g.Hero : g.Orc);
        var hit = g.In(g.Hit())[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-14: Given the Orc has RingOfFire equipped, when the Orc is hit, then the ring (Passive/Outgoing) is not included.
    [Fact]
    public void Incoming_Should_NotCollectOutgoingRingModifiers()
    {
        var name = "RingOfFire";
        var slot = RingLeft;
        var hero = false;
        var g = new Game();
        g.Own(name, slot, hero ? g.Hero : g.Orc);
        var hit = g.In(g.Hit())[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-15: Given Hero wears PlateArmour, when Hero hits the Orc, then Hero's PlateArmour is not included in the Orc's Incoming.
    [Fact]
    public void Incoming_Should_NotCollectAttackerArmour()
    {
        var name = "PlateArmour";
        var slot = Body;
        var hero = true;
        var g = new Game();
        g.Own(name, slot, hero ? g.Hero : g.Orc);
        var hit = g.In(g.Hit())[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-31: Given PlateArmour with `RelationKind = Enemies` on the Orc, when Hero hits the Orc, then the damage is reduced. When Goblin1 (the Orc's friend) hits the Orc, then it is not reduced (D8).
    [Fact]
    public void IncomingRelationFilters_Should_CompareAttackerToDefender()
    {
        var g = new Game();
        var armour = g.Own("PlateArmour", Body, g.Orc);
        armour.ModifierData[0].RelationKind = RelationKind.Enemies;
        g.In(g.Hit())[0].Sum(Physical).ShouldBe(-7);
        var friendly = g.Hit();
        friendly.SourceId = g.Goblin1.Id;
        g.In(friendly)[0].Sum(Physical).ShouldBe(-10);
    }

    // INC-02: Given PlateArmour, when the Orc receives `Physical −2`, then the sum is `0`, never positive.
    [Fact]
    public void PlateArmour_Should_NotPreventBeyondZero()
    {
        var g = new Game();
        g.Own("PlateArmour", Body, g.Orc);
        g.In(g.Hit(amount: -2))[0].Sum(Physical).ShouldBe(0);
    }

    // INC-03: Given PlateArmour, when the Orc receives `Fire −4`, then Fire is unchanged.
    [Fact]
    public void PlateArmour_Should_NotPreventFireDamage()
    {
        var g = new Game();
        g.Own("PlateArmour", Body, g.Orc);
        g.In(g.Hit(Fire, -4))[0].Sum(Fire).ShouldBe(-4);
    }

    // INC-04: Given PreventFlat 5 on Physical and PreventFlat 5 on Fire, when the Orc receives `Physical −10` and `Fire −4`, then the result is `Physical −5` and `Fire 0` (D12).
    [Fact]
    public void PreventFlat_Should_UseASeparateBudgetPerEffectType()
    {
        var g = new Game();
        var ward = Ward(g, "preventflat", Physical, 5, "amount");
        ward.ModifierData.Add(Mod("preventflat", Fire, 5, "amount", Scope.Passive, Phase.Incoming));
        var hit = g.Hit();
        hit.Items.Add(g.Hit(Fire, -4).Items[0]);
        var result = g.In(hit)[0];
        result.Sum(Physical).ShouldBe(-5);
        result.Sum(Fire).ShouldBe(0);
    }

    // INC-05: Given WardRing, when the Orc receives `Fire −4`, then the result is `Fire −2`.
    [Fact]
    public void WardRing_Should_HalveFireDamage()
    {
        var g = new Game();
        g.Own("WardRing", RingLeft, g.Orc);
        g.In(g.Hit(Fire, -4))[0].Sum(Fire).ShouldBe(-2);
    }

    // INC-06: Given PreventPercent 100 on Physical, when the Orc receives `Physical −10`, then the result is `0`.
    [Fact]
    public void FullPercentPrevention_Should_CancelDamage()
    {
        var g = new Game();
        Ward(g, "preventpercent", Physical, 100, "percent");
        g.In(g.Hit())[0].Sum(Physical).ShouldBe(0);
    }

    // INC-07: Given PreventPercent 30 and 50 on Physical, when the Orc receives `Physical −10`, then the result is `−3.5` (sequential, D11).
    [Fact]
    public void PercentPreventions_Should_ApplySequentially()
    {
        var g = new Game();
        var ward = Ward(g, "preventpercent", Physical, 30, "percent");
        ward.ModifierData.Add(Mod("preventpercent", Physical, 50, "percent", Scope.Passive, Phase.Incoming));
        g.In(g.Hit())[0].Sum(Physical).ShouldBe(-3.5);
    }

    // INC-08: Given PreventFlat 3 (Order 600) and PreventPercent 50 (Order 700), both on Physical, when the Orc receives `Physical −10`, then the result is `−3.5`. If the Orders are swapped, the result is `−2`.
    [Fact]
    public void Prevention_Should_RespectModifierOrder()
    {
        var g = new Game();
        var ward = Ward(g, "preventflat", Physical, 3, "amount");
        ward.ModifierData.Add(Mod("preventpercent", Physical, 50, "percent", Scope.Passive, Phase.Incoming));
        g.In(g.Hit())[0].Sum(Physical).ShouldBe(-3.5);
        g.World.Modifiers.RemoveAll(x => x.Id is "preventflat" or "preventpercent");
        g.World.Modifiers.Add(new OrderedModifier(new PreventPercent(), 600));
        g.World.Modifiers.Add(new OrderedModifier(new PreventFlat(), 700));
        g.In(g.Hit())[0].Sum(Physical).ShouldBe(-2);
    }

    // INC-09: Given ArrowWard (IncludedTags `[ranged]`), then a Shortbow hit (`−8`) becomes `−4` and a Longsword hit stays `−10` (D1: resolve `result.InstrumentId`).
    [Fact]
    public void ArrowWard_Should_FilterByActionInstrumentTags()
    {
        var g = new Game();
        g.Own("ArrowWard", Neck, g.Orc);
        foreach (var(name, expected)in new[]
        {
            ("Shortbow", -4d),
            ("Longsword", -10d),
            ("Fists", -2d)
        }

        )
        {
            var instrument = g.Own(name, MainHand);
            var result = g.Out(instrument)[0];
            g.In(result)[0].Sum(Physical).ShouldBe(expected);
            g.Repo.Relations.Remove(g.Relation(instrument));
        }
    }

    // INC-10: Given ArrowWard and a Fists hit, then nothing is prevented.
    [Fact]
    public void ArrowWard_Should_NotPreventFistsDamage()
    {
        var g = new Game();
        g.Own("ArrowWard", Neck, g.Orc);
        foreach (var(name, expected)in new[]
        {
            ("Shortbow", -4d),
            ("Longsword", -10d),
            ("Fists", -2d)
        }

        )
        {
            var instrument = g.Own(name, MainHand);
            var result = g.Out(instrument)[0];
            g.In(result)[0].Sum(Physical).ShouldBe(expected);
            g.Repo.Relations.Remove(g.Relation(instrument));
        }
    }

    // INC-11: Given Hero's PoisonDagger result contains `Poison −2 × 3` and the Orc wears PoisonPlateArmour (PreventFlat Poison 1), when initial Incoming and Apply run, then a stored EffectResult retains that item at `Poison −2 × 3`, SourceId Hero and InstrumentId PoisonDagger. No Poison prevention or Poison stat change occurs until Tick; the first tick resolves to `Poison −1` (D16, D36).
    [Fact]
    public void PoisonPlateArmour_Should_DeferPoisonPreventionUntilTick()
    {
        var g = new Game();
        var dagger = g.Own("PoisonDagger", MainHand);
        var armour = g.Own("PoisonPlateArmour", Body, g.Orc);
        var result = g.In(g.Out(dagger)[0])[0];
        result.Items.ShouldNotContain(x => x.InstrumentId == armour.Id);
        Ok(g.Effects.Apply(g.Orc.Id, result));
        var stored = g.Orc.Effects.Single();
        stored.SourceId.ShouldBe(g.Hero.Id);
        stored.InstrumentId.ShouldBe(dagger.Id);
        stored.Items.Single().Amount.ShouldBe(-2);
        stored.Items.Single().Duration.ShouldBe(3);
        Current(g.Orc).ShouldBe(96);
        var tick = g.Tick().Single();
        tick.Sum(Poison).ShouldBe(-1);
        tick.Items.ShouldContain(x => x.Amount == 1 && x.InstrumentId == armour.Id);
        tick.Items.ShouldAllBe(x => x.Duration == 0);
        stored.Items.Single().Amount.ShouldBe(-2);
        Current(g.Orc).ShouldBe(95);
    }

    // INC-29: Given INC-11, then initial Incoming passes through the timed `Poison −2 × 3` unchanged, without adding a timed armour correction. Apply stores a copy of the EffectResult containing only timed items. On Tick, the resolved output contains `Poison −2` from Hero/PoisonDagger and `Poison +1` from Orc/PoisonPlateArmour, both at Duration 0; the stored amount remains −2.
    [Fact]
    public void InitialIncoming_Should_NotStoreTimedArmourCorrections()
    {
        var g = new Game();
        var dagger = g.Own("PoisonDagger", MainHand);
        var armour = g.Own("PoisonPlateArmour", Body, g.Orc);
        var result = g.In(g.Out(dagger)[0])[0];
        result.Items.ShouldNotContain(x => x.InstrumentId == armour.Id);
        Ok(g.Effects.Apply(g.Orc.Id, result));
        var stored = g.Orc.Effects.Single();
        stored.SourceId.ShouldBe(g.Hero.Id);
        stored.InstrumentId.ShouldBe(dagger.Id);
        stored.Items.Single().Amount.ShouldBe(-2);
        stored.Items.Single().Duration.ShouldBe(3);
        Current(g.Orc).ShouldBe(96);
        var tick = g.Tick().Single();
        tick.Sum(Poison).ShouldBe(-1);
        tick.Items.ShouldContain(x => x.Amount == 1 && x.InstrumentId == armour.Id);
        tick.Items.ShouldAllBe(x => x.Duration == 0);
        stored.Items.Single().Amount.ShouldBe(-2);
        Current(g.Orc).ShouldBe(95);
    }

    // INC-12: Given PreventFlat 5 on Heal, when the target receives `Heal +10`, then the result is `+5` (same rule as damage, D10).
    [Fact]
    public void PreventFlat_Should_ReduceHealing()
    {
        var g = new Game();
        Ward(g, "preventflat", Heal, 5, "amount");
        g.In(g.Hit(Heal, 10))[0].Sum(Heal).ShouldBe(5);
    }

    // INC-13: Incoming returns the target's result first, followed by any results for the source (steal).
    [Fact]
    public void Incoming_Should_ReturnTargetBeforeStealResults()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        var results = g.In(g.Out(dagger)[0]);
        results.Select(x => x.TargetId).ShouldBe(new int? [] { g.Orc.Id, g.Hero.Id });
    }

    // INC-14: After Incoming, no stat has changed.
    [Fact]
    public void Incoming_Should_NotChangeStats()
    {
        var g = new Game();
        g.Own("PlateArmour", Body, g.Orc);
        var before = g.Snapshot();
        g.In(g.Hit());
        g.Snapshot().ShouldBe(before);
    }

    // INC-15: Given an empty result, then Incoming returns an empty result and there is no error.
    [Fact]
    public void Incoming_Should_AcceptEmptyResults()
    {
        var g = new Game();
        var hit = g.Hit();
        hit.Items.Clear();
        g.In(hit).Single().Items.ShouldBeEmpty();
    }

    // INC-16: Given a target without Incoming modifiers, then the items are unchanged.
    [Fact]
    public void Incoming_Should_PreserveItemsWithoutMatchingModifiers()
    {
        var g = new Game();
        var hit = g.Hit();
        g.In(hit).Single().Items.ShouldBe(hit.Items);
    }

    // INC-17: Given `result.TargetId` is Goblin1 or null (separate cases), when Incoming is called with the Orc's Id, then the call fails.
    [Fact]
    public void Incoming_Should_RejectMismatchedAndMissingTargetIds()
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
            Fail(g.Effects.Incoming(g.Orc.Id, hit));
            g.Snapshot().ShouldBe(before);
        }
    }

    // INC-18: Given an unknown targetId or a null result (separate cases), then the call fails.
    [Fact]
    public void Incoming_Should_RejectUnknownTargetsAndNullResults()
    {
        var g = new Game();
        var before = g.Snapshot();
        Fail(g.Effects.Incoming(999, g.Hit()));
        Fail(g.Effects.Incoming(g.Orc.Id, null !));
        g.Snapshot().ShouldBe(before);
    }

    // INC-19: Given Hero wears BlightRing, when Hero receives `Heal +10`, then the result has `+10` and `−5 (SourceId Hero, InstrumentId BlightRing)`, and `Sum(Heal) = +5`.
    [Fact]
    public void BlightRing_Should_ReduceHealingWithProvenance()
    {
        var g = new Game();
        var ring = g.Own("BlightRing", RingLeft);
        var hit = g.In(g.Hit(Heal, 10, target: g.Hero))[0];
        hit.Sum(Heal).ShouldBe(5);
        hit.Items.ShouldContain(x => x.Amount == -5 && x.SourceId == g.Hero.Id && x.InstrumentId == ring.Id);
        g.In(g.Hit(target: g.Hero))[0].Sum(Physical).ShouldBe(-10);
    }

    // INC-20: Given Hero wears BlightRing, when Hero receives `Physical −10`, then Physical is unchanged.
    [Fact]
    public void BlightRing_Should_NotPreventPhysicalDamage()
    {
        var g = new Game();
        var ring = g.Own("BlightRing", RingLeft);
        var hit = g.In(g.Hit(Heal, 10, target: g.Hero))[0];
        hit.Sum(Heal).ShouldBe(5);
        hit.Items.ShouldContain(x => x.Amount == -5 && x.SourceId == g.Hero.Id && x.InstrumentId == ring.Id);
        g.In(g.Hit(target: g.Hero))[0].Sum(Physical).ShouldBe(-10);
    }

    // INC-21: Given the Orc wears armour with PreventFlat 1 on Stun, when it receives `Stun +2`, then the result is `+1` (armour prevents stuns, D10).
    [Fact]
    public void PreventFlat_Should_ReduceStunAmounts()
    {
        var type = "preventflat";
        var effect = StunEffect;
        var value = 1d;
        var key = "amount";
        var amount = 2d;
        var expected = 1d;
        var g = new Game();
        Ward(g, type, effect, value, key);
        g.In(g.Hit(effect, amount))[0].Sum(effect).ShouldBe(expected);
    }

    // INC-22: Given the Orc has a Passive/Incoming Multiplier ×0.5 on Stun, when it receives `Stun +2`, then the result is `+1`.
    [Fact]
    public void IncomingMultiplier_Should_ReduceStunAmounts()
    {
        var type = "multiplier";
        var effect = StunEffect;
        var value = 0.5d;
        var key = "multiplier";
        var amount = 2d;
        var expected = 1d;
        var g = new Game();
        Ward(g, type, effect, value, key);
        g.In(g.Hit(effect, amount))[0].Sum(effect).ShouldBe(expected);
    }

    // INC-25: Given PreventPercent 50 on Heal, when the target receives `Heal +10`, then the result is `+5`.
    [Fact]
    public void PreventPercent_Should_ReduceHealing()
    {
        var type = "preventpercent";
        var effect = Heal;
        var value = 50d;
        var key = "percent";
        var amount = 10d;
        var expected = 5d;
        var g = new Game();
        Ward(g, type, effect, value, key);
        g.In(g.Hit(effect, amount))[0].Sum(effect).ShouldBe(expected);
    }

    // INC-26: Given PreventPercent 50 on Stun, when the Orc receives `Stun +2`, then the result is `+1`.
    [Fact]
    public void PreventPercent_Should_ReduceStunAmounts()
    {
        var type = "preventpercent";
        var effect = StunEffect;
        var value = 50d;
        var key = "percent";
        var amount = 2d;
        var expected = 1d;
        var g = new Game();
        Ward(g, type, effect, value, key);
        g.In(g.Hit(effect, amount))[0].Sum(effect).ShouldBe(expected);
    }

    // INC-23: Given a result whose SourceId or InstrumentId cannot be resolved in ComponentRepository (separate cases), when Incoming is called, then it fails without changing state. A retained Deleted origin is resolvable and does not fail merely because of its state (D39).
    [Fact]
    public void Incoming_Should_RejectUnresolvableOriginsWithoutMutation()
    {
        var g = new Game();
        foreach (var source in new[]
        {
            true,
            false
        }

        )
        {
            var hit = g.Hit();
            if (source)
                hit.SourceId = 999;
            else
                hit.InstrumentId = 999;
            var before = g.Snapshot();
            Fail(g.Effects.Incoming(g.Orc.Id, hit));
            g.Snapshot().ShouldBe(before);
        }

        var origin = g.Make("FireBomb");
        origin.State = ComponentState.Deleted;
        g.In(g.Hit(Fire, -1, instrument: origin))[0].Sum(Fire).ShouldBe(-1);
    }

    // INC-24: Given ArrowWard on Goblin1 and a splash result from a melee attack, then nothing is prevented. The match resolves `result.InstrumentId` (Longsword), not the splash item's InstrumentId (Cleave1).
    [Fact]
    public void ArrowWard_Should_NotFilterBySplashHolderTags()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        g.Own("ArrowWard", Neck, g.Goblin1);
        g.In(g.Out(sword, g.Orc, g.Goblin1)[1])[0].Sum(Physical).ShouldBe(-5);
    }

    // INC-27: Given PreventFlat 3 on Poison, when the Orc receives and stores `Poison −2 × 3`, then the timed item is not discarded. The first tick produces `−2` and `+2` (net 0), leaves HP unchanged, and reduces the stored duration to 2 while keeping the amount −2. Prevention never changes the duration or removes an unexpired application (D16, D35, A15).
    [Fact]
    public void FullPoisonPrevention_Should_PreserveUnexpiredTimedItems()
    {
        var g = new Game();
        Ward(g, "preventflat", Poison, 3, "amount");
        var input = g.Hit(Poison, -2, 3);
        Ok(g.Effects.Apply(g.Orc.Id, g.In(input)[0]));
        var tick = g.Tick()[0];
        tick.Items.Select(x => x.Amount).ShouldBe(new[] { -2d, 2d });
        tick.Sum(Poison).ShouldBe(0);
        Current(g.Orc).ShouldBe(100);
        g.Orc.Effects.Single().Items.Single().Duration.ShouldBe(2);
        g.Orc.Effects[0].Items[0].Amount.ShouldBe(-2);
    }

    // INC-28: Given a PreventFlat 5 without an `EffectTypeId`, when the Orc receives `Physical −10`, then nothing is prevented (D9).
    [Fact]
    public void PreventionWithoutEffectType_Should_NotPreventDamage()
    {
        var g = new Game();
        Ward(g, "preventflat", null, 5, "amount");
        g.In(g.Hit())[0].Sum(Physical).ShouldBe(-10);
    }

    // INC-30: Given the Orc wears WardRing (Fire 50%), when it receives the FireBomb (`Fire −3` now, `Fire −1 × 2`), then initial Incoming resolves the immediate part to −1.5 and Apply stores `Fire −1 × 2`. With the ring still equipped, the first tick resolves to −0.5. If the ring is then unequipped, the next tick resolves to −1 (D16).
    [Fact]
    public void WardRing_Should_ApplyCurrentEquipmentOnEachBurnTick()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        var ring = g.Own("WardRing", RingLeft, g.Orc);
        var result = g.In(g.Out(bomb)[0])[0];
        result.Sum(Fire).ShouldBe(-1.5);
        Ok(g.Effects.Apply(g.Orc.Id, result));
        g.Orc.Effects[0].Items.Single().Amount.ShouldBe(-1);
        g.Tick()[0].Sum(Fire).ShouldBe(-.5);
        Ok(g.Relations.Unequip(g.Orc.Id, ring.Id));
        g.Tick()[0].Sum(Fire).ShouldBe(-1);
    }

    // INC-31: Given PreventFlat 1 on Fire, when the Orc receives the FireBomb, then initial Incoming reduces the immediate `−3` to −2 and Apply still stores `Fire −1 × 2`. On each tick, Incoming applies prevention anew and the timed Fire resolves to 0. The initial hit and later ticks do not share a prevention budget, and a zero tick does not erase the unexpired stored result (D12, A15).
    [Fact]
    public void FirePrevention_Should_UseSeparateInitialAndTickBudgets()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        Ward(g, "preventflat", Fire, 1, "amount");
        var result = g.In(g.Out(bomb)[0])[0];
        result.Sum(Fire).ShouldBe(-2);
        Ok(g.Effects.Apply(g.Orc.Id, result));
        g.Tick()[0].Sum(Fire).ShouldBe(0);
        g.Orc.Effects.Single().Items.Single().Amount.ShouldBe(-1);
        g.Tick()[0].Sum(Fire).ShouldBe(0);
        g.Orc.Effects.ShouldBeEmpty();
    }

    // STL-01: Given an Orc without armour, then Incoming returns the Orc's `Physical −10` and a Hero result with `Physical +5 (SourceId Hero, InstrumentId VampDagger)` (A10).
    [Fact]
    public void VampDagger_Should_ReturnHalfDamageAsSteal()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        var results = g.In(g.Out(dagger)[0]);
        results[0].Sum(Physical).ShouldBe(-10);
        var steal = results[1];
        steal.TargetId.ShouldBe(g.Hero.Id);
        steal.SourceId.ShouldBe(g.Hero.Id);
        steal.InstrumentId.ShouldBe(dagger.Id);
        steal.Items.Single().ShouldBe(new EffectResultItem { EffectTypeId = Physical, Amount = 5, SourceId = g.Hero.Id, InstrumentId = dagger.Id });
    }

    // RES-09: Given a VampDagger hit, then the steal result has `TargetId = Hero`, `SourceId = Hero` and `InstrumentId = VampDagger` (A10).
    [Fact]
    public void StealResults_Should_PreserveActionIdentity()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        var results = g.In(g.Out(dagger)[0]);
        results[0].Sum(Physical).ShouldBe(-10);
        var steal = results[1];
        steal.TargetId.ShouldBe(g.Hero.Id);
        steal.SourceId.ShouldBe(g.Hero.Id);
        steal.InstrumentId.ShouldBe(dagger.Id);
        steal.Items.Single().ShouldBe(new EffectResultItem { EffectTypeId = Physical, Amount = 5, SourceId = g.Hero.Id, InstrumentId = dagger.Id });
    }

    // STL-02: Given the Orc wears PlateArmour, then the Orc gets `−7` and Hero gets `+3.5` (after prevention, D14).
    [Fact]
    public void Steal_Should_UsePostArmourDamage()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        g.Own("PlateArmour", Body, g.Orc);
        var results = g.In(g.Out(dagger)[0]);
        results[0].Sum(Physical).ShouldBe(-7);
        results[1].Sum(Physical).ShouldBe(3.5);
    }

    // STL-03: Given the Orc has HP 3, then the Orc's result is `−10` and Hero gets `+1.5` (capped by Current, D14).
    [Fact]
    public void Steal_Should_BeCappedByTargetCurrentHealth()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        g.Orc.Stats[HP].Current = 3;
        var results = g.In(g.Out(dagger)[0]);
        results[0].Sum(Physical).ShouldBe(-10);
        results[1].Sum(Physical).ShouldBe(1.5);
    }

    // STL-04: Given the damage is fully prevented (the Orc has PreventPercent 100 on Physical), then there is no steal result.
    [Fact]
    public void Steal_Should_NotReturnFullyPreventedDamage()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        Ward(g, "preventpercent", Physical, 100, "percent");
        var results = g.In(g.Out(dagger)[0]);
        results.Count.ShouldBe(1);
        results[0].Sum(Physical).ShouldBe(0);
    }

    // STL-05: Given a Steal modifier on Heal (or Stun) and a positive item such as `Heal +10` or `Stun +2`, then nothing is stolen (Steal only takes negative amounts, D14).
    [Fact]
    public void Steal_Should_NotApplyToPositiveAmounts()
    {
        var g = new Game();
        foreach (var effect in new[]
        {
            Heal,
            StunEffect
        }

        )
        {
            var potion = g.Own("HealingPotion");
            potion.ModifierData = new()
            {
                Mod("increase", effect, 10),
                Mod("steal", effect, 50, "percent")
            };
            g.In(g.Out(potion)[0]).Count.ShouldBe(1);
        }
    }

    // STL-06: Given RingOfFire too (the VampDagger steals both Physical and Fire), then Hero gets `+7` (50% of 10 + 50% of 4), as one item per Steal modifier.
    [Fact]
    public void Steal_Should_KeepSeparateEffectTypeContributions()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        g.Own("RingOfFire", RingLeft);
        var steal = g.In(g.Out(dagger)[0])[1];
        steal.Items.Count.ShouldBe(2);
        steal.SumStat(HP, g.World).ShouldBe(7);
    }

    // STL-07: Given Cleave1 (no RingOfFire) and Goblin1 at 2, then Hero also gets `+2.5` from Goblin1.
    [Fact]
    public void Steal_Should_IncludeSplashTargets()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        g.Own("Cleave1");
        g.In(g.Out(dagger, g.Orc, g.Goblin1)[1])[1].Sum(Physical).ShouldBe(2.5);
    }

    // STL-08: Given Hero at HP 98, when the steal results are applied, then Hero ends at HP 100 (clamped).
    [Fact]
    public void StealApplication_Should_ClampAtMaximumHealth()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        g.Hero.Stats[HP].Current = 98;
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        Current(g.Hero).ShouldBe(100);
    }

    // STL-09: Given Hero wears BlightRing, then the steal heal is not halved (A10: the source's Incoming modifiers don't run on steal results).
    [Fact]
    public void BlightRing_Should_NotMitigateStealResults()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        g.Hero.Stats[HP].Current = 80;
        g.Own("BlightRing", RingLeft);
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        Current(g.Hero).ShouldBe(85);
    }

    // STL-10: Given Hero at HP 80/100 uses LeechDagger on the Orc at HP 100 with no armour, then the initial action stores `Poison −2 × 3` without changing either actor's HP or producing Steal. On `Tick(Orc, 1)`, the Orc loses 2 and Hero receives `Poison +1` from Steal, ending at HP 98 and 81 respectively (A10, D14).
    [Fact]
    public void LeechDagger_Should_DeferStealUntilPoisonTicks()
    {
        var g = new Game();
        var dagger = g.Own("LeechDagger", MainHand);
        g.Hero.Stats[HP].Current = 80;
        var initial = Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        initial.ShouldNotContain(x => x.TargetId == g.Hero.Id);
        Current(g.Orc).ShouldBe(100);
        Current(g.Hero).ShouldBe(80);
        g.Orc.Effects.Single().Items.Single().Duration.ShouldBe(3);
        var tick = g.Tick();
        Current(g.Orc).ShouldBe(98);
        Current(g.Hero).ShouldBe(81);
        tick.Single(x => x.TargetId == g.Hero.Id).Sum(Poison).ShouldBe(1);
    }

    // STL-11: Given LeechDagger's timed Poison is stored, Hero is at HP 80 and has an Incoming PreventPercent Poison 100, when the unarmoured Orc ticks once, then Hero still receives the full `Poison +1` Steal and reaches HP 81. Hero's Incoming does not run again on that positive Steal result (A10).
    [Fact]
    public void Steal_Should_NotRunThroughSourceIncomingPrevention()
    {
        var g = new Game();
        var dagger = g.Own("LeechDagger", MainHand);
        g.Hero.Stats[HP].Current = 80;
        Ward(g, "preventpercent", Poison, 100, "percent", g.Hero);
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        g.Tick();
        Current(g.Hero).ShouldBe(81);
    }

    // STL-12: Given LeechDagger's `Poison −2 × 3` is stored and the Orc has PreventFlat Poison 3, when the Orc ticks, then no Steal result is produced because resolved damage is 0. The stored raw Poison item remains with duration 2 (D14, A15).
    [Fact]
    public void Steal_Should_NotApplyWhenPoisonTickIsFullyPrevented()
    {
        var g = new Game();
        var dagger = g.Own("LeechDagger", MainHand);
        Ward(g, "preventflat", Poison, 3, "amount");
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        g.Tick().ShouldNotContain(x => x.TargetId == g.Hero.Id);
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(2);
        g.Orc.Effects[0].Items[0].Amount.ShouldBe(-2);
    }

    // STL-13: Given LeechDagger's timed Poison is stored, the Orc is at HP 1 and Hero at HP 80, when the unarmoured Orc ticks, then the Orc reaches HP 0 and Steal gives Hero only `Poison +0.5` (50% of Current 1), reaching HP 80.5. On the next tick while the Orc remains at HP 0, no Steal is produced, although the timed effect still counts down (D14).
    [Fact]
    public void TimedSteal_Should_BeCappedByRemainingTargetHealth()
    {
        var g = new Game();
        var dagger = g.Own("LeechDagger", MainHand);
        g.Hero.Stats[HP].Current = 80;
        g.Orc.Stats[HP].Current = 1;
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        g.Tick().Single(x => x.TargetId == g.Hero.Id).Sum(Poison).ShouldBe(.5);
        Current(g.Orc).ShouldBe(0);
        Current(g.Hero).ShouldBe(80.5);
        g.Tick().ShouldNotContain(x => x.TargetId == g.Hero.Id);
        g.Orc.Effects[0].Items[0].Duration.ShouldBe(1);
    }

    private static Component Ward(Game g, string type, int? effect, double value, string key, Component? owner = null)
    {
        var ward = g.Own("WardRing", RingLeft, owner ?? g.Orc);
        ward.ModifierData = new()
        {
            Mod(type, effect, value, key, Scope.Passive, Phase.Incoming)
        };
        return ward;
    }
}

internal sealed class OrderedModifier(IModifierType inner, int order) : IModifierType
{
    public string Id => inner.Id;
    public string Name => inner.Name;
    public int Order => order;

    public EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId) => inner.Calculate(result, modifier, context, holderId);
}
