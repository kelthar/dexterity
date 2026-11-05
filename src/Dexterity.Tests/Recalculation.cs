using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // REC-01: Given StrengthRing is equipped, then Hero's STR has `Max 10, Bonus 2, ModifiedMax 12, Current 12` (D19, A13).
    [Fact]
    public void StrengthRing_Should_IncreaseStrengthBonusAndCurrent()
    {
        var g = new Game();
        var ring = g.Own("StrengthRing");
        Ok(g.Relations.Equip(g.Hero.Id, ring.Id, RingLeft));
        var stat = g.Hero.Stats[Str];
        (stat.Max, stat.Bonus, stat.ModifiedMax, stat.Current).ShouldBe((10d, 2d, 12d, 12d));
        Ok(g.Relations.Unequip(g.Hero.Id, ring.Id));
        stat = g.Hero.Stats[Str];
        (stat.Bonus, stat.ModifiedMax, stat.Current).ShouldBe((0d, 10d, 10d));
        var skill = g.Make("Toughness");
        Ok(g.Relations.Connect(g.Hero.Id, skill.Id));
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(110);
        Ok(g.Relations.Remove(g.Hero.Id, skill.Id));
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(100);
    }

    // REC-02: Given StrengthRing is unequipped, then STR is `Bonus 0, ModifiedMax 10, Current 10`.
    [Fact]
    public void StrengthRingUnequip_Should_RemoveItsBonus()
    {
        var g = new Game();
        var ring = g.Own("StrengthRing");
        Ok(g.Relations.Equip(g.Hero.Id, ring.Id, RingLeft));
        var stat = g.Hero.Stats[Str];
        (stat.Max, stat.Bonus, stat.ModifiedMax, stat.Current).ShouldBe((10d, 2d, 12d, 12d));
        Ok(g.Relations.Unequip(g.Hero.Id, ring.Id));
        stat = g.Hero.Stats[Str];
        (stat.Bonus, stat.ModifiedMax, stat.Current).ShouldBe((0d, 10d, 10d));
        var skill = g.Make("Toughness");
        Ok(g.Relations.Connect(g.Hero.Id, skill.Id));
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(110);
        Ok(g.Relations.Remove(g.Hero.Id, skill.Id));
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(100);
    }

    // REC-14: Equip, unequip, connect and disconnect each update Bonus and ModifiedMax with no manual Recalculate call (D22).
    [Fact]
    public void RelationChanges_Should_RecalculateAutomatically()
    {
        var g = new Game();
        var ring = g.Own("StrengthRing");
        Ok(g.Relations.Equip(g.Hero.Id, ring.Id, RingLeft));
        var stat = g.Hero.Stats[Str];
        (stat.Max, stat.Bonus, stat.ModifiedMax, stat.Current).ShouldBe((10d, 2d, 12d, 12d));
        Ok(g.Relations.Unequip(g.Hero.Id, ring.Id));
        stat = g.Hero.Stats[Str];
        (stat.Bonus, stat.ModifiedMax, stat.Current).ShouldBe((0d, 10d, 10d));
        var skill = g.Make("Toughness");
        Ok(g.Relations.Connect(g.Hero.Id, skill.Id));
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(110);
        Ok(g.Relations.Remove(g.Hero.Id, skill.Id));
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(100);
    }

    // REC-03: Recalculate called twice in a row gives `Bonus 2` (idempotent).
    [Fact]
    public void Recalculate_Should_NotAccumulateExistingBonuses()
    {
        var g = new Game();
        g.Own("StrengthRing", RingLeft);
        Ok(g.Effects.Recalculate(g.Hero.Id));
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[Str].Bonus.ShouldBe(2);
    }

    // REC-04: Given two StrengthRings, then `Bonus 4`.
    [Fact]
    public void TwoStrengthRings_Should_StackBonuses()
    {
        var g = new Game();
        g.Own("StrengthRing", RingLeft);
        g.Own("StrengthRing", RingRight);
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[Str].Bonus.ShouldBe(4);
    }

    // REC-05: Given StrengthRing and CursedRing, then `Bonus 0`.
    [Fact]
    public void StrengthAndCursedRings_Should_CancelBonuses()
    {
        var g = new Game();
        g.Own("StrengthRing", RingLeft);
        g.Own("CursedRing", RingRight);
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[Str].Bonus.ShouldBe(0);
    }

    // REC-06: Given a CursedRing variant with value 12, then STR ModifiedMax is 0 (clamped, not −2, D32) and Current is 0.
    [Fact]
    public void NegativeBonuses_Should_NotLowerMaximumBelowZero()
    {
        var g = new Game();
        g.Own("CursedRing", RingLeft).ModifierData[0].Data["value"] = 12;
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[Str].ModifiedMax.ShouldBe(0);
        Current(g.Hero, Str).ShouldBe(0);
    }

    // REC-07: Given Hero at HP 50/100, when VigorAmulet is equipped, then HP is 60/110. When it is unequipped, HP is 50/100 (D19).
    [Fact]
    public void VigorAmulet_Should_MoveCurrentByMaximumDelta()
    {
        var g = new Game();
        g.Hero.Stats[HP].Current = 50;
        var amulet = g.Own("VigorAmulet");
        Ok(g.Relations.Equip(g.Hero.Id, amulet.Id, Neck));
        Current(g.Hero).ShouldBe(60);
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(110);
        Ok(g.Relations.Unequip(g.Hero.Id, amulet.Id));
        Current(g.Hero).ShouldBe(50);
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(100);
    }

    // REC-08: Given Hero at HP 110/110 with VigorAmulet, when it is unequipped, then HP is 100/100.
    [Fact]
    public void VigorAmuletUnequip_Should_RemoveMaximumDelta()
    {
        var current = 110d;
        var expected = 100d;
        var g = new Game();
        var amulet = g.Own("VigorAmulet", Neck);
        g.Hero.Stats[HP].ModifiedMax = 110;
        g.Hero.Stats[HP].Bonus = 10;
        g.Hero.Stats[HP].Current = current;
        Ok(g.Relations.Unequip(g.Hero.Id, amulet.Id));
        Current(g.Hero).ShouldBe(expected);
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(100);
    }

    // REC-09: Given Hero at HP 5/110 with VigorAmulet, when it is unequipped, then HP is 0/100. *(Confirmed: intended.)*
    [Fact]
    public void VigorAmuletUnequip_Should_ClampCurrentAtZero()
    {
        var current = 5d;
        var expected = 0d;
        var g = new Game();
        var amulet = g.Own("VigorAmulet", Neck);
        g.Hero.Stats[HP].ModifiedMax = 110;
        g.Hero.Stats[HP].Bonus = 10;
        g.Hero.Stats[HP].Current = current;
        Ok(g.Relations.Unequip(g.Hero.Id, amulet.Id));
        Current(g.Hero).ShouldBe(expected);
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(100);
    }

    // REC-10: Given Toughness is Connected, then HP ModifiedMax is 110 (skills count, A7).
    [Fact]
    public void ConnectedToughness_Should_IncreaseMaximumHealth()
    {
        var g = new Game();
        var skill = g.Make("Toughness");
        Ok(g.Relations.Connect(g.Hero.Id, skill.Id));
        g.Hero.Stats[HP].ModifiedMax.ShouldBe(110);
    }

    // REC-11: Given a calculator `HP.Max = 50 + 5 × STR.ModifiedMax` (Order 1, STR has no calculator), then Hero's HP Max is 100. With StrengthRing it is 110, and Current also moves +10 (D20, A30).
    [Fact]
    public void HealthCalculator_Should_ReadStrengthModifiedMaximum()
    {
        var g = new Game();
        g.World.Calculators.Add(HpCalculator(1));
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[HP].Max.ShouldBe(100);
        var ring = g.Own("StrengthRing");
        Ok(g.Relations.Equip(g.Hero.Id, ring.Id, RingLeft));
        g.Hero.Stats[HP].Max.ShouldBe(110);
        Current(g.Hero).ShouldBe(110);
    }

    // REC-12: Given a STR calculator (a fake that returns 12, Order 1) and the HP calculator (Order 2), registered HP first, when Recalculate runs once, then STR is 12 and HP Max is 110 (Order decides, not registration order).
    [Fact]
    public void Calculators_Should_SeePreviouslyRecalculatedStats()
    {
        var g = new Game();
        g.World.Calculators.Add(HpCalculator(2));
        g.World.Calculators.Add(new Calculator { StatTypeId = Str, Order = 1, Value = _ => 12 });
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[Str].ModifiedMax.ShouldBe(12);
        g.Hero.Stats[HP].Max.ShouldBe(110);
    }

    // REC-13: Given a stat without a calculator, then Max stays at its stored value.
    [Fact]
    public void StatsWithoutCalculators_Should_PreserveStoredMaximum()
    {
        var g = new Game();
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[HP].Max.ShouldBe(100);
        g.Hero.Stats[Str].Max.ShouldBe(10);
    }

    // REC-15: Recalculate on a component with no calculators and no matching modifiers (the Longsword) changes nothing and doesn't fail.
    [Fact]
    public void Recalculate_Should_AcceptComponentsWithoutModifiersOrCalculators()
    {
        var g = new Game();
        var sword = g.Make("Longsword");
        var before = g.Snapshot();
        Ok(g.Effects.Recalculate(sword.Id));
        g.Snapshot().ShouldBe(before);
    }

    // REC-16: Given StrengthRing is in Inventory, then `Bonus 0`.
    [Fact]
    public void InventoryStrengthRing_Should_NotAddBonuses()
    {
        var g = new Game();
        g.Own("StrengthRing");
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[Str].Bonus.ShouldBe(0);
    }

    // REC-17: Given RingOfFire (Passive/Outgoing) is equipped, then no stat's Bonus changes.
    [Fact]
    public void RingOfFire_Should_NotAddStatBonuses()
    {
        var g = new Game();
        g.Own("RingOfFire", RingLeft);
        var before = g.Snapshot();
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Snapshot().ShouldBe(before);
    }

    // REC-18: Given the Character calculator, then it never runs on Items (the Longsword has no HP).
    [Fact]
    public void CharacterCalculators_Should_NotRunOnItems()
    {
        var g = new Game();
        var calculator = HpCalculator(1);
        g.World.Calculators.Add(calculator);
        var sword = g.Make("Longsword");
        Ok(g.Effects.Recalculate(sword.Id));
        calculator.Calls.ShouldBeEmpty();
        sword.Stats.ShouldNotContainKey(HP);
    }

    // REC-19: Given a Gem with StatsRecalc `+2 StrBonus` connected to the equipped Longsword, then Hero's STR doesn't change (D23: it only applies to the Longsword, which has no STR). *(Confirmed: intended.)*
    [Fact]
    public void ConnectedGem_Should_NotPropagateStatsToGrandparents()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var gem = g.Make("Ruby");
        gem.ModifierData = new()
        {
            Mod("increase", StrBonus, 2, scope: Scope.Passive, phase: Phase.StatsRecalc)
        };
        Ok(g.Relations.Connect(sword.Id, gem.Id));
        g.Hero.Stats[Str].ModifiedMax.ShouldBe(10);
        sword.Stats.ShouldNotContainKey(Str);
    }

    // REC-20: Given a drained STR (Current 8/10), when Recalculate runs, then the drain is kept (Current 8) and is not reset to 10.
    [Fact]
    public void Recalculate_Should_PreserveDrainedCurrentValues()
    {
        var g = new Game();
        g.Hero.Stats[Str].Current = 8;
        Ok(g.Effects.Recalculate(g.Hero.Id));
        Current(g.Hero, Str).ShouldBe(8);
    }

    // REC-21: Given the HP calculator (Order 1) and the STR calculator (returns 12, Order 2), which is the wrong order, when Recalculate runs once, then HP Max is 100 (it read the old STR 10, A29) and STR is 12. A second Recalculate gives HP Max 110. No error is raised.
    [Fact]
    public void Calculators_Should_RespectConfiguredOrder()
    {
        var g = new Game();
        g.World.Calculators.Add(HpCalculator(1));
        g.World.Calculators.Add(new Calculator { StatTypeId = Str, Order = 2, Value = _ => 12 });
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[HP].Max.ShouldBe(100);
        g.Hero.Stats[Str].ModifiedMax.ShouldBe(12);
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[HP].Max.ShouldBe(110);
    }

    // REC-22: Given calculators for STR and MANA with the same Order 5, registered STR first, then STR's calculator runs before MANA's (A28, checked with a spy).
    [Fact]
    public void EqualOrderCalculators_Should_PreserveRegistrationOrder()
    {
        foreach (var orders in new[]
        {
            (5, 5),
            (-1, 0)
        }

        )
        {
            var g = new Game();
            var calls = new List<string>();
            g.World.Calculators.Add(new Calculator { StatTypeId = Str, Order = orders.Item1, Value = _ =>
            {
                calls.Add("STR");
                return 10;
            } });
            g.World.Calculators.Add(new Calculator { StatTypeId = Mana, Order = orders.Item2, Value = _ =>
            {
                calls.Add("MANA");
                return 50;
            } });
            if (orders.Item1 < 0)
                g.World.Calculators.Reverse();
            Ok(g.Effects.Recalculate(g.Hero.Id));
            calls.ShouldBe(new[] { "STR", "MANA" });
        }
    }

    // REC-23: Given calculators with Order −1 and Order 0, then the −1 calculator runs first.
    [Fact]
    public void NegativeOrderCalculators_Should_RunFirst()
    {
        foreach (var orders in new[]
        {
            (5, 5),
            (-1, 0)
        }

        )
        {
            var g = new Game();
            var calls = new List<string>();
            g.World.Calculators.Add(new Calculator { StatTypeId = Str, Order = orders.Item1, Value = _ =>
            {
                calls.Add("STR");
                return 10;
            } });
            g.World.Calculators.Add(new Calculator { StatTypeId = Mana, Order = orders.Item2, Value = _ =>
            {
                calls.Add("MANA");
                return 50;
            } });
            if (orders.Item1 < 0)
                g.World.Calculators.Reverse();
            Ok(g.Effects.Recalculate(g.Hero.Id));
            calls.ShouldBe(new[] { "STR", "MANA" });
        }
    }

    // REC-24: Given StrengthRing and VigorAmulet equipped, the HP calculator `50 + 5 × STR.ModifiedMax` (Order 1) and a MANA calculator `HP.ModifiedMax / 2` (Order 2), then STR is 12, HP is `Max 110, Bonus 10, ModifiedMax 120`, and MANA Max is 60 (bonuses chain through calculators).
    [Fact]
    public void DependentCalculators_Should_IncludePreviouslyAppliedBonuses()
    {
        var g = new Game();
        g.Own("StrengthRing", RingLeft);
        g.Own("VigorAmulet", Neck);
        g.World.Calculators.Add(HpCalculator(1));
        g.World.Calculators.Add(new Calculator { StatTypeId = Mana, Order = 2, Value = actor => actor.Stats[HP].ModifiedMax / 2 });
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Hero.Stats[Str].ModifiedMax.ShouldBe(12);
        var hp = g.Hero.Stats[HP];
        (hp.Max, hp.Bonus, hp.ModifiedMax).ShouldBe((110d, 10d, 120d));
        g.Hero.Stats[Mana].Max.ShouldBe(60);
    }

    // REC-25: Given the HP calculator returns 100 and VigorAmulet is equipped, then HP is `Max 100, Bonus 10, ModifiedMax 110`. A stat's own Bonus is added after its calculator (A30).
    [Fact]
    public void HealthCalculator_Should_AddVigorBonusOnce()
    {
        var g = new Game();
        g.Own("VigorAmulet", Neck);
        g.World.Calculators.Add(new Calculator());
        Ok(g.Effects.Recalculate(g.Hero.Id));
        var hp = g.Hero.Stats[HP];
        (hp.Max, hp.Bonus, hp.ModifiedMax).ShouldBe((100d, 10d, 110d));
    }

    // REC-26: Given correctly ordered calculators, two Recalculate calls in a row give the same values.
    [Fact]
    public void Recalculate_Should_BeIdempotentWithOrderedDependencies()
    {
        var g = new Game();
        g.Own("StrengthRing", RingLeft);
        g.World.Calculators.Add(HpCalculator(1));
        Ok(g.Effects.Recalculate(g.Hero.Id));
        var before = g.Snapshot();
        Ok(g.Effects.Recalculate(g.Hero.Id));
        g.Snapshot().ShouldBe(before);
    }

    // REC-27: Given an Item calculator with Order −10, when a Character is recalculated, then the Item calculator doesn't run (Order never overrides the ComponentType restriction).
    [Fact]
    public void ItemCalculators_Should_NotRunOnCharacters()
    {
        var g = new Game();
        var calculator = new Calculator
        {
            ComponentTypeId = Item,
            StatTypeId = Charges,
            Order = -10
        };
        g.World.Calculators.Add(calculator);
        Ok(g.Effects.Recalculate(g.Hero.Id));
        calculator.Calls.ShouldBeEmpty();
    }

    // SKL-01: Cleave1 has `Stats[LEVEL] = 1` and Cleave2 has `Stats[LEVEL] = 2`.
    [Fact]
    public void CleaveLevels_Should_HaveTheirOwnLevelStats()
    {
        var g = new Game();
        Current(g.Make("Cleave1"), Level).ShouldBe(1);
        Current(g.Make("Cleave2"), Level).ShouldBe(2);
    }

    // SKL-02: Given Hero has Cleave1, then the splash is 50%. After Cleave1 is swapped for Cleave2, the splash is 75%, not 125%.
    [Fact]
    public void CleaveOne_Should_SplashHalfDamage()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var first = g.Own("Cleave1");
        var next = g.Make("Cleave2");
        g.Out(sword, g.Orc, g.Goblin1)[1].Sum(Physical).ShouldBe(-5);
        Ok(g.Relations.Remove(g.Hero.Id, first.Id));
        Ok(g.Relations.Connect(g.Hero.Id, next.Id));
        g.Out(sword, g.Orc, g.Goblin1)[1].Sum(Physical).ShouldBe(-7.5);
    }

    // SKL-03: Given Cleave1 is connected to both Hero and Ally, then both get splash. When Ally switches to Cleave2, Hero still gets 50%.
    [Fact]
    public void SharedCleave_Should_ApplyToEveryConnectedActor()
    {
        var g = new Game();
        var first = g.Own("Cleave1");
        g.Link(g.Ally, first);
        var heroSword = g.Own("Longsword", MainHand);
        var allySword = g.Own("Longsword", MainHand, g.Ally);
        Ok(g.Effects.Outgoing(g.Ally.Id, allySword.Id, g.Orc.Id, new[] { g.Goblin1.Id }))[1].Sum(Physical).ShouldBe(-5);
        var next = g.Make("Cleave2");
        Ok(g.Relations.Remove(g.Ally.Id, first.Id));
        Ok(g.Relations.Connect(g.Ally.Id, next.Id));
        Ok(g.Effects.Outgoing(g.Ally.Id, allySword.Id, g.Orc.Id, new[] { g.Goblin1.Id }))[1].Sum(Physical).ShouldBe(-7.5);
        g.Out(heroSword, g.Orc, g.Goblin1)[1].Sum(Physical).ShouldBe(-5);
    }

    // SKL-05: Given Cleave1 is disconnected, then there is no splash.
    [Fact]
    public void DisconnectedCleave_Should_NotSplash()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var cleave = g.Own("Cleave1");
        Ok(g.Relations.Remove(g.Hero.Id, cleave.Id));
        g.Out(sword, g.Orc, g.Goblin1)[1].Items.ShouldBeEmpty();
    }

    private static Calculator HpCalculator(int order) => new()
    {
        Order = order,
        Value = actor => 50 + 5 * actor.Stats[Str].ModifiedMax
    };
}
