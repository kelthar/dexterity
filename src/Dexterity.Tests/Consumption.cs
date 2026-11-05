using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // USE-02: Given a category created without a Usable value, then its Usable is `No` (A23).
    [Fact]
    public void ComponentCategory_Should_DefaultToUnusable()
    {
        new ComponentCategory().Usable.ShouldBe(Usable.No);
    }

    // USE-03: Given a category CursedBlade (`Usable = No`) under Sword (`Eternal`), when Hero uses a CursedBlade, then Outgoing fails (A22: not inherited).
    [Fact]
    public void CursedBlade_Should_NotBeUsable()
    {
        var g = new Game();
        g.Category(22, "CursedBlade", Sword, true);
        var blade = g.Own("Longsword", MainHand);
        blade.CategoryId = 22;
        var before = g.Snapshot();
        Fail(g.Effects.Outgoing(g.Hero.Id, blade.Id, g.Orc.Id));
        g.Snapshot().ShouldBe(before);
    }

    // USE-04: Given the Longsword (Eternal), when Hero completes the full use pipeline 1 000 times, then the Longsword is still Active in ComponentRepository and Equipped, and every outgoing result is `Physical −10` (A27).
    [Fact]
    public void EternalLongsword_Should_SurviveRepeatedCompleteActions()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        for (var i = 0; i < 1000; i++)
            Ok(Pipeline.Run(g.Effects, g.Hero.Id, sword.Id, g.Orc.Id))[0].Sum(Physical).ShouldBe(-10);
        Ok(g.Repo.Get(sword.Id)).State.ShouldBe(ComponentState.Active);
        g.Relation(sword).Type.ShouldBe(RelationType.Equipped);
    }

    // USE-05: Given the FireWand is equipped, then Hero's MANA is 55/55 (from its passive).
    [Fact]
    public void EquippedFireWand_Should_IncreaseMaximumMana()
    {
        var g = new Game();
        var wand = g.Own("FireWand");
        Ok(g.Relations.Equip(g.Hero.Id, wand.Id, MainHand));
        Current(g.Hero, Mana).ShouldBe(55);
        g.Hero.Stats[Mana].ModifiedMax.ShouldBe(55);
    }

    // USE-06: When Hero attacks the Orc with the FireWand, then the result is `Fire −5`, and CHARGES is still 3 (Outgoing is side-effect free, D29).
    [Fact]
    public void FireWandOutgoing_Should_NotSpendCharges()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        var before = g.Snapshot();
        for (var i = 0; i < 5; i++)
            g.Out(wand)[0].Sum(Fire).ShouldBe(-5);
        Current(wand, Charges).ShouldBe(3);
        g.Snapshot().ShouldBe(before);
    }

    // USE-10: Given the FireWand at 3/3, when Outgoing is called 5 times without `Consume`, then all 5 calls succeed and CHARGES is still 3 (paying is the caller's job).
    [Fact]
    public void RepeatedOutgoing_Should_NotSpendCharges()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        var before = g.Snapshot();
        for (var i = 0; i < 5; i++)
            g.Out(wand)[0].Sum(Fire).ShouldBe(-5);
        Current(wand, Charges).ShouldBe(3);
        g.Snapshot().ShouldBe(before);
    }

    // USE-07: Given USE-06, when all Incoming and Apply calls for the action succeed and `Consume(FireWand)` is called, then CHARGES is 2/3 and the wand remains Active.
    [Fact]
    public void Consume_Should_SpendOneChargeAfterResolution()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        foreach (var hit in g.In(g.Out(wand)[0]))
            Ok(g.Effects.Apply(hit.TargetId!.Value, hit));
        Current(wand, Charges).ShouldBe(3);
        Ok(g.Effects.Consume(wand.Id));
        Current(wand, Charges).ShouldBe(2);
        wand.Stats[Charges].Max.ShouldBe(3);
        wand.State.ShouldBe(ComponentState.Active);
    }

    // USE-08: Given CHARGES 1/3 and no pending-effect dependencies on FireWand, when Consume is called after resolution, then the FireWand is marked Deleted and physically removed from ComponentRepository. Its relation to Hero is gone, MainHand is free, and Hero's MANA is 50/50 without a manual Recalculate (D28, D39, A26, D22).
    [Fact]
    public void LastCharge_Should_DeleteUnreferencedFireWand()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        wand.Stats[Charges].Current = 1;
        g.Hero.Stats[Mana].ModifiedMax = 55;
        g.Hero.Stats[Mana].Bonus = 5;
        g.Hero.Stats[Mana].Current = 55;
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, wand.Id, g.Orc.Id));
        wand.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldNotContainKey(wand.Id);
        g.Repo.Relations.ShouldNotContain(x => x.TargetId == wand.Id);
        Current(g.Hero, Mana).ShouldBe(50);
    }

    // USE-09: Given the FireWand is Deleted but retained for pending effects, or has already been physically removed (separate cases), when Hero tries a new use, then CanConsume and direct Outgoing fail. A retained Deleted record cannot be reused (D39).
    [Fact]
    public void DeletedFireWand_Should_RejectNewUse()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        wand.State = ComponentState.Deleted;
        g.Store(g.Hit(Fire, -1, 2, instrument: wand));
        foreach (var retained in new[]
        {
            true,
            false
        }

        )
        {
            if (!retained)
                g.Repo.Items.Remove(wand.Id);
            var before = g.Snapshot();
            Fail(g.Effects.CanConsume(wand.Id));
            Fail(g.Effects.Outgoing(g.Hero.Id, wand.Id, g.Orc.Id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // USE-11: Given the HealingScroll (2/2, not equipable) in Hero's Inventory, when Hero uses it on himself, then the result is `Heal +5` (D24). After two successfully resolved uses, each followed by Consume, it is removed from Hero's inventory and physically removed from ComponentRepository because it has no pending-effect dependencies.
    [Fact]
    public void HealingScroll_Should_HealItsOwner()
    {
        var g = new Game();
        var scroll = g.Own("HealingScroll");
        for (var i = 0; i < 2; i++)
            Ok(Pipeline.Run(g.Effects, g.Hero.Id, scroll.Id, g.Hero.Id))[0].Sum(Heal).ShouldBe(5);
        scroll.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldNotContainKey(scroll.Id);
        g.Repo.Relations.ShouldNotContain(x => x.TargetId == scroll.Id);
    }

    // USE-12: Given the FireWand at 1/3, when `Recharge +5` is applied to the FireWand, then CHARGES is 3/3 (clamped, D18).
    [Fact]
    public void Recharge_Should_ClampChargesAtMaximum()
    {
        var g = new Game();
        var wand = g.Make("FireWand");
        wand.Stats[Charges].Current = 1;
        Ok(g.Effects.Apply(wand.Id, g.Hit(Recharge, 5, target: wand)));
        Current(wand, Charges).ShouldBe(3);
    }

    // USE-13: Given the FireWand is drained to 0/3 by applying `Recharge −3`, then it stays Active in ComponentRepository and Equipped. CanConsume returns successful false; direct Outgoing doesn't fail but returns an empty result, and Consume on it fails (D37, A25).
    [Fact]
    public void DrainingCharges_Should_NotDeleteAnActiveItem()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        Ok(g.Effects.Apply(wand.Id, g.Hit(Recharge, -3, target: wand)));
        wand.State.ShouldBe(ComponentState.Active);
        g.Relation(wand).Type.ShouldBe(RelationType.Equipped);
        Ok(g.Effects.CanConsume(wand.Id)).ShouldBeFalse();
        g.Out(wand)[0].Items.ShouldBeEmpty();
        var before = g.Snapshot();
        Fail(g.Effects.Consume(wand.Id));
        g.Snapshot().ShouldBe(before);
        Ok(g.Effects.Apply(wand.Id, g.Hit(Recharge, 1, target: wand)));
        Ok(g.Effects.CanConsume(wand.Id)).ShouldBeTrue();
        g.Out(wand)[0].Sum(Fire).ShouldBe(-5);
    }

    // USE-14: Given the FireWand at 0/3, when `Recharge +1` is applied, then it can be used again.
    [Fact]
    public void RechargingAnEmptyWand_Should_EnableUseAgain()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        Ok(g.Effects.Apply(wand.Id, g.Hit(Recharge, -3, target: wand)));
        wand.State.ShouldBe(ComponentState.Active);
        g.Relation(wand).Type.ShouldBe(RelationType.Equipped);
        Ok(g.Effects.CanConsume(wand.Id)).ShouldBeFalse();
        g.Out(wand)[0].Items.ShouldBeEmpty();
        var before = g.Snapshot();
        Fail(g.Effects.Consume(wand.Id));
        g.Snapshot().ShouldBe(before);
        Ok(g.Effects.Apply(wand.Id, g.Hit(Recharge, 1, target: wand)));
        Ok(g.Effects.CanConsume(wand.Id)).ShouldBeTrue();
        g.Out(wand)[0].Sum(Fire).ShouldBe(-5);
    }

    // USE-15: Given a component created in a Charges category with CHARGES 0, when it is used, then the call doesn't fail but returns an empty result (D37).
    [Fact]
    public void ZeroChargeUse_Should_ReturnEmptyEffectsSuccessfully()
    {
        var g = new Game();
        var wand = Ok(g.Repo.Create(Item, Wand));
        Ok(g.Relations.Add(g.Hero.Id, wand.Id));
        Ok(g.Relations.Equip(g.Hero.Id, wand.Id, MainHand));
        g.Out(wand)[0].Items.ShouldBeEmpty();
        Current(wand, Charges).ShouldBe(0);
    }

    // USE-16: Given the HealingPotion in Hero's Inventory with no pending-effect dependencies, when Hero checks CanConsume and resolves the use on himself, then the result is `Heal +10` and the potion remains Active until Consume. Consume marks it Deleted, removes it from Hero's inventory and physically removes the unneeded record from ComponentRepository.
    [Fact]
    public void HealingPotion_Should_RemainActiveUntilConsume()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        Ok(g.Effects.CanConsume(potion.Id)).ShouldBeTrue();
        foreach (var hit in g.In(g.Out(potion, g.Hero)[0]))
            Ok(g.Effects.Apply(hit.TargetId!.Value, hit));
        potion.State.ShouldBe(ComponentState.Active);
        g.Relation(potion).Type.ShouldBe(RelationType.Inventory);
        Ok(g.Effects.Consume(potion.Id));
        potion.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldNotContainKey(potion.Id);
        g.Repo.Relations.ShouldNotContain(x => x.TargetId == potion.Id);
    }

    // USE-17: Given the ThrowingKnife equipped in MainHand with no pending-effect dependencies, when Consume is called after all attack results are applied, then the knife is marked Deleted and physically removed, and MainHand is empty.
    [Fact]
    public void ConsumedKnife_Should_BeRemovedAfterResolution()
    {
        var g = new Game();
        var knife = g.Own("ThrowingKnife", MainHand);
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, knife.Id, g.Orc.Id));
        knife.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldNotContainKey(knife.Id);
        g.Repo.Relations.ShouldNotContain(x => x.SlotIds.Contains(MainHand));
    }

    // USE-18: Given KnifeRuby is connected to the ThrowingKnife and has no other parents, when the knife is consumed, then KnifeRuby is also marked Deleted (unconfirmed cascade default, A35). Each record is physically removed only when its pending-effect dependencies are resolved (D39). Merely detaching or transferring the knife preserves Active KnifeRuby and their connection (A26).
    [Fact]
    public void ConsumedKnife_Should_CascadeDeletionToItsRuby()
    {
        var g = new Game();
        var knife = g.Own("ThrowingKnife", MainHand);
        var ruby = g.Make("KnifeRuby");
        g.Link(knife, ruby);
        Ok(g.Relations.Remove(g.Hero.Id, knife.Id));
        knife.State.ShouldBe(ComponentState.Active);
        ruby.State.ShouldBe(ComponentState.Active);
        g.Relation(ruby).ParentId.ShouldBe(knife.Id);
        Ok(g.Relations.Add(g.Hero.Id, knife.Id));
        Ok(g.Relations.Equip(g.Hero.Id, knife.Id, MainHand));
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, knife.Id, g.Orc.Id));
        ruby.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldNotContainKey(ruby.Id);
        g.Repo.Items.ShouldNotContainKey(knife.Id);
    }

    // USE-19: Given the FireBomb was used on the Orc and applied (`Fire −3` now, one EffectResult containing `Fire −1 × 2` stored), when the FireBomb is consumed, then its gameplay relations are removed but its State Deleted record remains in ComponentRepository. Two `Tick(Orc, 1)` calls with no Fire prevention still deal −1 each, resolving InstrumentId FireBomb through the repository. The bomb is retained after the first tick and physically removed after the second resolves its last pending effect. Stored results and tick outputs preserve SourceId Hero and InstrumentId FireBomb (D39).
    [Fact]
    public void ConsumedFireBomb_Should_RetainOriginWhileBurnsRemain()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, bomb.Id, g.Orc.Id));
        bomb.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldContainKey(bomb.Id);
        g.Repo.Relations.ShouldNotContain(x => x.TargetId == bomb.Id);
        Current(g.Orc).ShouldBe(97);
        g.Orc.Effects.Single().InstrumentId.ShouldBe(bomb.Id);
        var first = g.Tick().Single();
        first.InstrumentId.ShouldBe(bomb.Id);
        first.SourceId.ShouldBe(g.Hero.Id);
        first.Sum(Fire).ShouldBe(-1);
        g.Repo.Items.ShouldContainKey(bomb.Id);
        g.Tick().Single().InstrumentId.ShouldBe(bomb.Id);
        Current(g.Orc).ShouldBe(95);
        g.Repo.Items.ShouldNotContainKey(bomb.Id);
    }

    // USE-20: Given the Orc wears ArrowWard (IncludedTags `[ranged]`), when Hero checks CanConsume, throws ThrowingKnife, resolves Incoming and Apply, and only then calls Consume, then Incoming resolves InstrumentId ThrowingKnife from its still-Active repository record and halves the hit to −3. The knife stays Active and Equipped until the action is resolved; afterwards it is removed from gameplay and, with no pending dependencies, physically removed (D29, D39).
    [Fact]
    public void ThrowingKnife_Should_ResolveIncomingBeforeConsumption()
    {
        var g = new Game();
        var knife = g.Own("ThrowingKnife", MainHand);
        g.Own("ArrowWard", Neck, g.Orc);
        Ok(g.Effects.CanConsume(knife.Id)).ShouldBeTrue();
        var result = g.In(g.Out(knife)[0])[0];
        result.Sum(Physical).ShouldBe(-3);
        Ok(g.Effects.Apply(g.Orc.Id, result));
        knife.State.ShouldBe(ComponentState.Active);
        g.Relation(knife).Type.ShouldBe(RelationType.Equipped);
        Current(g.Orc).ShouldBe(97);
        Ok(g.Effects.Consume(knife.Id));
        g.Repo.Items.ShouldNotContainKey(knife.Id);
    }

    // USE-21: `Consume` on a component with `Usable = No` fails and changes nothing.
    [Fact]
    public void Consume_Should_RejectUnusableItemsWithoutMutation()
    {
        var g = new Game();
        var ring = g.Own("RingOfFire", RingLeft);
        var before = g.Snapshot();
        Fail(g.Effects.Consume(ring.Id));
        g.Snapshot().ShouldBe(before);
    }

    // USE-22: Consume on a retained Deleted component or a physically removed Id (separate cases) fails without spending charges or changing any record.
    [Fact]
    public void Consume_Should_RejectPreviouslyDeletedItems()
    {
        var g = new Game();
        var potion = g.Make("HealingPotion");
        potion.State = ComponentState.Deleted;
        foreach (var id in new[]
        {
            potion.Id,
            999
        }

        )
        {
            var before = g.Snapshot();
            Fail(g.Effects.Consume(id));
            g.Snapshot().ShouldBe(before);
        }

        g.Repo.Items.Remove(potion.Id);
        Fail(g.Effects.Consume(potion.Id));
    }

    // USE-23: Consume on an unknown component Id fails.
    [Fact]
    public void Consume_Should_RejectUnknownComponents()
    {
        var g = new Game();
        var potion = g.Make("HealingPotion");
        potion.State = ComponentState.Deleted;
        foreach (var id in new[]
        {
            potion.Id,
            999
        }

        )
        {
            var before = g.Snapshot();
            Fail(g.Effects.Consume(id));
            g.Snapshot().ShouldBe(before);
        }

        g.Repo.Items.Remove(potion.Id);
        Fail(g.Effects.Consume(potion.Id));
    }

    // USE-24: Given a category with `Usable = Charges` and no charges stat named in the world, then the build fails (A24).
    [Fact]
    public void ChargesCategories_Should_RequireANamedChargesStat()
    {
        var g = new Game();
        g.World.ChargesStatId = null;
        Fail(g.Builder.Build(g.World));
    }

    // USE-25: Given the named charges stat isn't a plain Pool (for example a Fixed stat, or a Counter, which would count down every tick), then the build fails.
    [Fact]
    public void ChargesStat_Should_BeAPool()
    {
        var g = new Game();
        foreach (var type in new[]
        {
            StatKind.Fixed,
            StatKind.Counter
        }

        )
        {
            g.World.Stats.Single(x => x.Id == Charges).Type = type;
            Fail(g.Builder.Build(g.World));
        }
    }

    // USE-26: When `Create(Skill, Scroll)` is called (Skill has no CHARGES stat), then it fails.
    [Fact]
    public void ChargedComponents_Should_RequireTheirChargesStat()
    {
        var g = new Game();
        var before = g.Snapshot();
        Fail(g.Repo.Create(Skill, Scroll));
        g.Snapshot().ShouldBe(before);
    }

    // USE-28: When Hero uses the FireWand (Enemies) on Ally, then the call doesn't fail. It returns an empty result, and CHARGES is unchanged.
    [Fact]
    public void WrongTargetWandUse_Should_ReturnEmptyEffectsSuccessfully()
    {
        var g = new Game();
        foreach (var(name, slot)in new[]
        {
            ("FireWand", MainHand),
            ("Fireball", 0)
        }

        )
        {
            var item = g.Own(name, slot);
            var before = g.Snapshot();
            g.Out(item, g.Ally)[0].Items.ShouldBeEmpty();
            g.Snapshot().ShouldBe(before);
        }
    }

    // USE-29: When Hero casts Fireball (Enemies) at Ally, then the call doesn't fail and returns an empty result.
    [Fact]
    public void WrongTargetFireball_Should_ReturnEmptyEffectsSuccessfully()
    {
        var g = new Game();
        foreach (var(name, slot)in new[]
        {
            ("FireWand", MainHand),
            ("Fireball", 0)
        }

        )
        {
            var item = g.Own(name, slot);
            var before = g.Snapshot();
            g.Out(item, g.Ally)[0].Items.ShouldBeEmpty();
            g.Snapshot().ShouldBe(before);
        }
    }

    // USE-30: When Hero attacks Ally with the Longsword (RelationKind None), then the result is `Physical −10`.
    [Fact]
    public void UnrestrictedLongsword_Should_DamageAllies()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Out(sword, g.Ally)[0].Sum(Physical).ShouldBe(-10);
    }

    // USE-31: When Hero uses HolyWater on Ally, then the result is `Heal +5` only. On the Orc, it is `Fire −5` only (one matching Use modifier is enough).
    [Fact]
    public void HolyWater_Should_SelectFriendlyHealingModifiers()
    {
        var g = new Game();
        var water = g.Own("HolyWater");
        var ally = g.Out(water, g.Ally)[0];
        ally.Sum(Heal).ShouldBe(5);
        ally.Sum(Fire).ShouldBe(0);
        var enemy = g.Out(water)[0];
        enemy.Sum(Fire).ShouldBe(-5);
        enemy.Sum(Heal).ShouldBe(0);
    }

    // USE-32: Given a usable instrument without Use modifiers, when it is used, then every result is empty and nothing fails (no Use modifier matches, D30).
    [Fact]
    public void WrongTargetUse_Should_SkipAllPassiveModifiers()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData.Clear();
        g.Own("RingOfFire", RingLeft);
        var before = g.Snapshot();
        g.Out(sword)[0].Items.ShouldBeEmpty();
        g.Snapshot().ShouldBe(before);
    }

    // USE-33: Given the FireWand at the Orc with Ally as a secondary, then the use is valid, and Ally's result is empty (only the primary target decides).
    [Fact]
    public void SecondaryMismatch_Should_NotInvalidatePrimaryUse()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        var results = g.Out(wand, g.Orc, g.Ally);
        results[0].Sum(Fire).ShouldBe(-5);
        results[1].Items.ShouldBeEmpty();
    }

    // USE-34: Given Hero wears RingOfFire, when Hero uses the HealingPotion on the Orc, then the Orc gets nothing. The ring is skipped too, because the whole action does nothing (D30).
    [Fact]
    public void RingOfFire_Should_NotOverridePotionUseEligibility()
    {
        var g = new Game();
        var potion = g.Own("HealingPotion");
        g.Own("RingOfFire", RingLeft);
        g.Out(potion)[0].Items.ShouldBeEmpty();
    }

    // USE-35: Given Hero wears RingOfFire and has Fists equipped, when Hero attacks Ally, then the result is `Physical −2` (Fists have RelationKind None), and the ring adds nothing (Enemies only).
    [Fact]
    public void RingOfFire_Should_NotModifyFriendlyFistsAttacks()
    {
        var g = new Game();
        var fists = g.Own("Fists", MainHand);
        g.Own("RingOfFire", RingLeft);
        var result = g.Out(fists, g.Ally)[0];
        result.Sum(Physical).ShouldBe(-2);
        result.Sum(Fire).ShouldBe(0);
    }

    // USE-36: Given an instrument whose matching Use modifier creates no item (Decrease value 0, A15) and Hero wears RingOfFire, when Hero uses it on the Orc, then the use is valid, so the ring still adds `Fire −4`.
    [Fact]
    public void MatchingUseModifier_Should_EnablePassiveFire()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData[0].Data["value"] = 0;
        g.Own("RingOfFire", RingLeft);
        g.Out(sword)[0].Sum(Fire).ShouldBe(-4);
    }

    // USE-37: Given the FireWand at 0/3 and Hero wears RingOfFire, when Hero uses the wand on the Orc, then the Orc gets nothing. The ring is skipped too, because the whole action does nothing (D37).
    [Fact]
    public void ZeroCharges_Should_DisablePassiveFire()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        wand.Stats[Charges].Current = 0;
        g.Own("RingOfFire", RingLeft);
        g.Out(wand)[0].Items.ShouldBeEmpty();
    }

    // USE-38: Given the Orc has an Incoming PreventPercent Fire 50 with IncludedTags `[grenade]`, when FireBomb is used, its timed result is stored and Consume marks the bomb Deleted, then each later tick resolves InstrumentId FireBomb through ComponentRepository, matches its composed tags and resolves the stored `Fire −1` to −0.5. The retained record, rather than an embedded component copy, supplies the origin data until the final tick completes (D16, D33, D38, D39).
    [Fact]
    public void DeletedFireBomb_Should_ProvideTagsForPendingTicks()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        var ward = Ward(g, "preventpercent", Fire, 50, "percent");
        ward.ModifierData[0].IncludedTags.Add("grenade");
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, bomb.Id, g.Orc.Id));
        bomb.State.ShouldBe(ComponentState.Deleted);
        g.Tick()[0].Sum(Fire).ShouldBe(-.5);
        g.Repo.Items.ShouldContainKey(bomb.Id);
        g.Tick()[0].Sum(Fire).ShouldBe(-.5);
        g.Repo.Items.ShouldNotContainKey(bomb.Id);
    }

    // USE-39: Given an Active Eternal Longsword, Consumed HealingPotion or Charges FireWand at 3/3 (separate cases), when CanConsume is called before use, then it returns a successful `Result<bool>` with Data true. State, charges, relations, slots, stats and pending effects remain unchanged; the check does not spend or reserve a use (D29).
    [Fact]
    public void CanConsume_Should_ReturnTrueForEligibleItems()
    {
        var g = new Game();
        foreach (var name in new[]
        {
            "Longsword",
            "HealingPotion",
            "FireWand"
        }

        )
        {
            var item = g.Make(name);
            var before = g.Snapshot();
            Ok(g.Effects.CanConsume(item.Id)).ShouldBeTrue();
            g.Snapshot().ShouldBe(before);
        }
    }

    // USE-40: Given Active FireWand at CHARGES 0/3 and Hero wears RingOfFire, when CanConsume is called, then it returns successful false. The caller treats the use as a successful empty action and skips modifier calculation, Incoming, Apply and Consume. The wand stays Active and Equipped; direct Outgoing also returns empty effects and skips passives (D37).
    [Fact]
    public void CanConsume_Should_ReturnFalseForEmptyCharges()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        wand.Stats[Charges].Current = 0;
        g.Own("RingOfFire", RingLeft);
        var before = g.Snapshot();
        Ok(g.Effects.CanConsume(wand.Id)).ShouldBeFalse();
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, wand.Id, g.Orc.Id)).ShouldBeEmpty();
        g.Out(wand)[0].Items.ShouldBeEmpty();
        g.Snapshot().ShouldBe(before);
    }

    // USE-41: Given an unknown or physically removed Id, a retained Deleted instrument, or an Active instrument with Usable = No (separate cases), when CanConsume is called, then it returns a failed `Result<bool>` without mutation. No action is executed (A5, D29).
    [Fact]
    public void CanConsume_Should_FailWithoutMutationForInvalidItems()
    {
        var g = new Game();
        var dead = g.Make("FireBomb");
        dead.State = ComponentState.Deleted;
        var ring = g.Make("RingOfFire");
        var removed = g.Make("Longsword");
        g.Repo.Items.Remove(removed.Id);
        foreach (var id in new[]
        {
            999,
            removed.Id,
            dead.Id,
            ring.Id
        }

        )
        {
            var before = g.Snapshot();
            Fail(g.Effects.CanConsume(id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // USE-44: Given Hero at HP 80 has RegenPotion and no Incoming modifiers, when the full pipeline runs on Hero, then its timed-only result has `Sum(HP) = 0` but contains `Heal +5 × 2`. Apply stores it before Consume marks the potion Deleted. The record remains available for two ticks, which heal to 85 and 90, then cleanup physically removes it. A timed-only action is not an empty action (D29, D39).
    [Fact]
    public void TimedOnlyEffects_Should_TriggerConsumptionDespiteZeroSum()
    {
        var g = new Game();
        g.Hero.Stats[HP].Current = 80;
        var potion = g.Own("RegenPotion");
        var outgoing = g.Out(potion, g.Hero)[0];
        outgoing.SumStat(HP, g.World).ShouldBe(0);
        outgoing.Items.ShouldNotBeEmpty();
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, potion.Id, g.Hero.Id));
        potion.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldContainKey(potion.Id);
        Current(g.Hero).ShouldBe(80);
        g.Tick(g.Hero);
        Current(g.Hero).ShouldBe(85);
        g.Tick(g.Hero);
        Current(g.Hero).ShouldBe(90);
        g.Repo.Items.ShouldNotContainKey(potion.Id);
    }

    // USE-45: Given the same consumed FireBomb is referenced by stored `Fire −1 × 1` on the Orc and `Fire −1 × 2` on Goblin1, when the Orc's last burn resolves, then the bomb remains Deleted in ComponentRepository because Goblin1 still needs it. Only after Goblin1's two ticks resolve does cleanup physically remove the bomb. Finishing one target does not imply all effects from that origin have resolved (D39).
    [Fact]
    public void Cleanup_Should_CheckPendingEffectsAcrossAllTargets()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        g.Store(g.Hit(Fire, -1, 1, instrument: bomb));
        g.Store(g.Hit(Fire, -1, 2, g.Goblin1, bomb));
        Ok(g.Effects.Consume(bomb.Id));
        g.Tick();
        g.Repo.Items.ShouldContainKey(bomb.Id);
        g.Tick(g.Goblin1);
        g.Repo.Items.ShouldContainKey(bomb.Id);
        g.Tick(g.Goblin1);
        g.Repo.Items.ShouldNotContainKey(bomb.Id);
    }

    // USE-46: Given consumed FireBomb has only `Fire −1 × 2` pending on an Orc with PreventFlat Fire 1, when two unit ticks run, then both ticks resolve to 0 while durations count down normally. The bomb remains after the first tick and is physically removed after the final tick even though neither tick changed HP. Completion depends on pending effects, not on non-zero damage (D39, A15).
    [Fact]
    public void FullyPreventedBurns_Should_CountDownAndReleaseOrigins()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        g.Store(g.Hit(Fire, -1, 2, instrument: bomb));
        Ward(g, "preventflat", Fire, 1, "amount");
        Ok(g.Effects.Consume(bomb.Id));
        g.Tick()[0].Sum(Fire).ShouldBe(0);
        g.Repo.Items.ShouldContainKey(bomb.Id);
        g.Tick()[0].Sum(Fire).ShouldBe(0);
        Current(g.Orc).ShouldBe(100);
        g.Repo.Items.ShouldNotContainKey(bomb.Id);
    }

    // USE-47: Given a FireWand variant at CHARGES 1/3 whose Use modifier creates `Fire −5 × 2`, when Hero resolves the action and then consumes it, then CHARGES becomes 0, State becomes Deleted, MainHand is freed and Hero's MANA returns to 50/50 immediately. The wand's record and ModifierData remain in ComponentRepository for its pending ticks but supply no new passive bonus. After the last tick resolves, the unneeded record is physically removed (D28, D39).
    [Fact]
    public void LastCharge_Should_ImmediatelyDetachAndRecalculateOwner()
    {
        var g = new Game();
        var wand = g.Own("FireWand");
        wand.Stats[Charges].Current = 1;
        wand.ModifierData[0].Duration = 2;
        Ok(g.Relations.Equip(g.Hero.Id, wand.Id, MainHand));
        Current(g.Hero, Mana).ShouldBe(55);
        Ok(Pipeline.Run(g.Effects, g.Hero.Id, wand.Id, g.Orc.Id));
        Current(wand, Charges).ShouldBe(0);
        wand.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldContainKey(wand.Id);
        wand.ModifierData.Count.ShouldBe(2);
        g.Repo.Relations.ShouldNotContain(x => x.TargetId == wand.Id);
        Current(g.Hero, Mana).ShouldBe(50);
        g.Tick();
        g.Repo.Items.ShouldContainKey(wand.Id);
        g.Tick();
        g.Repo.Items.ShouldNotContainKey(wand.Id);
    }
}
