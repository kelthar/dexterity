using NSubstitute;
using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // MT-01: Decrease value 10 adds an item `−10` of its EffectType.
    [Fact]
    public void Decrease_Should_AddNegativeContributions()
    {
        var g = new Game();
        foreach (var type in new[]
        {
            "decrease",
            "increase"
        }

        )
            foreach (var value in new[]
            {
                0d,
                10d
            }

            )
            {
                var modifier = g.World.Modifiers.Single(x => x.Id == type);
                var result = new EffectResult();
                result = modifier.Calculate(result, Mod(type, Physical, value), new EffectContext { OwnerId = g.Hero.Id, InstrumentId = g.Hero.Id }, g.Hero.Id);
                result.Items.Count.ShouldBe(value == 0 ? 0 : 1);
                result.Sum(Physical).ShouldBe(type == "decrease" ? -value : value);
            }
    }

    // MT-02: Increase value 10 adds an item `+10`.
    [Fact]
    public void Increase_Should_AddPositiveContributions()
    {
        var g = new Game();
        foreach (var type in new[]
        {
            "decrease",
            "increase"
        }

        )
            foreach (var value in new[]
            {
                0d,
                10d
            }

            )
            {
                var modifier = g.World.Modifiers.Single(x => x.Id == type);
                var result = new EffectResult();
                result = modifier.Calculate(result, Mod(type, Physical, value), new EffectContext { OwnerId = g.Hero.Id, InstrumentId = g.Hero.Id }, g.Hero.Id);
                result.Items.Count.ShouldBe(value == 0 ? 0 : 1);
                result.Sum(Physical).ShouldBe(type == "decrease" ? -value : value);
            }
    }

    // MT-03: Increase or Decrease with value 0 adds no item (A15).
    [Fact]
    public void ZeroValueModifiers_Should_NotAddContributions()
    {
        var g = new Game();
        foreach (var type in new[]
        {
            "decrease",
            "increase"
        }

        )
            foreach (var value in new[]
            {
                0d,
                10d
            }

            )
            {
                var modifier = g.World.Modifiers.Single(x => x.Id == type);
                var result = new EffectResult();
                result = modifier.Calculate(result, Mod(type, Physical, value), new EffectContext { OwnerId = g.Hero.Id, InstrumentId = g.Hero.Id }, g.Hero.Id);
                result.Items.Count.ShouldBe(value == 0 ? 0 : 1);
                result.Sum(Physical).ShouldBe(type == "decrease" ? -value : value);
            }
    }

    // MT-04: Given the RustySword (5–9) and a random source that returns 7, then the result is `Physical −7`.
    [Fact]
    public void RandomDecrease_Should_UseTheRandomSourceValue()
    {
        var g = new Game();
        var sword = g.Own("RustySword", MainHand);
        foreach (var value in new[]
        {
            5d,
            7d,
            9d
        }

        )
        {
            g.Random.Range(5, 9).Returns(value);
            g.Out(sword)[0].Sum(Physical).ShouldBe(-value);
        }

        sword.ModifierData[0].Data["min"] = 6;
        sword.ModifierData[0].Data["max"] = 6;
        g.Random.Range(6, 6).Returns(6);
        g.Out(sword)[0].Sum(Physical).ShouldBe(-6);
    }

    // MT-05: Given the random source returns min or max, then the result is −5 or −9 (inclusive).
    [Fact]
    public void RandomDecrease_Should_AcceptBothRangeEndpoints()
    {
        var g = new Game();
        var sword = g.Own("RustySword", MainHand);
        foreach (var value in new[]
        {
            5d,
            7d,
            9d
        }

        )
        {
            g.Random.Range(5, 9).Returns(value);
            g.Out(sword)[0].Sum(Physical).ShouldBe(-value);
        }

        sword.ModifierData[0].Data["min"] = 6;
        sword.ModifierData[0].Data["max"] = 6;
        g.Random.Range(6, 6).Returns(6);
        g.Out(sword)[0].Sum(Physical).ShouldBe(-6);
    }

    // MT-06: Given `min == max == 6`, then the result is always −6.
    [Fact]
    public void RandomDecrease_Should_ReturnTheFixedValueForEqualBounds()
    {
        var g = new Game();
        var sword = g.Own("RustySword", MainHand);
        foreach (var value in new[]
        {
            5d,
            7d,
            9d
        }

        )
        {
            g.Random.Range(5, 9).Returns(value);
            g.Out(sword)[0].Sum(Physical).ShouldBe(-value);
        }

        sword.ModifierData[0].Data["min"] = 6;
        sword.ModifierData[0].Data["max"] = 6;
        g.Random.Range(6, 6).Returns(6);
        g.Out(sword)[0].Sum(Physical).ShouldBe(-6);
    }

    // MT-07: Given 1 000 attacks with a real random source, then every result is in `[−9, −5]`.
    [Fact]
    public void RandomDecrease_Should_StayWithinConfiguredBounds()
    {
        var g = new Game();
        var random = new DefaultRandomSource(123);
        g.World.Random = random;
        g.World.Modifiers[g.World.Modifiers.FindIndex(x => x.Id == "randomdecrease")] = new RandomDecrease(random);
        var sword = g.Own("RustySword", MainHand);
        for (var i = 0; i < 1000; i++)
            g.Out(sword)[0].Sum(Physical).ShouldBeInRange(-9, -5);
    }

    // MT-08: Given RandomIncrease Heal 3–5, then the result is in `[+3, +5]`.
    [Fact]
    public void RandomIncrease_Should_ProducePositiveValuesWithinBounds()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        potion.ModifierData = new()
        {
            RandomMod("randomincrease", Heal, 3, 5)
        };
        foreach (var value in new[]
        {
            3d,
            4d,
            5d
        }

        )
        {
            g.Random.Range(3, 5).Returns(value);
            g.Out(potion, g.Hero)[0].Sum(Heal).ShouldBe(value);
        }
    }

    // MT-09: Given the Battleaxe (12) and AxeMastery (×1.5), then the result has `−12` (InstrumentId Battleaxe) and `−6` (InstrumentId AxeMastery), and `Sum(Physical) = −18`.
    [Fact]
    public void AxeMastery_Should_AddAMultiplicativeContribution()
    {
        var g = new Game();
        var axe = g.Own("Battleaxe", MainHand);
        var mastery = g.Own("AxeMastery");
        var hit = g.Out(axe)[0];
        hit.Items.ShouldContain(x => x.Amount == -12 && x.InstrumentId == axe.Id);
        hit.Items.ShouldContain(x => x.Amount == -6 && x.InstrumentId == mastery.Id);
        hit.Sum(Physical).ShouldBe(-18);
    }

    // MT-10: Given ×1 on Physical, then the sum is unchanged and no item is added.
    [Fact]
    public void MultiplierOne_Should_NotAddContributions()
    {
        var factor = 1d;
        var expected = -12d;
        var count = 1;
        var g = new Game();
        var axe = g.Own("Battleaxe", MainHand);
        axe.ModifierData.Add(Mod("multiplier", Physical, factor, "multiplier"));
        var hit = g.Out(axe)[0];
        hit.Sum(Physical).ShouldBe(expected);
        hit.Items.Count.ShouldBe(count);
    }

    // MT-11: Given ×0 on Physical, then `Sum(Physical) = 0`.
    [Fact]
    public void MultiplierZero_Should_CancelTheRunningTotal()
    {
        var factor = 0d;
        var expected = 0d;
        var count = 2;
        var g = new Game();
        var axe = g.Own("Battleaxe", MainHand);
        axe.ModifierData.Add(Mod("multiplier", Physical, factor, "multiplier"));
        var hit = g.Out(axe)[0];
        hit.Sum(Physical).ShouldBe(expected);
        hit.Items.Count.ShouldBe(count);
    }

    // MT-12: Given ×0.5 on Physical and `Physical −12`, then the sum is −6.
    [Fact]
    public void FractionalMultiplier_Should_ReduceTheRunningTotal()
    {
        var factor = 0.5d;
        var expected = -6d;
        var count = 2;
        var g = new Game();
        var axe = g.Own("Battleaxe", MainHand);
        axe.ModifierData.Add(Mod("multiplier", Physical, factor, "multiplier"));
        var hit = g.Out(axe)[0];
        hit.Sum(Physical).ShouldBe(expected);
        hit.Items.Count.ShouldBe(count);
    }

    // MT-13: Given ×1.5 and ×2 on Physical and `Physical −12` (sequential), then the sum is −36.
    [Fact]
    public void Multipliers_Should_ApplySequentially()
    {
        var g = new Game();
        var axe = g.Own("Battleaxe", MainHand);
        axe.ModifierData.Add(Mod("multiplier", Physical, 1.5, "multiplier"));
        axe.ModifierData.Add(Mod("multiplier", Physical, 2, "multiplier"));
        g.Out(axe)[0].Sum(Physical).ShouldBe(-36);
    }

    // MT-14: Given ×2 on Heal and `Heal +10`, then the result is `+20` (same rule as damage, D10).
    [Fact]
    public void Multiplier_Should_ApplyToHealing()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        potion.ModifierData.Add(Mod("multiplier", Heal, 2, "multiplier"));
        g.Out(potion, g.Hero)[0].Sum(Heal).ShouldBe(20);
    }

    // MT-15: Given a Multiplier on Fire, then Physical items are unchanged.
    [Fact]
    public void FireMultiplier_Should_NotChangePhysicalDamage()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData.Add(Mod("multiplier", Fire, 2, "multiplier"));
        sword.ModifierData.Add(Mod("multiplier", null, 2, "multiplier"));
        var hit = g.Out(sword)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
        hit.Sum(Fire).ShouldBe(0);
        sword.ModifierData.RemoveAt(0);
        g.Out(sword)[0].Items.ShouldBeEmpty();
    }

    // MT-16: Given a Multiplier on Physical and a result with no Physical items, then no item is added.
    [Fact]
    public void Multiplier_Should_NotCreateUnmatchedEffectTypes()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData.Add(Mod("multiplier", Fire, 2, "multiplier"));
        sword.ModifierData.Add(Mod("multiplier", null, 2, "multiplier"));
        var hit = g.Out(sword)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
        hit.Sum(Fire).ShouldBe(0);
        sword.ModifierData.RemoveAt(0);
        g.Out(sword)[0].Items.ShouldBeEmpty();
    }

    // MT-25: Given a Multiplier ×2 without an `EffectTypeId`, then no item changes (D9).
    [Fact]
    public void MultiplierWithoutEffectType_Should_NotChangeItems()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData.Add(Mod("multiplier", Fire, 2, "multiplier"));
        sword.ModifierData.Add(Mod("multiplier", null, 2, "multiplier"));
        var hit = g.Out(sword)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
        hit.Sum(Fire).ShouldBe(0);
        sword.ModifierData.RemoveAt(0);
        g.Out(sword)[0].Items.ShouldBeEmpty();
    }

    // MT-18: Given Precision (chance 100, ×2) and the Longsword, then `Sum(Physical) = −20`, and the extra item's InstrumentId is Precision.
    [Fact]
    public void Critical_Should_RecordItsHoldingComponent()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var precision = g.Own("Precision");
        var hit = g.Out(sword)[0];
        hit.Sum(Physical).ShouldBe(-20);
        hit.Items.ShouldContain(x => x.Amount == -10 && x.InstrumentId == precision.Id);
    }

    // MT-19: Given a Critical on Physical with chance 0, then a crit never happens (1 000 attacks).
    [Fact]
    public void ZeroChanceCritical_Should_NeverTrigger()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var precision = g.Own("Precision");
        precision.ModifierData[0].Data["chance"] = 0;
        for (var i = 0; i < 1000; i++)
            g.Out(sword)[0].Sum(Physical).ShouldBe(-10);
    }

    // MT-20: Given a Critical on Physical with chance 30, then a roll of 29.9 crits and a roll of 30 doesn't.
    [Fact]
    public void Critical_Should_UseAStrictChanceBoundary()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var precision = g.Own("Precision");
        precision.ModifierData[0].Data["chance"] = 30;
        g.Random.Roll().Returns(29.9);
        g.Out(sword)[0].Sum(Physical).ShouldBe(-20);
        g.Random.Roll().Returns(30);
        g.Out(sword)[0].Sum(Physical).ShouldBe(-10);
    }

    // MT-21: Given Precision (Critical on Physical and on Fire), the Longsword and RingOfFire, then both Physical (−20) and Fire (−8) are doubled.
    [Fact]
    public void Precision_Should_CritPhysicalAndFireSeparately()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Precision");
        g.Own("RingOfFire", RingLeft);
        var hit = g.Out(sword)[0];
        hit.Sum(Physical).ShouldBe(-20);
        hit.Sum(Fire).ShouldBe(-8);
    }

    // MT-22: Given two Critical modifiers on Physical that both hit (×2 and ×1.5), then the total is ×3 (sequential).
    [Fact]
    public void MultipleCriticals_Should_MultiplySequentially()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var precision = g.Own("Precision");
        var extra = Mod("critical", Physical, 100, "chance", Scope.Passive);
        extra.Data["multiplier"] = 1.5;
        precision.ModifierData.Add(extra);
        g.Out(sword)[0].Sum(Physical).ShouldBe(-30);
    }

    // MT-23: Given a crit with secondaries, then the random source is called once for each Critical modifier per action, not once per target.
    [Fact]
    public void Critical_Should_RollOncePerModifierPerAction()
    {
        var g = new Game();
        var sword = g.Headline("RustySword", false);
        g.Own("Precision");
        g.Random.ClearReceivedCalls();
        var results = g.Out(sword, g.Orc, g.Goblin1, g.Goblin2, g.Goblin3);
        g.Random.Received(2).Roll();
        g.Random.Received(1).Range(5, 9);
        results[0].Sum(Physical).ShouldBe(-14);
        results[1].Sum(Physical).ShouldBe(-7);
    }

    // DET-02: Random values are drawn once per modifier per action, not once per target (SPL-17, MT-23).
    [Fact]
    public void RandomModifiers_Should_DrawOncePerAction()
    {
        var g = new Game();
        var sword = g.Headline("RustySword", false);
        g.Own("Precision");
        g.Random.ClearReceivedCalls();
        var results = g.Out(sword, g.Orc, g.Goblin1, g.Goblin2, g.Goblin3);
        g.Random.Received(2).Roll();
        g.Random.Received(1).Range(5, 9);
        results[0].Sum(Physical).ShouldBe(-14);
        results[1].Sum(Physical).ShouldBe(-7);
    }

    // MT-24: Given a Precision variant with chance 50, and rolls of 10 for the Physical Critical and 80 for the Fire Critical, then only Physical crits (each Critical modifier rolls on its own, D13).
    [Fact]
    public void Precision_Should_UseIndependentRollsForEachEffectType()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        var precision = g.Own("Precision");
        precision.ModifierData.ForEach(x => x.Data["chance"] = 50);
        g.Random.Roll().Returns(10, 80);
        var hit = g.Out(sword)[0];
        hit.Sum(Physical).ShouldBe(-20);
        hit.Sum(Fire).ShouldBe(-4);
    }

    // SPL-01: Given the Longsword, then the Orc gets `Physical −10` and Goblin1 gets `Physical −5`.
    [Fact]
    public void Longsword_Should_SplashHalfItsPhysicalDamage()
    {
        var name = "Longsword";
        var primary = -10d;
        var splash = -5d;
        var g = new Game();
        var weapon = g.Own(name, MainHand);
        g.Own("Cleave1");
        var results = g.Out(weapon, g.Orc, g.Goblin1);
        results[0].Sum(Physical).ShouldBe(primary);
        results[1].Sum(Physical).ShouldBe(splash);
    }

    // SPL-05: Given the Battleaxe (Axe ⊂ Melee), then Goblin1 gets `−6`.
    [Fact]
    public void Battleaxe_Should_SplashHalfItsPhysicalDamage()
    {
        var name = "Battleaxe";
        var primary = -12d;
        var splash = -6d;
        var g = new Game();
        var weapon = g.Own(name, MainHand);
        g.Own("Cleave1");
        var results = g.Out(weapon, g.Orc, g.Goblin1);
        results[0].Sum(Physical).ShouldBe(primary);
        results[1].Sum(Physical).ShouldBe(splash);
    }

    // SPL-06: Given the Greatsword, then Goblin1 gets `−7`.
    [Fact]
    public void Greatsword_Should_SplashHalfItsPhysicalDamage()
    {
        var name = "Greatsword";
        var primary = -14d;
        var splash = -7d;
        var g = new Game();
        var weapon = g.Own(name, MainHand);
        g.Own("Cleave1");
        var results = g.Out(weapon, g.Orc, g.Goblin1);
        results[0].Sum(Physical).ShouldBe(primary);
        results[1].Sum(Physical).ShouldBe(splash);
    }

    // SPL-07: Given the Shortbow, then Goblin1 gets nothing and the Orc gets `−8`.
    [Fact]
    public void Shortbow_Should_NotMatchMeleeSplash()
    {
        var name = "Shortbow";
        var primary = -8d;
        var splash = 0d;
        var g = new Game();
        var weapon = g.Own(name, MainHand);
        g.Own("Cleave1");
        var results = g.Out(weapon, g.Orc, g.Goblin1);
        results[0].Sum(Physical).ShouldBe(primary);
        results[1].Sum(Physical).ShouldBe(splash);
    }

    // SPL-09: Given Fists (Unarmed ⊂ Melee), then Goblin1 gets `Physical −1`.
    [Fact]
    public void Fists_Should_SplashHalfTheirPhysicalDamage()
    {
        var name = "Fists";
        var primary = -2d;
        var splash = -1d;
        var g = new Game();
        var weapon = g.Own(name, MainHand);
        g.Own("Cleave1");
        var results = g.Out(weapon, g.Orc, g.Goblin1);
        results[0].Sum(Physical).ShouldBe(primary);
        results[1].Sum(Physical).ShouldBe(splash);
    }

    // SPL-02: Given Goblin2 at distance 10 (> 3), then Goblin2's result is empty.
    [Fact]
    public void Splash_Should_NotReachDistantTargets()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        var results = g.Out(weapon, g.Orc, g.Goblin2, g.Goblin3);
        results[1].Items.ShouldBeEmpty();
        results[2].Sum(Physical).ShouldBe(-5);
        g.Logic.Received().GetDistance(g.Orc.Id, g.Goblin2.Id);
        g.Logic.Received().GetDistance(g.Orc.Id, g.Goblin3.Id);
        g.Logic.DidNotReceive().GetDistance(g.Hero.Id, Arg.Any<int>());
    }

    // SPL-03: Given Goblin3 at distance exactly 3, then Goblin3 gets `−5` (inclusive, A3).
    [Fact]
    public void Splash_Should_IncludeTargetsAtExactlyItsRange()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        var results = g.Out(weapon, g.Orc, g.Goblin2, g.Goblin3);
        results[1].Items.ShouldBeEmpty();
        results[2].Sum(Physical).ShouldBe(-5);
        g.Logic.Received().GetDistance(g.Orc.Id, g.Goblin2.Id);
        g.Logic.Received().GetDistance(g.Orc.Id, g.Goblin3.Id);
        g.Logic.DidNotReceive().GetDistance(g.Hero.Id, Arg.Any<int>());
    }

    // SPL-04: Distance is read with `IWorldLogic.GetDistance(Orc, secondary)`, not from the Hero (checked with a spy fake).
    [Fact]
    public void Splash_Should_MeasureDistanceFromPrimaryTarget()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        var results = g.Out(weapon, g.Orc, g.Goblin2, g.Goblin3);
        results[1].Items.ShouldBeEmpty();
        results[2].Sum(Physical).ShouldBe(-5);
        g.Logic.Received().GetDistance(g.Orc.Id, g.Goblin2.Id);
        g.Logic.Received().GetDistance(g.Orc.Id, g.Goblin3.Id);
        g.Logic.DidNotReceive().GetDistance(g.Hero.Id, Arg.Any<int>());
    }

    // SPL-08: Given Fireball, then there is no splash.
    [Fact]
    public void Fireball_Should_NotMatchMeleeSplash()
    {
        var g = new Game();
        var spell = g.Own("Fireball");
        g.Own("Cleave1");
        g.Out(spell, g.Orc, g.Goblin1)[1].Items.ShouldBeEmpty();
    }

    // SPL-10: Given Hero without Cleave, then there is no splash with the Longsword.
    [Fact]
    public void MeleeWeapons_Should_NotSplashWithoutCleave()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Out(sword, g.Orc, g.Goblin1)[1].Items.ShouldBeEmpty();
    }

    // SPL-11: Given Ally at distance 1, then Ally gets nothing (Cleave is Enemies only).
    [Fact]
    public void EnemySplash_Should_NotDamageAllies()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        var hits = g.Out(sword, g.Orc, g.Ally, g.Hero);
        hits[1].Items.ShouldBeEmpty();
        hits[2].Items.ShouldBeEmpty();
    }

    // SPL-13: Given Hero (the source) in the secondary list at distance 1, then Hero gets nothing (Cleave is Enemies only).
    [Fact]
    public void EnemySplash_Should_NotDamageItsOwner()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        var hits = g.Out(sword, g.Orc, g.Ally, g.Hero);
        hits[1].Items.ShouldBeEmpty();
        hits[2].Items.ShouldBeEmpty();
    }

    // SPL-12: Given a Cleave variant with `RelationKind = None`, then Ally gets `−5` (friendly fire).
    [Fact]
    public void UnrestrictedSplash_Should_DamageAllies()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1").ModifierData.ForEach(x => x.RelationKind = RelationKind.None);
        var hits = g.Out(sword, g.Orc, g.Ally, g.Hero);
        hits[1].Sum(Physical).ShouldBe(-5);
        hits[2].Sum(Physical).ShouldBe(-5);
    }

    // SPL-27: Given a Cleave variant with `RelationKind = None` and Hero (the source) in the secondary list at distance 1, then Hero gets `Physical −5` (D7, A9: None includes Self).
    [Fact]
    public void UnrestrictedSplash_Should_DamageItsOwner()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1").ModifierData.ForEach(x => x.RelationKind = RelationKind.None);
        var hits = g.Out(sword, g.Orc, g.Ally, g.Hero);
        hits[1].Sum(Physical).ShouldBe(-5);
        hits[2].Sum(Physical).ShouldBe(-5);
    }

    // SPL-14: Given RingOfFire and the Longsword, then Goblin1 gets `Physical −5, Fire −2` (D6).
    [Fact]
    public void Splash_Should_IncludeRingFireContributions()
    {
        var g = new Game();
        var sword = g.Headline(armour: false);
        var hit = g.Out(sword, g.Orc, g.Goblin1)[1];
        hit.Sum(Physical).ShouldBe(-5);
        hit.Sum(Fire).ShouldBe(-2);
    }

    // SPL-15: Given a Cleave variant with only the Physical Splash modifier, then Goblin1 gets `Physical −5` and no Fire.
    [Fact]
    public void PhysicalSplash_Should_NotCopyFireDamage()
    {
        var g = new Game();
        var sword = g.Headline(armour: false);
        g.Repo.Items.Values.Single(x => x.Name == "Cleave").ModifierData.RemoveAll(x => x.EffectTypeId != Physical);
        var hit = g.Out(sword, g.Orc, g.Goblin1)[1];
        hit.Sum(Physical).ShouldBe(-5);
        hit.Sum(Fire).ShouldBe(0);
    }

    // SPL-16: Given Precision (crit ×2), then the Orc gets `−20` and Goblin1 gets `−10` (D13).
    [Fact]
    public void Splash_Should_InheritCriticalDamage()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        g.Own("Precision");
        var hits = g.Out(sword, g.Orc, g.Goblin1);
        hits[0].Sum(Physical).ShouldBe(-20);
        hits[1].Sum(Physical).ShouldBe(-10);
    }

    // SPL-17: Given the RustySword with a roll of 7, then the Orc gets `−7` and Goblin1 gets `−3.5`, with one roll only.
    [Fact]
    public void Splash_Should_ReusePrimaryRandomDamage()
    {
        var g = new Game();
        var sword = g.Own("RustySword", MainHand);
        g.Own("Cleave1");
        var hits = g.Out(sword, g.Orc, g.Goblin1);
        hits[0].Sum(Physical).ShouldBe(-7);
        hits[1].Sum(Physical).ShouldBe(-3.5);
        g.Random.Received(1).Range(5, 9);
    }

    // SPL-18: Given Cleave2 instead of Cleave1, then Goblin1 gets `−7.5`.
    [Fact]
    public void CleaveTwo_Should_SplashSeventyFivePercent()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave2");
        g.Out(sword, g.Orc, g.Goblin1)[1].Sum(Physical).ShouldBe(-7.5);
    }

    // SPL-19: Given Cleave1 (50%) and a second Physical splash modifier (25%) on another component, then Goblin1 gets two items, `−5` and `−2.5`, and `Sum(Physical) = −7.5`. Each splash adds to the total (A9).
    [Fact]
    public void SeparateSplashModifiers_Should_AddSeparateContributions()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        var extra = g.Own("AxeMastery");
        var splash = Mod("splash", Physical, 25, "percent", Scope.Passive);
        splash.Data["range"] = 3;
        extra.ModifierData = new()
        {
            splash
        };
        var hit = g.Out(sword, g.Orc, g.Goblin1)[1];
        hit.Items.Count.ShouldBe(2);
        hit.Items.Select(x => x.Amount).ShouldBe(new[] { -5d, -2.5 });
        hit.Sum(Physical).ShouldBe(-7.5);
    }

    // SPL-20: Given the PoisonDagger, then Goblin1 gets `Physical −2` and `Poison −1, Duration 3` (duration is kept).
    [Fact]
    public void Splash_Should_PreservePoisonDuration()
    {
        var g = new Game();
        var sword = g.Own("PoisonDagger", MainHand);
        var cleave = g.Own("Cleave1");
        var hit = g.Out(sword, g.Orc, g.Goblin1)[1];
        hit.Items.ShouldAllBe(x => x.SourceId == g.Hero.Id && x.InstrumentId == cleave.Id);
        hit.Sum(Physical).ShouldBe(-2);
        hit.Items.ShouldContain(x => x.EffectTypeId == Poison && x.Amount == -1 && x.Duration == 3);
    }

    // SPL-21: Splash items have `SourceId = Hero`, `InstrumentId = Cleave1` and the original EffectTypeId.
    [Fact]
    public void Splash_Should_RecordCleaveProvenance()
    {
        var g = new Game();
        var sword = g.Own("PoisonDagger", MainHand);
        var cleave = g.Own("Cleave1");
        var hit = g.Out(sword, g.Orc, g.Goblin1)[1];
        hit.Items.ShouldAllBe(x => x.SourceId == g.Hero.Id && x.InstrumentId == cleave.Id);
        hit.Sum(Physical).ShouldBe(-2);
        hit.Items.ShouldContain(x => x.EffectTypeId == Poison && x.Amount == -1 && x.Duration == 3);
    }

    // SPL-22: Given Goblin1 wears PlateArmour, when Goblin1's Incoming runs, then Goblin1 ends at `Physical −2` (each target is mitigated on its own).
    [Fact]
    public void SplashTargets_Should_ApplyTheirOwnArmour()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        g.Own("PlateArmour", Body, g.Orc);
        g.Own("PlateArmour", Body, g.Goblin1);
        var outgoing = g.Out(sword, g.Orc, g.Goblin1);
        outgoing[1].Sum(Physical).ShouldBe(-5);
        g.In(outgoing[1])[0].Sum(Physical).ShouldBe(-2);
    }

    // SPL-23: Given the Orc wears PlateArmour, then Goblin1 still gets `−5`, because splash comes from Outgoing, before the Orc's prevention.
    [Fact]
    public void PrimaryArmour_Should_NotReduceOutgoingSplash()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        g.Own("PlateArmour", Body, g.Orc);
        g.Own("PlateArmour", Body, g.Goblin1);
        var outgoing = g.Out(sword, g.Orc, g.Goblin1);
        outgoing[1].Sum(Physical).ShouldBe(-5);
        g.In(outgoing[1])[0].Sum(Physical).ShouldBe(-2);
    }

    // SPL-24: Splash doesn't chain. A goblin that is near Goblin1 but not near the Orc gets nothing.
    [Fact]
    public void Splash_Should_NotChain()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        g.Logic.GetDistance(g.Goblin1.Id, g.Goblin2.Id).Returns(1);
        g.Out(sword, g.Orc, g.Goblin1, g.Goblin2)[2].Items.ShouldBeEmpty();
    }

    // SPL-25: The engine never adds targets on its own. Only the given secondaries can be splashed.
    [Fact]
    public void Splash_Should_NotDiscoverAdditionalTargets()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        g.Out(sword).Select(x => x.TargetId).ShouldBe(new int? [] { g.Orc.Id });
    }

    // SPL-26: Given splash percent 0, then nothing is added and there is no error.
    [Fact]
    public void ZeroPercentSplash_Should_NotAddContributions()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var cleave = g.Own("Cleave1");
        cleave.ModifierData.ForEach(x => x.Data["percent"] = 0);
        g.Out(sword, g.Orc, g.Goblin1)[1].Items.ShouldBeEmpty();
        cleave.ModifierData.ForEach(x =>
        {
            x.Data["percent"] = 50;
            x.EffectTypeId = null;
        });
        g.Out(sword, g.Orc, g.Goblin1)[1].Items.ShouldBeEmpty();
    }

    // SPL-28: Given a Splash modifier without an `EffectTypeId`, then nothing is splashed (D9).
    [Fact]
    public void SplashWithoutEffectType_Should_NotCopyItems()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var cleave = g.Own("Cleave1");
        cleave.ModifierData.ForEach(x => x.Data["percent"] = 0);
        g.Out(sword, g.Orc, g.Goblin1)[1].Items.ShouldBeEmpty();
        cleave.ModifierData.ForEach(x =>
        {
            x.Data["percent"] = 50;
            x.EffectTypeId = null;
        });
        g.Out(sword, g.Orc, g.Goblin1)[1].Items.ShouldBeEmpty();
    }
}
