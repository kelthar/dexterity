using NSubstitute;
using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public class RuntimeTests
{
    [Fact]
    public void DeletedOrigin_Should_RemainUntilResultTargetReferenceExpires()
    {
        var reference = "target";
        var g = new Game();
        var origin = g.Make("Ruby");
        origin.MetaData["origin"] = "preserved";
        origin.Tags.Add("preserved");
        var pending = g.Hit(Poison, -2, 1);
        var target = g.Orc;
        switch (reference)
        {
            case "target":
                pending.TargetId = origin.Id;
                break;
            case "source":
                pending.SourceId = origin.Id;
                break;
            case "instrument":
                pending.InstrumentId = origin.Id;
                break;
            case "itemSource":
                pending.Items[0] = pending.Items[0] with
                {
                    SourceId = origin.Id
                };
                break;
            case "itemInstrument":
                pending.Items[0] = pending.Items[0] with
                {
                    InstrumentId = origin.Id
                };
                break;
            case "owner":
                target = origin;
                pending.TargetId = origin.Id;
                break;
        }

        // A result-level target reference can retain a different record from its storage owner.
        // Cleanup must scan all identity fields, not infer references from storage location.
        target.Effects.Add(pending);
        Ok(g.Repo.Delete(origin.Id));
        Ok(g.Repo.Get(origin.Id)).ShouldBeSameAs(origin);
        origin.State.ShouldBe(ComponentState.Deleted);
        origin.MetaData["origin"].ShouldBe("preserved");
        origin.Tags.ShouldContain("preserved");
        if (reference == "target")
        {
            // Move a deliberately cross-owned report to its destination before resolving it.
            target.Effects.Remove(pending);
            origin.Effects.Add(pending);
            target = origin;
        }

        g.Tick(target);
        Fail(g.Repo.Get(origin.Id));
    }

    [Fact]
    public void DeletedOrigin_Should_RemainUntilResultSourceReferenceExpires()
    {
        var reference = "source";
        var g = new Game();
        var origin = g.Make("Ruby");
        origin.MetaData["origin"] = "preserved";
        origin.Tags.Add("preserved");
        var pending = g.Hit(Poison, -2, 1);
        var target = g.Orc;
        switch (reference)
        {
            case "target":
                pending.TargetId = origin.Id;
                break;
            case "source":
                pending.SourceId = origin.Id;
                break;
            case "instrument":
                pending.InstrumentId = origin.Id;
                break;
            case "itemSource":
                pending.Items[0] = pending.Items[0] with
                {
                    SourceId = origin.Id
                };
                break;
            case "itemInstrument":
                pending.Items[0] = pending.Items[0] with
                {
                    InstrumentId = origin.Id
                };
                break;
            case "owner":
                target = origin;
                pending.TargetId = origin.Id;
                break;
        }

        // A result-level target reference can retain a different record from its storage owner.
        // Cleanup must scan all identity fields, not infer references from storage location.
        target.Effects.Add(pending);
        Ok(g.Repo.Delete(origin.Id));
        Ok(g.Repo.Get(origin.Id)).ShouldBeSameAs(origin);
        origin.State.ShouldBe(ComponentState.Deleted);
        origin.MetaData["origin"].ShouldBe("preserved");
        origin.Tags.ShouldContain("preserved");
        if (reference == "target")
        {
            // Move a deliberately cross-owned report to its destination before resolving it.
            target.Effects.Remove(pending);
            origin.Effects.Add(pending);
            target = origin;
        }

        g.Tick(target);
        Fail(g.Repo.Get(origin.Id));
    }

    [Fact]
    public void DeletedOrigin_Should_RemainUntilResultInstrumentReferenceExpires()
    {
        var reference = "instrument";
        var g = new Game();
        var origin = g.Make("Ruby");
        origin.MetaData["origin"] = "preserved";
        origin.Tags.Add("preserved");
        var pending = g.Hit(Poison, -2, 1);
        var target = g.Orc;
        switch (reference)
        {
            case "target":
                pending.TargetId = origin.Id;
                break;
            case "source":
                pending.SourceId = origin.Id;
                break;
            case "instrument":
                pending.InstrumentId = origin.Id;
                break;
            case "itemSource":
                pending.Items[0] = pending.Items[0] with
                {
                    SourceId = origin.Id
                };
                break;
            case "itemInstrument":
                pending.Items[0] = pending.Items[0] with
                {
                    InstrumentId = origin.Id
                };
                break;
            case "owner":
                target = origin;
                pending.TargetId = origin.Id;
                break;
        }

        // A result-level target reference can retain a different record from its storage owner.
        // Cleanup must scan all identity fields, not infer references from storage location.
        target.Effects.Add(pending);
        Ok(g.Repo.Delete(origin.Id));
        Ok(g.Repo.Get(origin.Id)).ShouldBeSameAs(origin);
        origin.State.ShouldBe(ComponentState.Deleted);
        origin.MetaData["origin"].ShouldBe("preserved");
        origin.Tags.ShouldContain("preserved");
        if (reference == "target")
        {
            // Move a deliberately cross-owned report to its destination before resolving it.
            target.Effects.Remove(pending);
            origin.Effects.Add(pending);
            target = origin;
        }

        g.Tick(target);
        Fail(g.Repo.Get(origin.Id));
    }

    [Fact]
    public void DeletedOrigin_Should_RemainUntilItemSourceReferenceExpires()
    {
        var reference = "itemSource";
        var g = new Game();
        var origin = g.Make("Ruby");
        origin.MetaData["origin"] = "preserved";
        origin.Tags.Add("preserved");
        var pending = g.Hit(Poison, -2, 1);
        var target = g.Orc;
        switch (reference)
        {
            case "target":
                pending.TargetId = origin.Id;
                break;
            case "source":
                pending.SourceId = origin.Id;
                break;
            case "instrument":
                pending.InstrumentId = origin.Id;
                break;
            case "itemSource":
                pending.Items[0] = pending.Items[0] with
                {
                    SourceId = origin.Id
                };
                break;
            case "itemInstrument":
                pending.Items[0] = pending.Items[0] with
                {
                    InstrumentId = origin.Id
                };
                break;
            case "owner":
                target = origin;
                pending.TargetId = origin.Id;
                break;
        }

        // A result-level target reference can retain a different record from its storage owner.
        // Cleanup must scan all identity fields, not infer references from storage location.
        target.Effects.Add(pending);
        Ok(g.Repo.Delete(origin.Id));
        Ok(g.Repo.Get(origin.Id)).ShouldBeSameAs(origin);
        origin.State.ShouldBe(ComponentState.Deleted);
        origin.MetaData["origin"].ShouldBe("preserved");
        origin.Tags.ShouldContain("preserved");
        if (reference == "target")
        {
            // Move a deliberately cross-owned report to its destination before resolving it.
            target.Effects.Remove(pending);
            origin.Effects.Add(pending);
            target = origin;
        }

        g.Tick(target);
        Fail(g.Repo.Get(origin.Id));
    }

    [Fact]
    public void DeletedOrigin_Should_RemainUntilItemInstrumentReferenceExpires()
    {
        var reference = "itemInstrument";
        var g = new Game();
        var origin = g.Make("Ruby");
        origin.MetaData["origin"] = "preserved";
        origin.Tags.Add("preserved");
        var pending = g.Hit(Poison, -2, 1);
        var target = g.Orc;
        switch (reference)
        {
            case "target":
                pending.TargetId = origin.Id;
                break;
            case "source":
                pending.SourceId = origin.Id;
                break;
            case "instrument":
                pending.InstrumentId = origin.Id;
                break;
            case "itemSource":
                pending.Items[0] = pending.Items[0] with
                {
                    SourceId = origin.Id
                };
                break;
            case "itemInstrument":
                pending.Items[0] = pending.Items[0] with
                {
                    InstrumentId = origin.Id
                };
                break;
            case "owner":
                target = origin;
                pending.TargetId = origin.Id;
                break;
        }

        // A result-level target reference can retain a different record from its storage owner.
        // Cleanup must scan all identity fields, not infer references from storage location.
        target.Effects.Add(pending);
        Ok(g.Repo.Delete(origin.Id));
        Ok(g.Repo.Get(origin.Id)).ShouldBeSameAs(origin);
        origin.State.ShouldBe(ComponentState.Deleted);
        origin.MetaData["origin"].ShouldBe("preserved");
        origin.Tags.ShouldContain("preserved");
        if (reference == "target")
        {
            // Move a deliberately cross-owned report to its destination before resolving it.
            target.Effects.Remove(pending);
            origin.Effects.Add(pending);
            target = origin;
        }

        g.Tick(target);
        Fail(g.Repo.Get(origin.Id));
    }

    [Fact]
    public void DeletedOrigin_Should_RemainUntilOwnedEffectsExpires()
    {
        var reference = "owner";
        var g = new Game();
        var origin = g.Make("Ruby");
        origin.MetaData["origin"] = "preserved";
        origin.Tags.Add("preserved");
        var pending = g.Hit(Poison, -2, 1);
        var target = g.Orc;
        switch (reference)
        {
            case "target":
                pending.TargetId = origin.Id;
                break;
            case "source":
                pending.SourceId = origin.Id;
                break;
            case "instrument":
                pending.InstrumentId = origin.Id;
                break;
            case "itemSource":
                pending.Items[0] = pending.Items[0] with
                {
                    SourceId = origin.Id
                };
                break;
            case "itemInstrument":
                pending.Items[0] = pending.Items[0] with
                {
                    InstrumentId = origin.Id
                };
                break;
            case "owner":
                target = origin;
                pending.TargetId = origin.Id;
                break;
        }

        // A result-level target reference can retain a different record from its storage owner.
        // Cleanup must scan all identity fields, not infer references from storage location.
        target.Effects.Add(pending);
        Ok(g.Repo.Delete(origin.Id));
        Ok(g.Repo.Get(origin.Id)).ShouldBeSameAs(origin);
        origin.State.ShouldBe(ComponentState.Deleted);
        origin.MetaData["origin"].ShouldBe("preserved");
        origin.Tags.ShouldContain("preserved");
        if (reference == "target")
        {
            // Move a deliberately cross-owned report to its destination before resolving it.
            target.Effects.Remove(pending);
            origin.Effects.Add(pending);
            target = origin;
        }

        g.Tick(target);
        Fail(g.Repo.Get(origin.Id));
    }

    [Fact]
    public void FailedRecalculation_Should_KeepEquipmentAndStatChanges()
    {
        var g = new Game();
        var fire = g.Own("RingOfFire", RingLeft);
        var strength = g.Own("StrengthRing");
        var calculator = Substitute.For<IStatCalculator>();
        calculator.ComponentTypeId.Returns(Character);
        calculator.StatTypeId.Returns(HP);
        calculator.Calculate(g.Hero.Id, g.World).Returns(Result.Fail("Calculator failure", 400));
        g.World.Calculators.Add(calculator);
        Fail(g.Relations.Equip(g.Hero.Id, strength.Id, RingLeft));
        g.Relation(strength).Type.ShouldBe(RelationType.Equipped);
        g.Relation(strength).SlotIds.ShouldBe(new[] { RingLeft });
        g.Relation(fire).Type.ShouldBe(RelationType.Inventory);
        g.Relation(fire).SlotIds.ShouldBeEmpty();
        g.Hero.Stats[Str].Bonus.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void FailedLastChargeDeletion_Should_KeepChargeAndRelationChanges()
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        wand.Stats[Charges].Current = 1;
        var calculator = Substitute.For<IStatCalculator>();
        calculator.ComponentTypeId.Returns(Character);
        calculator.StatTypeId.Returns(Mana);
        calculator.Calculate(g.Hero.Id, g.World).Returns(Result.Fail("Calculator failure", 400));
        g.World.Calculators.Add(calculator);
        Fail(g.Effects.Consume(wand.Id));
        wand.Stats[Charges].Current.ShouldBe(0);
        wand.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Relations.ShouldNotContain(x => x.TargetId == wand.Id);
    }

    [Fact]
    public void LaterTickFailure_Should_KeepAppliedDamageAndSteal()
    {
        var g = new Game();
        var dagger = g.Own("LeechDagger", MainHand);
        g.Hero.Stats[HP].Current = 80;
        g.Orc.Stats[Stun].Current = 2;
        g.Store(g.Hit(Poison, -2, 3, instrument: dagger));
        g.Store(g.Hit(Poison, -2, 3, instrument: dagger));
        var service = new FailSecondIncoming(g.World);
        Fail(service.Tick(g.Orc.Id, 1));
        service.Calls.ShouldBe(2);
        Current(g.Orc).ShouldBe(98);
        Current(g.Hero).ShouldBe(81);
        g.Orc.Stats[Stun].Current.ShouldBe(2);
        g.Orc.Effects.SelectMany(x => x.Items).ShouldAllBe(x => x.Duration == 3);
    }

    [Fact]
    public void StealAcrossDamageTypes_Should_ShareTheAvailableStatBudget()
    {
        var g = new Game();
        var dagger = g.Own("VampDagger", MainHand);
        g.Own("RingOfFire", RingLeft);
        g.Orc.Stats[HP].Current = 3;
        g.Hero.Stats[HP].Current = 80;
        var results = Ok(Pipeline.Run(g.Effects, g.Hero.Id, dagger.Id, g.Orc.Id));
        results.Single(x => x.TargetId == g.Hero.Id).SumStat(HP, g.World).ShouldBe(1.5, 0.000001);
        Current(g.Orc).ShouldBe(0);
        Current(g.Hero).ShouldBe(81.5, 0.000001);
    }

    [Fact]
    public void Incoming_Should_NotProduceAmountsForEmptyOrTimedOnlyInputs()
    {
        var g = new Game();
        var ward = g.Own("WardRing", RingLeft, g.Orc);
        var spy = new SpyModifier
        {
            Run = (result, _, context) =>
            {
                result.Items.Add(new EffectResultItem { EffectTypeId = Poison, Amount = 1, SourceId = context.OwnerId, InstrumentId = ward.Id });
                return result;
            }
        };
        g.World.Modifiers.Add(spy);
        ward.ModifierData = new()
        {
            Mod("spy", Poison, 1, scope: Scope.Passive, phase: Phase.Incoming)
        };
        var empty = g.Hit(Poison);
        empty.Items.Clear();
        g.In(empty)[0].Items.ShouldBeEmpty();
        var timed = g.Hit(Poison, -2, 2);
        var scheduled = g.In(timed)[0];
        scheduled.Items.ShouldBe(timed.Items);
        spy.Calls.ShouldBeEmpty();
        Ok(g.Effects.Apply(g.Orc.Id, scheduled));
        g.Tick()[0].Sum(Poison).ShouldBe(-1);
        spy.Calls.Count.ShouldBe(1);
        spy.Calls[0].Trigger.ShouldBe(Trigger.Tick);
    }

    [Fact]
    public void NaN_Should_BeRejectedForTimeAndEffectValuesWithoutMutation()
    {
        var value = double.NaN;
        var g = new Game();
        g.Store(g.Hit(Poison, -2, 3));
        var before = g.Snapshot();
        Fail(g.Effects.Tick(g.Orc.Id, value));
        Fail(g.Effects.Apply(g.Orc.Id, g.Hit(amount: value)));
        Fail(g.Effects.Incoming(g.Orc.Id, g.Hit(duration: value)));
        g.Snapshot().ShouldBe(before);
    }

    [Fact]
    public void PositiveInfinity_Should_BeRejectedForTimeAndEffectValuesWithoutMutation()
    {
        var value = double.PositiveInfinity;
        var g = new Game();
        g.Store(g.Hit(Poison, -2, 3));
        var before = g.Snapshot();
        Fail(g.Effects.Tick(g.Orc.Id, value));
        Fail(g.Effects.Apply(g.Orc.Id, g.Hit(amount: value)));
        Fail(g.Effects.Incoming(g.Orc.Id, g.Hit(duration: value)));
        g.Snapshot().ShouldBe(before);
    }

    [Fact]
    public void NegativeInfinity_Should_BeRejectedForTimeAndEffectValuesWithoutMutation()
    {
        var value = double.NegativeInfinity;
        var g = new Game();
        g.Store(g.Hit(Poison, -2, 3));
        var before = g.Snapshot();
        Fail(g.Effects.Tick(g.Orc.Id, value));
        Fail(g.Effects.Apply(g.Orc.Id, g.Hit(amount: value)));
        Fail(g.Effects.Incoming(g.Orc.Id, g.Hit(duration: value)));
        g.Snapshot().ShouldBe(before);
    }

    [Fact]
    public void DefaultWorld_Should_SupportRepositoryRelationsAndCompleteActions()
    {
        var definitions = new Game().World;
        var world = new World
        {
            Stats = definitions.Stats,
            EffectTypes = definitions.EffectTypes,
            ComponentTypes = definitions.ComponentTypes,
            Categories = definitions.Categories,
            Slots = definitions.Slots,
            Modifiers = definitions.Modifiers,
            ChargesStatId = Charges
        };
        Ok(new WorldBuilder(world).Build(world));
        world.Random.ShouldBeOfType<DefaultRandomSource>();
        world.Logic.ShouldBeOfType<DefaultWorldLogic>();
        var hero = Ok(world.Repository.Create(Character, SkillRoot));
        var orc = Ok(world.Repository.Create(Character, SkillRoot));
        foreach (var actor in new[]
        {
            hero,
            orc
        }

        )
            Ok(world.Repository.SetStat(actor.Id, new Stat { TypeId = HP, Max = 100, ModifiedMax = 100, Current = 100 }));
        hero.MetaData["faction"] = "heroes";
        orc.MetaData["faction"] = "orcs";
        orc.MetaData["x"] = "3";
        orc.MetaData["y"] = "4";
        world.Logic.GetDistance(hero.Id, orc.Id).ShouldBe(5);
        world.Logic.GetRelationKind(hero.Id, orc.Id).ShouldBe(RelationKind.Enemies);
        var sword = Ok(world.Repository.Create(Item, Sword));
        sword.ModifierData.Add(Mod("decrease", Physical, 10));
        var relations = new RelationService(world);
        Ok(relations.Add(hero.Id, sword.Id));
        Ok(relations.Equip(hero.Id, sword.Id, MainHand));
        Ok(Pipeline.Run(new DefaultEffectService(world), hero.Id, sword.Id, orc.Id));
        Current(orc).ShouldBe(90);
        var calculator = new StatCalculator
        {
            ComponentTypeId = Character,
            StatTypeId = HP
        };
        Ok(calculator.Calculate(hero.Id, world)).ShouldBe(100);
    }
}

internal sealed class FailSecondIncoming(World world) : DefaultEffectService(world)
{
    internal int Calls { get; private set; }

    public override Result<List<EffectResult>> Incoming(int targetId, EffectResult result) => ++Calls == 2 ? Result.Fail("Second Incoming failed", 400) : base.Incoming(targetId, result);
}
