using NSubstitute;
using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // E2E-01: Given the setup, when Hero attacks Orc with Longsword, then Outgoing returns: Orc `Physical −10, Fire −4`. Goblin1 `Physical −5, Fire −2`. Goblin2 empty. Ally empty.
    [Fact]
    public void LongswordAttack_Should_ProducePrimaryAndSplashDamage()
    {
        var g = new Game();
        var weapon = g.Headline();
        var results = g.Out(weapon, g.Orc, g.Goblin1, g.Goblin2, g.Ally);
        results.Select(x => x.TargetId).ShouldBe(new int? [] { g.Orc.Id, g.Goblin1.Id, g.Goblin2.Id, g.Ally.Id });
        results[0].Sum(Physical).ShouldBe(-10);
        results[0].Sum(Fire).ShouldBe(-4);
        results[1].Sum(Physical).ShouldBe(-5);
        results[1].Sum(Fire).ShouldBe(-2);
        results[2].Items.ShouldBeEmpty();
        results[3].Items.ShouldBeEmpty();
    }

    // E2E-02: Given E2E-01, when Incoming runs for each target, then Orc has `Physical −7` (+3 prevented by PlateArmour) and `Fire −4`. Goblin1 is unchanged at `−5 / −2`.
    [Fact]
    public void LongswordAttack_Should_ApplyEachTargetsPrevention()
    {
        var g = new Game();
        var weapon = g.Headline();
        var results = g.Out(weapon, g.Orc, g.Goblin1).Select(x => g.In(x)[0]).ToList();
        results[0].Sum(Physical).ShouldBe(-7);
        results[0].Sum(Fire).ShouldBe(-4);
        results[1].Sum(Physical).ShouldBe(-5);
        results[1].Sum(Fire).ShouldBe(-2);
    }

    // E2E-03: Given E2E-02, when each result is applied, then Orc HP = 89, Goblin1 HP = 43, Goblin2 HP = 50, Ally HP = 100, and Hero HP = 100.
    [Fact]
    public void LongswordAttack_Should_ChangeOnlyAffectedTargetsHealth()
    {
        var g = new Game();
        var weapon = g.Headline();
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, weapon.Id, g.Orc.Id, g.Goblin1.Id, g.Goblin2.Id, g.Ally.Id));
        Current(g.Orc).ShouldBe(89);
        Current(g.Goblin1).ShouldBe(43);
        Current(g.Goblin2).ShouldBe(50);
        Current(g.Ally).ShouldBe(100);
        Current(g.Hero).ShouldBe(100);
    }

    // E2E-04: Given the setup, when Hero attacks with **Battleaxe** instead (Axe ⊂ Melee), then Goblin1 gets `Physical −6, Fire −2`.
    [Fact]
    public void BattleaxeAttack_Should_SplashPhysicalAndFireDamage()
    {
        var g = new Game();
        var weapon = g.Headline("Battleaxe");
        var secondary = g.Out(weapon, g.Orc, g.Goblin1)[1];
        secondary.Sum(Physical).ShouldBe(-6);
        secondary.Sum(Fire).ShouldBe(-2);
    }

    // E2E-05: Given the setup, when Hero attacks with **Shortbow** (full pipeline), then nobody but the Orc is affected. Orc HP = 100 − 5 − 4 = 91 and Goblin1 HP = 50.
    [Fact]
    public void ShortbowAttack_Should_NotSplash()
    {
        var g = new Game();
        var weapon = g.Headline("Shortbow");
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, weapon.Id, g.Orc.Id, g.Goblin1.Id));
        Current(g.Orc).ShouldBe(91);
        Current(g.Goblin1).ShouldBe(50);
    }

    // E2E-06: Given the setup with RingOfFire moved to **Inventory**, when Hero attacks with Longsword (full pipeline), then there is no fire anywhere. Orc HP = 93 and Goblin1 HP = 45.
    [Fact]
    public void LongswordAttack_Should_NotProduceFireWithoutRingOfFire()
    {
        var g = new Game();
        var weapon = g.Headline();
        var ring = g.Repo.Items.Values.Single(x => x.Name == "RingOfFire");
        Ok(g.Relations.Unequip(g.Hero.Id, ring.Id));
        var resolved = Ok(Pipeline.Run(g.Effects, g.Hero.Id, weapon.Id, g.Orc.Id, g.Goblin1.Id));
        resolved.SelectMany(x => x.Items).ShouldNotContain(x => x.EffectTypeId == Fire);
        Current(g.Orc).ShouldBe(93);
        Current(g.Goblin1).ShouldBe(45);
    }

    // E2E-07: Given the setup with **Cleave1 disconnected**, when Hero attacks with Longsword, then Goblin1 gets nothing. Orc still gets `Physical −10, Fire −4`.
    [Fact]
    public void LongswordAttack_Should_NotSplashWithoutCleave()
    {
        var g = new Game();
        var weapon = g.Headline();
        var skill = g.Repo.Items.Values.Single(x => x.Name == "Cleave");
        Ok(g.Relations.Remove(g.Hero.Id, skill.Id));
        var results = g.Out(weapon, g.Orc, g.Goblin1);
        results[1].Items.ShouldBeEmpty();
        results[0].Sum(Physical).ShouldBe(-10);
        results[0].Sum(Fire).ShouldBe(-4);
    }

    // E2E-08: Given the setup, when Hero casts **Fireball** (Connected) at the Orc, then the Orc gets `Fire −6` and `Fire −4` (Sum Fire = −10), and there is no splash because Fireball has no `melee` tag.
    [Fact]
    public void Fireball_Should_CombineOwnAndRingDamageWithoutSplash()
    {
        var g = new Game();
        g.Headline();
        var spell = g.Own("Fireball");
        var results = g.Out(spell, g.Orc, g.Goblin1);
        results[0].Items.Count.ShouldBe(2);
        results[0].Sum(Fire).ShouldBe(-10);
        results[1].Items.ShouldBeEmpty();
    }

    // E2E-09: Given the setup plus **AxeMastery** and **Precision** connected, when Hero attacks with Battleaxe, then Outgoing gives the Orc `Physical 12 → ×1.5 → 18 → crit ×2 → 36` and `Fire 4 → crit 8`. Goblin1 gets `Physical −18, Fire −4`. After the full pipeline, Orc HP = 100 − 33 − 8 = 59 and Goblin1 HP = 28.
    [Fact]
    public void BattleaxeAttack_Should_ApplyMasteryBeforeCriticalAndSplash()
    {
        var g = new Game();
        var weapon = g.Headline("Battleaxe");
        g.Own("AxeMastery");
        g.Own("Precision");
        var outgoing = g.Out(weapon, g.Orc, g.Goblin1);
        outgoing[0].Sum(Physical).ShouldBe(-36);
        outgoing[0].Sum(Fire).ShouldBe(-8);
        outgoing[1].Sum(Physical).ShouldBe(-18);
        outgoing[1].Sum(Fire).ShouldBe(-4);
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, weapon.Id, g.Orc.Id, g.Goblin1.Id));
        Current(g.Orc).ShouldBe(59);
        Current(g.Goblin1).ShouldBe(28);
    }

    // E2E-10: Given the setup with the **VampDagger** in MainHand instead of the Longsword, an Orc without armour, and **Hero at HP 80/100**, when the full pipeline runs, then the Orc loses 14 and Goblin1 loses 7. Hero's steal results total `+10.5` (50% of 14 + 50% of 7). Hero HP 80 → 90.5.
    [Fact]
    public void PoisonDaggerAttack_Should_ApplyImmediateAndTimedDamage()
    {
        var g = new Game();
        var weapon = g.Headline("VampDagger", false);
        g.Hero.Stats[HP].Current = 80;
        var results = Ok(Pipeline.Run(g.Effects, g.Hero.Id, weapon.Id, g.Orc.Id, g.Goblin1.Id));
        results.Where(x => x.TargetId == g.Hero.Id).Sum(x => x.SumStat(HP, g.World)).ShouldBe(10.5);
        Current(g.Orc).ShouldBe(86);
        Current(g.Goblin1).ShouldBe(43);
        Current(g.Hero).ShouldBe(90.5);
    }

    // E2E-11: Given the setup with **PoisonDagger** in MainHand, when the full pipeline runs, then the Orc gets `Physical −1` (4 − 3) and `Fire −4`, and stores one EffectResult containing `Poison −2 × 3` (HP 95). Goblin1 gets `Physical −2` and `Fire −2`, and stores one EffectResult containing `Poison −1 × 3` (HP 46). Both stored results retain SourceId Hero and InstrumentId PoisonDagger; Goblin1's timed item retains InstrumentId Cleave1. With no Poison prevention, three `Tick(…, 1)` calls give Orc HP 89 and Goblin1 HP 43.
    [Fact]
    public void PoisonDaggerAttack_Should_StoreRawPoisonBeforeTickPrevention()
    {
        var g = new Game();
        var weapon = g.Headline("PoisonDagger");
        var cleave = g.Repo.Items.Values.Single(x => x.Name == "Cleave");
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, weapon.Id, g.Orc.Id, g.Goblin1.Id));
        Current(g.Orc).ShouldBe(95);
        Current(g.Goblin1).ShouldBe(46);
        foreach (var target in new[]
        {
            g.Orc,
            g.Goblin1
        }

        )
        {
            target.Effects.Count.ShouldBe(1);
            var stored = target.Effects[0];
            stored.SourceId.ShouldBe(g.Hero.Id);
            stored.InstrumentId.ShouldBe(weapon.Id);
            stored.Items.Count.ShouldBe(1);
            stored.Items[0].Duration.ShouldBe(3);
            stored.Items[0].Amount.ShouldBe(target == g.Orc ? -2 : -1);
            stored.Items[0].InstrumentId.ShouldBe(target == g.Orc ? weapon.Id : cleave.Id);
            for (var i = 0; i < 3; i++)
                g.Tick(target);
        }

        Current(g.Orc).ShouldBe(89);
        Current(g.Goblin1).ShouldBe(43);
    }

    // COL-01: Given Hero attacks with the Longsword, then the Longsword's Use/Outgoing modifiers are included.
    [Fact]
    public void Outgoing_Should_CollectInstrumentUseModifiers()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        var before = g.Snapshot();
        var results = g.Out(weapon);
        results.Count.ShouldBe(1);
        var hit = results[0];
        hit.TargetId.ShouldBe(g.Orc.Id);
        hit.Items.Count.ShouldBe(1);
        hit.Items[0].ShouldBe(new EffectResultItem { EffectTypeId = Physical, Amount = -10, SourceId = g.Hero.Id, InstrumentId = weapon.Id });
        g.Snapshot().ShouldBe(before);
    }

    // OUT-01: When Hero attacks the Orc with the Longsword (no passives), then the result list has one result with `TargetId = Orc` and one item `Physical −10, Duration 0, SourceId = Hero, InstrumentId = Longsword`.
    [Fact]
    public void Longsword_Should_ProducePhysicalDamageWithProvenance()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        var before = g.Snapshot();
        var results = g.Out(weapon);
        results.Count.ShouldBe(1);
        var hit = results[0];
        hit.TargetId.ShouldBe(g.Orc.Id);
        hit.Items.Count.ShouldBe(1);
        hit.Items[0].ShouldBe(new EffectResultItem { EffectTypeId = Physical, Amount = -10, SourceId = g.Hero.Id, InstrumentId = weapon.Id });
        g.Snapshot().ShouldBe(before);
    }

    // RSL-01: Given Hero attacks the Orc with the equipped Longsword and no passives, when Outgoing is called, then it returns a `Result<List<EffectResult>>` for which `.Failed(...)` is false. Data contains the Orc's EffectResult with `Physical −10`, and no component state changes (OUT-01, OUT-02).
    [Fact]
    public void Outgoing_Should_ReturnASuccessfulGenericResult()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        var before = g.Snapshot();
        var results = g.Out(weapon);
        results.Count.ShouldBe(1);
        var hit = results[0];
        hit.TargetId.ShouldBe(g.Orc.Id);
        hit.Items.Count.ShouldBe(1);
        hit.Items[0].ShouldBe(new EffectResultItem { EffectTypeId = Physical, Amount = -10, SourceId = g.Hero.Id, InstrumentId = weapon.Id });
        g.Snapshot().ShouldBe(before);
    }

    // COL-02: Given Hero attacks, then Passive/Outgoing modifiers of components that are **Equipped** (ring) or **Connected** (skill) to Hero are included.
    [Fact]
    public void Outgoing_Should_CollectOwnerEquippedAndConnectedPassives()
    {
        var g = new Game();
        var weapon = g.Headline(armour: false);
        var results = g.Out(weapon, g.Orc, g.Goblin1);
        results[0].Sum(Fire).ShouldBe(-4);
        results[1].Sum(Physical).ShouldBe(-5);
    }

    // COL-03: Given Hero attacks with the Longsword, then Passive/Outgoing modifiers of components Connected to the Longsword (Ruby) are included (D23).
    [Fact]
    public void Outgoing_Should_CollectInstrumentConnectedPassives()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        var ruby = g.Make("Ruby");
        g.Link(weapon, ruby);
        g.Out(weapon)[0].Sum(Fire).ShouldBe(-8);
    }

    // MT-17: Given the Ruby (×2 Fire) in the Longsword and RingOfFire, when Hero attacks with the Longsword, then `Sum(Fire) = −8` (the ring's fire is multiplied).
    [Fact]
    public void Ruby_Should_ApplyToItsHoldingInstrument()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        var ruby = g.Make("Ruby");
        g.Link(weapon, ruby);
        g.Out(weapon)[0].Sum(Fire).ShouldBe(-8);
    }

    // COL-04: Given RingOfFire is in Hero's **Inventory**, then its modifiers are not included (A7).
    [Fact]
    public void RingOfFire_Should_NotApplyFromInventory()
    {
        var name = "RingOfFire";
        var slot = 0;
        var owner = "Hero";
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own(name, slot, owner == "Orc" ? g.Orc : owner == "Ally" ? g.Ally : g.Hero);
        var hit = g.Out(weapon)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-05: Given the Battleaxe is equipped in OffHand and Hero attacks with the Longsword, then the Battleaxe's Use modifiers are not included (Orc gets −10 only).
    [Fact]
    public void Battleaxe_Should_NotApplyUseModifiersToLongswordAttacks()
    {
        var name = "Battleaxe";
        var slot = OffHand;
        var owner = "Hero";
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own(name, slot, owner == "Orc" ? g.Orc : owner == "Ally" ? g.Ally : g.Hero);
        var hit = g.Out(weapon)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-06: Given Hero wears PlateArmour, when Hero attacks, then PlateArmour (Passive/Incoming) is not included.
    [Fact]
    public void PlateArmour_Should_NotApplyDuringOutgoing()
    {
        var name = "PlateArmour";
        var slot = Body;
        var owner = "Hero";
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own(name, slot, owner == "Orc" ? g.Orc : owner == "Ally" ? g.Ally : g.Hero);
        var hit = g.Out(weapon)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-07: Given Hero wears StrengthRing, when Hero attacks, then StrengthRing (StatsRecalc) adds no damage.
    [Fact]
    public void StrengthRing_Should_NotAddOutgoingDamage()
    {
        var name = "StrengthRing";
        var slot = RingLeft;
        var owner = "Hero";
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own(name, slot, owner == "Orc" ? g.Orc : owner == "Ally" ? g.Ally : g.Hero);
        var hit = g.Out(weapon)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-09: When Hero attacks, then the target's modifiers are never included in Outgoing.
    [Fact]
    public void Outgoing_Should_NotCollectTargetModifiers()
    {
        var name = "RingOfFire";
        var slot = RingLeft;
        var owner = "Orc";
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own(name, slot, owner == "Orc" ? g.Orc : owner == "Ally" ? g.Ally : g.Hero);
        var hit = g.Out(weapon)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-10: Given Ally wears RingOfFire, when Hero attacks, then Ally's ring is not included.
    [Fact]
    public void Outgoing_Should_NotCollectUnrelatedAllyPassives()
    {
        var name = "RingOfFire";
        var slot = RingLeft;
        var owner = "Ally";
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own(name, slot, owner == "Orc" ? g.Orc : owner == "Ally" ? g.Ally : g.Hero);
        var hit = g.Out(weapon)[0];
        hit.Items.Count.ShouldBe(1);
        hit.Sum(Physical).ShouldBe(-10);
    }

    // COL-08: Given the Ruby is in the Longsword, when Hero casts Fireball, then the Ruby is not included (D23).
    [Fact]
    public void Ruby_Should_NotModifyAnotherInstrumentsAction()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        var ruby = g.Make("Ruby");
        g.Link(sword, ruby);
        var spell = g.Own("Fireball");
        g.Out(sword)[0].Sum(Fire).ShouldBe(-8);
        g.Out(spell)[0].Sum(Fire).ShouldBe(-10);
    }

    // RNG-13: Given the Ruby in the Longsword, then the Longsword attack gives `Fire −8`. Fireball gives `Fire −10` (no Ruby, D23).
    [Fact]
    public void Ruby_Should_MultiplyInstrumentFireDamage()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        var ruby = g.Make("Ruby");
        g.Link(sword, ruby);
        var spell = g.Own("Fireball");
        g.Out(sword)[0].Sum(Fire).ShouldBe(-8);
        g.Out(spell)[0].Sum(Fire).ShouldBe(-10);
    }

    // COL-16: Given a modifier with empty IncludedTags, then it applies with any instrument.
    [Fact]
    public void EmptyIncludedTags_Should_MatchEveryInstrument()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        g.Out(sword)[0].Sum(Fire).ShouldBe(-4);
    }

    // COL-17: Given IncludedTags `[melee]`, then it matches the Longsword, the Greatsword and the Battleaxe.
    [Fact]
    public void MeleeFilter_Should_MatchMeleeWeapons()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "Greatsword",
            "Battleaxe",
            "Fists",
            "Shortbow",
            "Fireball"
        }

        )
            foreach (var filter in new[]
            {
                "MELEE",
                "axe,bow",
                "melee-except-axe",
                "except-axe"
            }

            )
            {
                var g = new Game();
                var weapon = g.Own(name, name == "Fireball" ? 0 : MainHand);
                var ring = g.Own("RingOfFire", RingLeft);
                var data = ring.ModifierData[0];
                data.IncludedTags = filter switch
                {
                    "MELEE" => new()
                    {
                        "MELEE"
                    },
                    "axe,bow" => new()
                    {
                        "axe",
                        "bow"
                    },
                    "melee-except-axe" => new()
                    {
                        "melee"
                    },
                    _ => new()};
                if (filter.Contains("except"))
                    data.ExcludedTags.Add("axe");
                bool matches = filter switch
                {
                    "MELEE" => name is not ("Shortbow" or "Fireball"),
                    "axe,bow" => name is "Battleaxe" or "Shortbow",
                    "melee-except-axe" => name is not ("Battleaxe" or "Shortbow" or "Fireball"),
                    _ => name != "Battleaxe"
                };
                g.Out(weapon)[0].Items.Count(x => x.InstrumentId == ring.Id).ShouldBe(matches ? 1 : 0);
            }
    }

    // COL-18: Given IncludedTags `[melee]`, then it doesn't match the Shortbow.
    [Fact]
    public void MeleeFilter_Should_NotMatchShortbow()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "Greatsword",
            "Battleaxe",
            "Fists",
            "Shortbow",
            "Fireball"
        }

        )
            foreach (var filter in new[]
            {
                "MELEE",
                "axe,bow",
                "melee-except-axe",
                "except-axe"
            }

            )
            {
                var g = new Game();
                var weapon = g.Own(name, name == "Fireball" ? 0 : MainHand);
                var ring = g.Own("RingOfFire", RingLeft);
                var data = ring.ModifierData[0];
                data.IncludedTags = filter switch
                {
                    "MELEE" => new()
                    {
                        "MELEE"
                    },
                    "axe,bow" => new()
                    {
                        "axe",
                        "bow"
                    },
                    "melee-except-axe" => new()
                    {
                        "melee"
                    },
                    _ => new()};
                if (filter.Contains("except"))
                    data.ExcludedTags.Add("axe");
                bool matches = filter switch
                {
                    "MELEE" => name is not ("Shortbow" or "Fireball"),
                    "axe,bow" => name is "Battleaxe" or "Shortbow",
                    "melee-except-axe" => name is not ("Battleaxe" or "Shortbow" or "Fireball"),
                    _ => name != "Battleaxe"
                };
                g.Out(weapon)[0].Items.Count(x => x.InstrumentId == ring.Id).ShouldBe(matches ? 1 : 0);
            }
    }

    // COL-19: Given IncludedTags `[melee]`, then it matches Fists (Unarmed ⊂ Melee).
    [Fact]
    public void MeleeFilter_Should_MatchFists()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "Greatsword",
            "Battleaxe",
            "Fists",
            "Shortbow",
            "Fireball"
        }

        )
            foreach (var filter in new[]
            {
                "MELEE",
                "axe,bow",
                "melee-except-axe",
                "except-axe"
            }

            )
            {
                var g = new Game();
                var weapon = g.Own(name, name == "Fireball" ? 0 : MainHand);
                var ring = g.Own("RingOfFire", RingLeft);
                var data = ring.ModifierData[0];
                data.IncludedTags = filter switch
                {
                    "MELEE" => new()
                    {
                        "MELEE"
                    },
                    "axe,bow" => new()
                    {
                        "axe",
                        "bow"
                    },
                    "melee-except-axe" => new()
                    {
                        "melee"
                    },
                    _ => new()};
                if (filter.Contains("except"))
                    data.ExcludedTags.Add("axe");
                bool matches = filter switch
                {
                    "MELEE" => name is not ("Shortbow" or "Fireball"),
                    "axe,bow" => name is "Battleaxe" or "Shortbow",
                    "melee-except-axe" => name is not ("Battleaxe" or "Shortbow" or "Fireball"),
                    _ => name != "Battleaxe"
                };
                g.Out(weapon)[0].Items.Count(x => x.InstrumentId == ring.Id).ShouldBe(matches ? 1 : 0);
            }
    }

    // COL-20: Given IncludedTags `[melee]`, then it doesn't match Fireball.
    [Fact]
    public void MeleeFilter_Should_NotMatchFireball()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "Greatsword",
            "Battleaxe",
            "Fists",
            "Shortbow",
            "Fireball"
        }

        )
            foreach (var filter in new[]
            {
                "MELEE",
                "axe,bow",
                "melee-except-axe",
                "except-axe"
            }

            )
            {
                var g = new Game();
                var weapon = g.Own(name, name == "Fireball" ? 0 : MainHand);
                var ring = g.Own("RingOfFire", RingLeft);
                var data = ring.ModifierData[0];
                data.IncludedTags = filter switch
                {
                    "MELEE" => new()
                    {
                        "MELEE"
                    },
                    "axe,bow" => new()
                    {
                        "axe",
                        "bow"
                    },
                    "melee-except-axe" => new()
                    {
                        "melee"
                    },
                    _ => new()};
                if (filter.Contains("except"))
                    data.ExcludedTags.Add("axe");
                bool matches = filter switch
                {
                    "MELEE" => name is not ("Shortbow" or "Fireball"),
                    "axe,bow" => name is "Battleaxe" or "Shortbow",
                    "melee-except-axe" => name is not ("Battleaxe" or "Shortbow" or "Fireball"),
                    _ => name != "Battleaxe"
                };
                g.Out(weapon)[0].Items.Count(x => x.InstrumentId == ring.Id).ShouldBe(matches ? 1 : 0);
            }
    }

    // COL-21: Given IncludedTags `[axe, bow]`, then it matches the Battleaxe and the Shortbow (any).
    [Fact]
    public void IncludedTags_Should_MatchAnyListedTag()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "Greatsword",
            "Battleaxe",
            "Fists",
            "Shortbow",
            "Fireball"
        }

        )
            foreach (var filter in new[]
            {
                "MELEE",
                "axe,bow",
                "melee-except-axe",
                "except-axe"
            }

            )
            {
                var g = new Game();
                var weapon = g.Own(name, name == "Fireball" ? 0 : MainHand);
                var ring = g.Own("RingOfFire", RingLeft);
                var data = ring.ModifierData[0];
                data.IncludedTags = filter switch
                {
                    "MELEE" => new()
                    {
                        "MELEE"
                    },
                    "axe,bow" => new()
                    {
                        "axe",
                        "bow"
                    },
                    "melee-except-axe" => new()
                    {
                        "melee"
                    },
                    _ => new()};
                if (filter.Contains("except"))
                    data.ExcludedTags.Add("axe");
                bool matches = filter switch
                {
                    "MELEE" => name is not ("Shortbow" or "Fireball"),
                    "axe,bow" => name is "Battleaxe" or "Shortbow",
                    "melee-except-axe" => name is not ("Battleaxe" or "Shortbow" or "Fireball"),
                    _ => name != "Battleaxe"
                };
                g.Out(weapon)[0].Items.Count(x => x.InstrumentId == ring.Id).ShouldBe(matches ? 1 : 0);
            }
    }

    // COL-22: Given IncludedTags `[melee]` and ExcludedTags `[axe]`, then the Battleaxe is excluded and the Longsword is included.
    [Fact]
    public void ExcludedTags_Should_OverrideIncludedTags()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "Greatsword",
            "Battleaxe",
            "Fists",
            "Shortbow",
            "Fireball"
        }

        )
            foreach (var filter in new[]
            {
                "MELEE",
                "axe,bow",
                "melee-except-axe",
                "except-axe"
            }

            )
            {
                var g = new Game();
                var weapon = g.Own(name, name == "Fireball" ? 0 : MainHand);
                var ring = g.Own("RingOfFire", RingLeft);
                var data = ring.ModifierData[0];
                data.IncludedTags = filter switch
                {
                    "MELEE" => new()
                    {
                        "MELEE"
                    },
                    "axe,bow" => new()
                    {
                        "axe",
                        "bow"
                    },
                    "melee-except-axe" => new()
                    {
                        "melee"
                    },
                    _ => new()};
                if (filter.Contains("except"))
                    data.ExcludedTags.Add("axe");
                bool matches = filter switch
                {
                    "MELEE" => name is not ("Shortbow" or "Fireball"),
                    "axe,bow" => name is "Battleaxe" or "Shortbow",
                    "melee-except-axe" => name is not ("Battleaxe" or "Shortbow" or "Fireball"),
                    _ => name != "Battleaxe"
                };
                g.Out(weapon)[0].Items.Count(x => x.InstrumentId == ring.Id).ShouldBe(matches ? 1 : 0);
            }
    }

    // COL-23: Given empty IncludedTags and ExcludedTags `[axe]`, then the Battleaxe is excluded and every other instrument is included.
    [Fact]
    public void ExcludedTags_Should_WorkWithoutIncludedTags()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "Greatsword",
            "Battleaxe",
            "Fists",
            "Shortbow",
            "Fireball"
        }

        )
            foreach (var filter in new[]
            {
                "MELEE",
                "axe,bow",
                "melee-except-axe",
                "except-axe"
            }

            )
            {
                var g = new Game();
                var weapon = g.Own(name, name == "Fireball" ? 0 : MainHand);
                var ring = g.Own("RingOfFire", RingLeft);
                var data = ring.ModifierData[0];
                data.IncludedTags = filter switch
                {
                    "MELEE" => new()
                    {
                        "MELEE"
                    },
                    "axe,bow" => new()
                    {
                        "axe",
                        "bow"
                    },
                    "melee-except-axe" => new()
                    {
                        "melee"
                    },
                    _ => new()};
                if (filter.Contains("except"))
                    data.ExcludedTags.Add("axe");
                bool matches = filter switch
                {
                    "MELEE" => name is not ("Shortbow" or "Fireball"),
                    "axe,bow" => name is "Battleaxe" or "Shortbow",
                    "melee-except-axe" => name is not ("Battleaxe" or "Shortbow" or "Fireball"),
                    _ => name != "Battleaxe"
                };
                g.Out(weapon)[0].Items.Count(x => x.InstrumentId == ring.Id).ShouldBe(matches ? 1 : 0);
            }
    }

    // TAG-05: Given IncludedTags `[MELEE]`, then it matches the `melee` tag (A21).
    [Fact]
    public void TagFilters_Should_IgnoreCase()
    {
        foreach (var name in new[]
        {
            "Longsword",
            "Greatsword",
            "Battleaxe",
            "Fists",
            "Shortbow",
            "Fireball"
        }

        )
            foreach (var filter in new[]
            {
                "MELEE",
                "axe,bow",
                "melee-except-axe",
                "except-axe"
            }

            )
            {
                var g = new Game();
                var weapon = g.Own(name, name == "Fireball" ? 0 : MainHand);
                var ring = g.Own("RingOfFire", RingLeft);
                var data = ring.ModifierData[0];
                data.IncludedTags = filter switch
                {
                    "MELEE" => new()
                    {
                        "MELEE"
                    },
                    "axe,bow" => new()
                    {
                        "axe",
                        "bow"
                    },
                    "melee-except-axe" => new()
                    {
                        "melee"
                    },
                    _ => new()};
                if (filter.Contains("except"))
                    data.ExcludedTags.Add("axe");
                bool matches = filter switch
                {
                    "MELEE" => name is not ("Shortbow" or "Fireball"),
                    "axe,bow" => name is "Battleaxe" or "Shortbow",
                    "melee-except-axe" => name is not ("Battleaxe" or "Shortbow" or "Fireball"),
                    _ => name != "Battleaxe"
                };
                g.Out(weapon)[0].Items.Count(x => x.InstrumentId == ring.Id).ShouldBe(matches ? 1 : 0);
            }
    }

    // COL-24: Given IncludedTags `[silver]` and a Longsword with its own tag `silver`, then it matches only that Longsword instance.
    [Fact]
    public void IncludedTags_Should_MatchComponentInstanceTags()
    {
        var g = new Game();
        var silver = g.Own("Longsword", MainHand);
        silver.Tags.Add("silver");
        var plain = g.Own("Longsword", OffHand);
        var ring = g.Own("RingOfFire", RingLeft);
        ring.ModifierData[0].IncludedTags.Add("silver");
        g.Out(silver)[0].Sum(Fire).ShouldBe(-4);
        g.Out(plain)[0].Sum(Fire).ShouldBe(0);
    }

    // COL-25: Given WhetstoneCharm (IncludedTags `[sword]`), when Hero attacks with the Longsword, then there is an extra `Physical −2`. With the Battleaxe there is no extra damage.
    [Fact]
    public void WhetstoneCharm_Should_AddPhysicalDamageToLongsword()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var axe = g.Own("Battleaxe", OffHand);
        g.Own("WhetstoneCharm", Neck);
        var hit = g.Out(sword)[0];
        hit.Items.Count.ShouldBe(2);
        hit.Sum(Physical).ShouldBe(-12);
        g.Out(axe)[0].Sum(Physical).ShouldBe(-12);
    }

    // OUT-17: Given the Longsword and WhetstoneCharm, then the result has two Physical items and `Sum(Physical) = −12`.
    [Fact]
    public void WhetstoneCharm_Should_KeepSeparatePhysicalContributions()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var axe = g.Own("Battleaxe", OffHand);
        g.Own("WhetstoneCharm", Neck);
        var hit = g.Out(sword)[0];
        hit.Items.Count.ShouldBe(2);
        hit.Sum(Physical).ShouldBe(-12);
        g.Out(axe)[0].Sum(Physical).ShouldBe(-12);
    }

    // COL-26: Given `RelationKind = None`, then the modifier applies when the target is Self, Friend or Enemy.
    [Fact]
    public void RelationKindNone_Should_MatchEveryRelation()
    {
        foreach (var relation in new[]
        {
            RelationKind.None,
            RelationKind.Enemies,
            RelationKind.Self | RelationKind.Friends,
            RelationKind.Self
        }

        )
            foreach (var party in new[]
            {
                "Hero",
                "Ally",
                "Orc"
            }

            )
            {
                var g = new Game();
                var sword = g.Own("Longsword", MainHand);
                sword.ModifierData[0].RelationKind = relation;
                var target = party == "Hero" ? g.Hero : party == "Ally" ? g.Ally : g.Orc;
                bool matches = relation == RelationKind.None || (relation == RelationKind.Self ? party == "Hero" : (relation == RelationKind.Enemies) == (party == "Orc"));
                g.Out(sword, target)[0].Sum(Physical).ShouldBe(matches ? -10 : 0);
            }
    }

    // COL-27: Given `RelationKind = Enemies` and `GetRelationKind(Hero, Orc) = Enemies`, then it applies.
    [Fact]
    public void EnemyFilter_Should_MatchEnemies()
    {
        foreach (var relation in new[]
        {
            RelationKind.None,
            RelationKind.Enemies,
            RelationKind.Self | RelationKind.Friends,
            RelationKind.Self
        }

        )
            foreach (var party in new[]
            {
                "Hero",
                "Ally",
                "Orc"
            }

            )
            {
                var g = new Game();
                var sword = g.Own("Longsword", MainHand);
                sword.ModifierData[0].RelationKind = relation;
                var target = party == "Hero" ? g.Hero : party == "Ally" ? g.Ally : g.Orc;
                bool matches = relation == RelationKind.None || (relation == RelationKind.Self ? party == "Hero" : (relation == RelationKind.Enemies) == (party == "Orc"));
                g.Out(sword, target)[0].Sum(Physical).ShouldBe(matches ? -10 : 0);
            }
    }

    // COL-28: Given `RelationKind = Enemies` and target Ally, then it doesn't apply.
    [Fact]
    public void EnemyFilter_Should_NotMatchFriends()
    {
        foreach (var relation in new[]
        {
            RelationKind.None,
            RelationKind.Enemies,
            RelationKind.Self | RelationKind.Friends,
            RelationKind.Self
        }

        )
            foreach (var party in new[]
            {
                "Hero",
                "Ally",
                "Orc"
            }

            )
            {
                var g = new Game();
                var sword = g.Own("Longsword", MainHand);
                sword.ModifierData[0].RelationKind = relation;
                var target = party == "Hero" ? g.Hero : party == "Ally" ? g.Ally : g.Orc;
                bool matches = relation == RelationKind.None || (relation == RelationKind.Self ? party == "Hero" : (relation == RelationKind.Enemies) == (party == "Orc"));
                g.Out(sword, target)[0].Sum(Physical).ShouldBe(matches ? -10 : 0);
            }
    }

    // COL-29: Given `RelationKind = Enemies` and target Hero (Self), then it doesn't apply.
    [Fact]
    public void EnemyFilter_Should_NotMatchSelf()
    {
        foreach (var relation in new[]
        {
            RelationKind.None,
            RelationKind.Enemies,
            RelationKind.Self | RelationKind.Friends,
            RelationKind.Self
        }

        )
            foreach (var party in new[]
            {
                "Hero",
                "Ally",
                "Orc"
            }

            )
            {
                var g = new Game();
                var sword = g.Own("Longsword", MainHand);
                sword.ModifierData[0].RelationKind = relation;
                var target = party == "Hero" ? g.Hero : party == "Ally" ? g.Ally : g.Orc;
                bool matches = relation == RelationKind.None || (relation == RelationKind.Self ? party == "Hero" : (relation == RelationKind.Enemies) == (party == "Orc"));
                g.Out(sword, target)[0].Sum(Physical).ShouldBe(matches ? -10 : 0);
            }
    }

    // COL-30: Given `RelationKind = Self|Friends`, then it applies to Hero and Ally but not to the Orc.
    [Fact]
    public void CombinedRelationFilters_Should_MatchAnyListedRelation()
    {
        foreach (var relation in new[]
        {
            RelationKind.None,
            RelationKind.Enemies,
            RelationKind.Self | RelationKind.Friends,
            RelationKind.Self
        }

        )
            foreach (var party in new[]
            {
                "Hero",
                "Ally",
                "Orc"
            }

            )
            {
                var g = new Game();
                var sword = g.Own("Longsword", MainHand);
                sword.ModifierData[0].RelationKind = relation;
                var target = party == "Hero" ? g.Hero : party == "Ally" ? g.Ally : g.Orc;
                bool matches = relation == RelationKind.None || (relation == RelationKind.Self ? party == "Hero" : (relation == RelationKind.Enemies) == (party == "Orc"));
                g.Out(sword, target)[0].Sum(Physical).ShouldBe(matches ? -10 : 0);
            }
    }

    // COL-32: Given the same component as source and target, then the relation is Self.
    [Fact]
    public void SameSourceAndTarget_Should_HaveSelfRelation()
    {
        foreach (var relation in new[]
        {
            RelationKind.None,
            RelationKind.Enemies,
            RelationKind.Self | RelationKind.Friends,
            RelationKind.Self
        }

        )
            foreach (var party in new[]
            {
                "Hero",
                "Ally",
                "Orc"
            }

            )
            {
                var g = new Game();
                var sword = g.Own("Longsword", MainHand);
                sword.ModifierData[0].RelationKind = relation;
                var target = party == "Hero" ? g.Hero : party == "Ally" ? g.Ally : g.Orc;
                bool matches = relation == RelationKind.None || (relation == RelationKind.Self ? party == "Hero" : (relation == RelationKind.Enemies) == (party == "Orc"));
                g.Out(sword, target)[0].Sum(Physical).ShouldBe(matches ? -10 : 0);
            }
    }

    // COL-33: Given a modifier with `EffectTypeId = Fire`, then it changes only Fire items.
    [Fact]
    public void Modifiers_Should_ChangeOnlyTheirEffectType()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        var ruby = g.Make("Ruby");
        g.Link(sword, ruby);
        var hit = g.Out(sword)[0];
        hit.Sum(Physical).ShouldBe(-10);
        hit.Sum(Fire).ShouldBe(-8);
    }

    // COL-34: Given a modifier without an `EffectTypeId`, then it changes no items at all (D9).
    [Fact]
    public void ModifiersWithoutEffectType_Should_MatchNothing()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var ring = g.Own("RingOfFire", RingLeft);
        ring.ModifierData[0].EffectTypeId = null;
        g.Out(sword)[0].Items.Count.ShouldBe(1);
    }

    // OUT-02: After Outgoing, no stat on any component has changed.
    [Fact]
    public void Outgoing_Should_NotChangeStats()
    {
        var g = new Game();
        var weapon = g.Headline();
        var before = g.Snapshot();
        var first = g.Out(weapon, g.Orc, g.Goblin1);
        var second = g.Out(weapon, g.Orc, g.Goblin1);
        System.Text.Json.JsonSerializer.Serialize(second).ShouldBe(System.Text.Json.JsonSerializer.Serialize(first));
        g.Snapshot().ShouldBe(before);
    }

    // OUT-15: Given the same input called twice, then the results are equal, and no component's Data, Stats or ModifierData changed.
    [Fact]
    public void Outgoing_Should_BeRepeatableWithoutMutation()
    {
        var g = new Game();
        var weapon = g.Headline();
        var before = g.Snapshot();
        var first = g.Out(weapon, g.Orc, g.Goblin1);
        var second = g.Out(weapon, g.Orc, g.Goblin1);
        System.Text.Json.JsonSerializer.Serialize(second).ShouldBe(System.Text.Json.JsonSerializer.Serialize(first));
        g.Snapshot().ShouldBe(before);
    }

    // SKL-04: After any attack, Cleave1's Stats, Data and ModifierData are unchanged (shared components are never mutated).
    [Fact]
    public void Attacks_Should_NotMutateSharedSkillState()
    {
        var g = new Game();
        var weapon = g.Headline();
        var before = g.Snapshot();
        var first = g.Out(weapon, g.Orc, g.Goblin1);
        var second = g.Out(weapon, g.Orc, g.Goblin1);
        System.Text.Json.JsonSerializer.Serialize(second).ShouldBe(System.Text.Json.JsonSerializer.Serialize(first));
        g.Snapshot().ShouldBe(before);
    }

    // OUT-03: Given the primary and three secondaries, then four results are returned: the primary first, then the secondaries in input order, empty ones included (A17).
    [Fact]
    public void Outgoing_Should_PreserveTargetOrderAndEmptyResults()
    {
        var g = new Game();
        var weapon = g.Headline();
        var results = g.Out(weapon, g.Orc, g.Orc, g.Goblin1, g.Goblin2, g.Goblin1, g.Ally);
        results.Select(x => x.TargetId).ShouldBe(new int? [] { g.Orc.Id, g.Goblin1.Id, g.Goblin2.Id, g.Ally.Id });
        results[0].Sum(Physical).ShouldBe(-10);
    }

    // OUT-14: Given the secondary list contains the primary or duplicates, then each target appears once and the primary gets only primary items.
    [Fact]
    public void Outgoing_Should_DeduplicateTargets()
    {
        var g = new Game();
        var weapon = g.Headline();
        var results = g.Out(weapon, g.Orc, g.Orc, g.Goblin1, g.Goblin2, g.Goblin1, g.Ally);
        results.Select(x => x.TargetId).ShouldBe(new int? [] { g.Orc.Id, g.Goblin1.Id, g.Goblin2.Id, g.Ally.Id });
        results[0].Sum(Physical).ShouldBe(-10);
    }

    // OUT-04: Given modifiers with Orders 100, 200 and 300, then they run in ascending Order regardless of collection order.
    [Fact]
    public void Modifiers_Should_RunInAscendingOrder()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        weapon.ModifierData.Insert(0, Mod("critical", Physical, 100, "chance"));
        weapon.ModifierData[0].Data["multiplier"] = 2;
        weapon.ModifierData.Insert(0, Mod("multiplier", Physical, 1.5, "multiplier"));
        g.Out(weapon)[0].Sum(Physical).ShouldBe(-30);
    }

    // OUT-05: Given two modifiers with the same Order, then the instrument's modifier runs first, then passives by component Id, then by list index (A6). The result is the same on every run.
    [Fact]
    public void EqualOrderModifiers_Should_RunInStableCollectionOrder()
    {
        var g = new Game();
        var order = new List<int>();
        var spy = new SpyModifier
        {
            Order = 100,
            Run = (result, _, context) =>
            {
                order.Add(result.Items.Count);
                return result;
            }
        };
        g.World.Modifiers.Add(spy);
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData.Add(Mod("spy", Physical, 1));
        var first = g.Own("RingOfFire", RingLeft);
        first.ModifierData.Add(Mod("spy", Fire, 1, scope: Scope.Passive));
        first.ModifierData.Add(Mod("decrease", Fire, 1, scope: Scope.Passive));
        first.ModifierData.Add(Mod("spy", Fire, 1, scope: Scope.Passive));
        var second = g.Own("RingOfFire", RingRight);
        second.ModifierData.Add(Mod("spy", Fire, 1, scope: Scope.Passive));
        g.Repo.Relations.Reverse();
        for (var i = 0; i < 3; i++)
        {
            order.Clear();
            spy.Holders.Clear();
            g.Out(sword);
            order.ShouldBe(new[] { 1, 2, 3, 4 });
            spy.Holders.ShouldBe(new[] { sword.Id, first.Id, first.Id, second.Id });
        }
    }

    // OUT-06: Each item's `SourceId` identifies the actor that owns the modifier that created it, and its `InstrumentId` identifies the component holding that modifier (D33): Hero + Longsword, Hero + RingOfFire, Hero + Cleave1 for splash shares, Hero + AxeMastery for multiplier items, and Orc + PlateArmour for prevention items.
    [Fact]
    public void Contributions_Should_IdentifyModifierOwnerAndHolder()
    {
        var g = new Game();
        var weapon = g.Headline("Battleaxe");
        var mastery = g.Own("AxeMastery");
        var outgoing = g.Out(weapon, g.Orc, g.Goblin1);
        outgoing.SelectMany(x => x.Items).ShouldAllBe(x => x.SourceId == g.Hero.Id);
        outgoing[0].Items.ShouldContain(x => x.InstrumentId == weapon.Id);
        outgoing[0].Items.ShouldContain(x => x.InstrumentId == mastery.Id && x.Amount == -6);
        var armour = g.Repo.Items.Values.Single(x => x.Name == "PlateArmour");
        g.In(outgoing[0])[0].Items.ShouldContain(x => x.SourceId == g.Orc.Id && x.InstrumentId == armour.Id && x.Amount == 3);
    }

    // OUT-07: When Hero uses the HealingPotion on himself, then Hero gets `Heal +10`. On Ally, Ally gets `Heal +10`.
    [Fact]
    public void HealingPotion_Should_HealItsOwner()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        foreach (var target in new[]
        {
            g.Hero,
            g.Ally
        }

        )
            g.Out(potion, target)[0].Sum(Heal).ShouldBe(10);
    }

    // OUT-11: Given the HealingPotion is in Hero's **Inventory** (not equipable), when Hero uses it, then it works (D24).
    [Fact]
    public void HealingPotion_Should_BeUsableFromInventory()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        foreach (var target in new[]
        {
            g.Hero,
            g.Ally
        }

        )
            g.Out(potion, target)[0].Sum(Heal).ShouldBe(10);
    }

    // OUT-08: When Hero uses the HealingPotion on the Orc, then every result is empty and nothing fails (wrong target, D30).
    [Fact]
    public void HealingPotion_Should_ReturnEmptyEffectsAgainstEnemies()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        var before = g.Snapshot();
        Ok(g.Effects.CanConsume(potion.Id)).ShouldBeTrue();
        g.Out(potion)[0].Items.ShouldBeEmpty();
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, potion.Id, g.Orc.Id));
        g.Snapshot().ShouldBe(before);
    }

    // USE-27: When Hero uses the HealingPotion on the Orc, then every result is empty, nothing fails, and the potion is unchanged (same as OUT-08).
    [Fact]
    public void WrongTargetPotionUse_Should_NotConsumePotion()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        var before = g.Snapshot();
        Ok(g.Effects.CanConsume(potion.Id)).ShouldBeTrue();
        g.Out(potion)[0].Items.ShouldBeEmpty();
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, potion.Id, g.Orc.Id));
        g.Snapshot().ShouldBe(before);
    }

    // USE-43: Given CanConsume returns true for HealingPotion, when Hero attempts to use it on the Orc, then Outgoing succeeds with empty effects and the caller does not call Consume. A successful eligibility check does not authorize payment for a wrong-target or no-matching-Use action (D29, D30).
    [Fact]
    public void EmptyEffects_Should_NotTriggerConsumptionDespiteEligibility()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        var before = g.Snapshot();
        Ok(g.Effects.CanConsume(potion.Id)).ShouldBeTrue();
        g.Out(potion)[0].Items.ShouldBeEmpty();
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, potion.Id, g.Orc.Id));
        g.Snapshot().ShouldBe(before);
    }

    // OUT-09: Given Hero wears RingOfFire and has Fists equipped, when Hero attacks the Orc, then the result is `Physical −2, Fire −4`.
    [Fact]
    public void Fists_Should_CombinePhysicalAndRingFireDamage()
    {
        var g = new Game();
        var fists = g.Own("Fists", MainHand);
        g.Own("RingOfFire", RingLeft);
        var hit = g.Out(fists)[0];
        hit.Sum(Physical).ShouldBe(-2);
        hit.Sum(Fire).ShouldBe(-4);
    }

    // RNG-04: Given Fists, then the result is `Physical −2, Fire −4`.
    [Fact]
    public void RingOfFire_Should_AddFireToFists()
    {
        var g = new Game();
        var fists = g.Own("Fists", MainHand);
        g.Own("RingOfFire", RingLeft);
        var hit = g.Out(fists)[0];
        hit.Sum(Physical).ShouldBe(-2);
        hit.Sum(Fire).ShouldBe(-4);
    }

    // OUT-10: When Outgoing is called with an unknown or physically removed instrument Id, then it fails (D5).
    [Fact]
    public void Outgoing_Should_RejectUnresolvableInstruments()
    {
        var g = new Game();
        foreach (var id in new[]
        {
            999,
            g.Make("Longsword").Id
        }

        )
        {
            g.Repo.Items.Remove(id);
            var before = g.Snapshot();
            Fail(g.Effects.Outgoing(g.Hero.Id, id, g.Orc.Id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // OUT-12: Given Fireball is Connected to Hero, when Hero casts it at the Orc, then the result is `Fire −6`.
    [Fact]
    public void Fireball_Should_BeUsableWhenConnected()
    {
        var g = new Game();
        g.Out(g.Own("Fireball"))[0].Sum(Fire).ShouldBe(-6);
    }

    // OUT-13: Given secondaries that are null or empty, then only the primary result is returned.
    [Fact]
    public void Outgoing_Should_ReturnOnlyPrimaryWithoutSecondaries()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        foreach (var secondary in new int[]? []
        {
            null,
            Array.Empty<int>()
        }

        )
            Ok(g.Effects.Outgoing(g.Hero.Id, sword.Id, g.Orc.Id, secondary)).Count.ShouldBe(1);
    }

    // OUT-16: When Hero attacks with the PoisonDagger, then the result has `Physical −4 (Duration 0)` and `Poison −2 (Duration 3)`.
    [Fact]
    public void PoisonDagger_Should_ProduceImmediatePhysicalAndTimedPoisonItems()
    {
        var g = new Game();
        var weapon = g.Own("PoisonDagger", MainHand);
        var hit = g.Out(weapon)[0];
        hit.Items.Count.ShouldBe(2);
        hit.Items.ShouldContain(x => x.EffectTypeId == Physical && x.Amount == -4 && x.Duration == 0);
        hit.Items.ShouldContain(x => x.EffectTypeId == Poison && x.Amount == -2 && x.Duration == 3);
    }

    // OUT-18: Given RingOfFire, when Hero attacks **himself** with the Longsword, then the result is `Physical −10` only (the ring is Enemies only).
    [Fact]
    public void RingOfFire_Should_NotAddDamageToSelfAttacks()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        var hit = g.Out(weapon, g.Hero)[0];
        hit.Sum(Physical).ShouldBe(-10);
        hit.Sum(Fire).ShouldBe(0);
    }

    // OUT-19: When sourceId or primaryTargetId does not identify a component in ComponentRepository (separate cases), then the call fails.
    [Fact]
    public void Outgoing_Should_RejectUnknownSourceAndTarget()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        foreach (var(source, instrument, target)in new[]
        {
            (999, weapon.Id, g.Orc.Id),
            (g.Hero.Id, 999, g.Orc.Id),
            (g.Hero.Id, weapon.Id, 999)
        }

        )
        {
            var before = g.Snapshot();
            Fail(g.Effects.Outgoing(source, instrument, target));
            g.Snapshot().ShouldBe(before);
        }
    }

    // RSL-03: Given Outgoing is called with an unknown sourceId, instrumentId or primaryTargetId (separate cases), then it returns a failed `Result<List<EffectResult>>`, `.Failed(...)` is true, no validation exception escapes, and component state is unchanged (A5, OUT-10, OUT-19).
    [Fact]
    public void InvalidOutgoing_Should_ReturnFailuresWithoutExceptionsOrMutation()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        foreach (var(source, instrument, target)in new[]
        {
            (999, weapon.Id, g.Orc.Id),
            (g.Hero.Id, 999, g.Orc.Id),
            (g.Hero.Id, weapon.Id, 999)
        }

        )
        {
            var before = g.Snapshot();
            Fail(g.Effects.Outgoing(source, instrument, target));
            g.Snapshot().ShouldBe(before);
        }
    }

    // OUT-20: When Hero uses an instrument that Hero doesn't own (the Ally's sword), then the call fails.
    [Fact]
    public void Outgoing_Should_RejectUnownedInstruments()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand, g.Ally);
        var before = g.Snapshot();
        Fail(g.Effects.Outgoing(g.Hero.Id, weapon.Id, g.Orc.Id));
        g.Snapshot().ShouldBe(before);
    }

    // OUT-21: When Hero uses an instrument whose category is `Usable = No` (RingOfFire, PlateArmour, Cleave1), then the call fails.
    [Fact]
    public void Outgoing_Should_RejectUnusableCategories()
    {
        var g = new Game();
        foreach (var(name, slot)in new[]
        {
            ("RingOfFire", RingLeft),
            ("PlateArmour", Body),
            ("Cleave1", 0),
            ("Ruby", 0)
        }

        )
        {
            var item = g.Own(name, slot);
            var before = g.Snapshot();
            Fail(g.Effects.Outgoing(g.Hero.Id, item.Id, g.Orc.Id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // USE-01: Given an instrument with `Usable = No` (RingOfFire, PlateArmour, Cleave1, Ruby), when Hero uses it, then Outgoing fails.
    [Fact]
    public void UnusableCategories_Should_RejectOutgoing()
    {
        var g = new Game();
        foreach (var(name, slot)in new[]
        {
            ("RingOfFire", RingLeft),
            ("PlateArmour", Body),
            ("Cleave1", 0),
            ("Ruby", 0)
        }

        )
        {
            var item = g.Own(name, slot);
            var before = g.Snapshot();
            Fail(g.Effects.Outgoing(g.Hero.Id, item.Id, g.Orc.Id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // OUT-22: Given the Longsword is in Hero's **Inventory** (equipable, not equipped), when Hero uses it, then the call fails (D24).
    [Fact]
    public void EquipableItems_Should_NotBeUsableFromInventory()
    {
        var g = new Game();
        var sword = g.Own("Longsword");
        var before = g.Snapshot();
        Fail(g.Effects.Outgoing(g.Hero.Id, sword.Id, g.Orc.Id));
        g.Snapshot().ShouldBe(before);
    }

    // OUT-23: Given Fireball is **not Connected** to Hero, when Hero casts it, then the call fails.
    [Fact]
    public void Fireball_Should_NotBeUsableWithoutConnection()
    {
        var g = new Game();
        var spell = g.Make("Fireball");
        var before = g.Snapshot();
        Fail(g.Effects.Outgoing(g.Hero.Id, spell.Id, g.Orc.Id));
        g.Snapshot().ShouldBe(before);
    }

    // OUT-24: Given a source, instrument, primary target or secondary target is a retained Deleted component (separate cases), when Outgoing is called for a new action, then it returns a failed Result without changing repository records, relations, stats or stored effects. Reading Deleted origins for already-pending effects does not make them available for new actions (D39, A5).
    [Fact]
    public void DeletedComponents_Should_NotJoinNewActions()
    {
        for (var endpoint = 0; endpoint < 4; endpoint++)
        {
            var g = new Game();
            var sword = g.Own("Longsword", MainHand);
            new[]
            {
                g.Hero,
                sword,
                g.Orc,
                g.Goblin1
            }[endpoint].State = ComponentState.Deleted;
            var before = g.Snapshot();
            Fail(g.Effects.Outgoing(g.Hero.Id, sword.Id, g.Orc.Id, new[] { g.Goblin1.Id }));
            g.Snapshot().ShouldBe(before);
        }
    }

    // RNG-01: Given the Longsword, then the Orc gets `Physical −10` and `Fire −4 (InstrumentId RingOfFire)`. `Sum(HP) = −14`, `Sum(Fire) = −4` and `Sum(Physical) = −10`.
    [Fact]
    public void RingOfFire_Should_AddFireToLongsword()
    {
        var g = new Game();
        var weapon = g.Own("Longsword", MainHand);
        var ring = g.Own("RingOfFire", RingLeft);
        var hit = g.Out(weapon)[0];
        hit.SumStat(HP, g.World).ShouldBe(-14);
        hit.Sum(Fire).ShouldBe(-4);
        hit.Sum(Physical).ShouldBe(-10);
        hit.Items.ShouldContain(x => x.InstrumentId == ring.Id && x.Amount == -4);
    }

    // RNG-02: Given the Shortbow, then the result is `Physical −8, Fire −4`.
    [Fact]
    public void RingOfFire_Should_AddFireToShortbow()
    {
        var g = new Game();
        var weapon = g.Own("Shortbow", MainHand);
        g.Own("RingOfFire", RingLeft);
        var hit = g.Out(weapon)[0];
        hit.Sum(Physical).ShouldBe(-8);
        hit.Sum(Fire).ShouldBe(-4);
    }

    // RNG-03: Given Fireball, then the result is `Fire −6, Fire −4` with `Sum(Fire) = −10`.
    [Fact]
    public void RingOfFire_Should_AddSeparateFireballContribution()
    {
        var g = new Game();
        var spell = g.Own("Fireball");
        g.Own("RingOfFire", RingLeft);
        var hit = g.Out(spell)[0];
        hit.Items.Count.ShouldBe(2);
        hit.Sum(Fire).ShouldBe(-10);
    }

    // RNG-05: Given a second RingOfFire in RingRight, then there are two fire items and `Sum(Fire) = −8`.
    [Fact]
    public void TwoRingsOfFire_Should_StackSeparateContributions()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        g.Own("RingOfFire", RingRight);
        var hit = g.Out(sword)[0];
        hit.Sum(Fire).ShouldBe(-8);
        hit.Items.Count(x => x.EffectTypeId == Fire).ShouldBe(2);
    }

    // RNG-06: Given the ring is moved from Inventory to Equipped, then the next attack has fire. After it is unequipped, the next attack has no fire.
    [Fact]
    public void RingOfFire_Should_ApplyAfterEquipping()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var ring = g.Own("RingOfFire");
        g.Out(sword)[0].Sum(Fire).ShouldBe(0);
        Ok(g.Relations.Equip(g.Hero.Id, ring.Id, RingLeft));
        g.Out(sword)[0].Sum(Fire).ShouldBe(-4);
        Ok(g.Relations.Unequip(g.Hero.Id, ring.Id));
        g.Out(sword)[0].Sum(Fire).ShouldBe(0);
    }

    // RNG-07: Given the ring is in Inventory, then there is no fire.
    [Fact]
    public void RingOfFire_Should_NotApplyWhileStoredInInventory()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire");
        g.Out(sword)[0].Sum(Fire).ShouldBe(0);
    }

    // RNG-08: Given the HealingPotion on Ally or on Hero, then nobody gets fire.
    [Fact]
    public void RingOfFire_Should_NotDamageHealingTargets()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        g.Own("RingOfFire", RingLeft);
        foreach (var target in new[]
        {
            g.Hero,
            g.Ally
        }

        )
            g.Out(potion, target)[0].Sum(Fire).ShouldBe(0);
    }

    // RNG-09: Given Hero attacks Ally with the Longsword, then the result is `Physical −10` only.
    [Fact]
    public void RingOfFire_Should_NotDamageAllies()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        var hit = g.Out(sword, g.Ally)[0];
        hit.Sum(Physical).ShouldBe(-10);
        hit.Sum(Fire).ShouldBe(0);
    }

    // RNG-10: Given Ally wears RingOfFire, then Hero's attacks get no fire from it.
    [Fact]
    public void AllyRingOfFire_Should_NotModifyHeroAttacks()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft, g.Ally);
        g.Out(sword)[0].Sum(Fire).ShouldBe(0);
    }

    // RNG-11: Given the Orc wears RingOfFire, when the Orc is hit, then the ring adds nothing.
    [Fact]
    public void RingOfFire_Should_RequireItsUseModifierToMatch()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft, g.Orc);
        g.In(g.Out(sword)[0])[0].Sum(Fire).ShouldBe(0);
    }

    // RNG-12: Given the Orc wears WardRing (50% fire), when the full pipeline runs, then the Orc loses `10 + 2` and ends at HP 88.
    [Fact]
    public void WardRing_Should_HalveIncomingRingFire()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("RingOfFire", RingLeft);
        g.Own("WardRing", RingLeft, g.Orc);
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, sword.Id, g.Orc.Id));
        Current(g.Orc).ShouldBe(88);
    }

    // RNG-14: Given the StunGrenade at the Orc, then the result is `Stun +2` and `Fire −4` (it's an attack on an enemy, D4).
    [Fact]
    public void RingOfFire_Should_AddFireToStunGrenade()
    {
        var g = new Game();
        var grenade = g.Own("StunGrenade");
        g.Own("RingOfFire", RingLeft);
        var hit = g.Out(grenade)[0];
        hit.Sum(StunEffect).ShouldBe(2);
        hit.Sum(Fire).ShouldBe(-4);
    }
}
