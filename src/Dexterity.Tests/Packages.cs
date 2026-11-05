using System.Text.Json;
using NSubstitute;
using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    [Fact]
    public void WorldPackage_Should_RoundTripStateWithoutReplacingDefinitionsOrRecalculating()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var skill = g.Own("Cleave1");
        g.Link(g.Ally, skill);
        g.Hero.MetaData["quest"] = "started";
        g.Hero.Tags.Add("chosen");
        g.Hero.Stats[HP].Current = 42;
        g.Store(g.Hit(Poison, -2, 3, instrument: sword));
        var before = g.Snapshot();
        IWorldPackageService service = new WorldPackageService();
        var package = Ok(service.Save(g.World));
        var json = JsonSerializer.Serialize(package);
        json.ShouldNotContain("ComponentTypes");
        json.ShouldNotContain("Categories");
        var restored = JsonSerializer.Deserialize<WorldPackage>(json)!;
        var target = new Game();
        var repository = target.World.Repository;
        var definitions = target.World.ComponentTypes;
        var random = target.World.Random;
        var logic = target.World.Logic;
        Ok(service.Load(target.World, restored));
        target.Snapshot().ShouldBe(before);
        target.World.Repository.ShouldBeSameAs(repository);
        target.World.ComponentTypes.ShouldBeSameAs(definitions);
        target.World.Random.ShouldBeSameAs(random);
        target.World.Logic.ShouldBeSameAs(logic);
        target.Repo.GetParents(skill.Id).Count.ShouldBe(2);
        target.Repo.Items[g.Hero.Id].ShouldNotBeSameAs(g.Hero);
        target.Repo.Items[sword.Id].ShouldNotBeSameAs(restored.Components.Single(x => x.Id == sword.Id));
        Ok(target.Effects.Tick(target.Orc.Id, 1));
        Current(target.Repo.Items[target.Orc.Id]).ShouldBe(98);
        Ok(target.Effects.Attack(target.Hero.Id, sword.Id, target.Orc.Id));
        Current(target.Repo.Items[target.Orc.Id]).ShouldBe(88);
    }

    [Fact]
    public void WorldPackage_Should_DeepCopyEveryMutablePartOfSavedState()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Store(g.Hit(Poison, -2, 3, instrument: sword));
        var before = g.Snapshot();
        var package = Ok(new WorldPackageService().Save(g.World));
        var hero = package.Components.Single(x => x.Id == g.Hero.Id);
        hero.Stats[HP].Current = 0;
        hero.Tags.Add("changed");
        hero.MetaData["changed"] = "yes";
        package.Components.Single(x => x.Id == sword.Id).ModifierData[0].Data["value"] = 999;
        package.Components.Single(x => x.Id == g.Orc.Id).Effects[0].Items.Clear();
        package.Relations[0].SlotIds.Clear();
        g.Snapshot().ShouldBe(before);
    }

    [Fact]
    public void WorldPackage_Should_PreserveDeletedComponentIds()
    {
        var g = new Game();
        var item = Ok(g.Repo.Create(Item, Sword));
        Ok(g.Repo.Delete(item.Id));
        var reserved = Ok(g.Repo.Create(Item, Sword)).Id;
        Ok(g.Repo.Delete(reserved));
        var service = new WorldPackageService();
        var package = Ok(service.Save(g.World));
        package.NextComponentId.ShouldBe(reserved + 1L);
        var target = new Game();
        Ok(service.Load(target.World, package));
        Ok(target.Repo.Create(Item, Sword)).Id.ShouldBe(reserved + 1);
    }

    [Fact]
    public void WorldPackage_Should_RetainDeletedTimedOriginsUntilTheirEffectsExpire()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        g.Store(g.Hit(Fire, -1, 2, instrument: bomb));
        Ok(g.Repo.Delete(bomb.Id));
        var service = new WorldPackageService();
        var package = Ok(service.Save(g.World));
        var target = new Game();
        Ok(service.Load(target.World, package));
        target.Repo.Items[bomb.Id].State.ShouldBe(ComponentState.Deleted);
        Ok(target.Effects.Tick(target.Orc.Id, 2));
        target.Repo.Items.ShouldNotContainKey(bomb.Id);
        Current(target.Repo.Items[target.Orc.Id]).ShouldBe(98);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("components")]
    [InlineData("null-component")]
    [InlineData("duplicate")]
    [InlineData("id")]
    [InlineData("cursor")]
    [InlineData("cursor-overflow")]
    [InlineData("type")]
    [InlineData("category")]
    [InlineData("state")]
    [InlineData("stats")]
    [InlineData("missing-stat")]
    [InlineData("stat-key")]
    [InlineData("stat-current")]
    [InlineData("stat-nan")]
    [InlineData("modifier")]
    [InlineData("endpoint")]
    [InlineData("duplicate-relation")]
    [InlineData("relation-kind")]
    [InlineData("cycle")]
    [InlineData("slots")]
    [InlineData("slot-conflict")]
    [InlineData("slot-category")]
    [InlineData("inventory-slot")]
    [InlineData("parents")]
    [InlineData("effect-origin")]
    [InlineData("effect-target")]
    [InlineData("effect-duration")]
    [InlineData("effect-type")]
    public void WorldPackage_Should_RejectInvalidStateWithoutMutatingTheWorldOrPackage(string defect)
    {
        var source = new Game();
        var sword = source.Own("Longsword", MainHand);
        source.Store(source.Hit(Poison, -2, 3, instrument: sword));
        var service = new WorldPackageService();
        var package = Ok(service.Save(source.World));
        var hero = package.Components.Single(x => x.Id == source.Hero.Id);
        var relation = package.Relations[0];
        var effect = package.Components.Single(x => x.Id == source.Orc.Id).Effects[0];
        switch (defect)
        {
            case "version": package.Version = 99; break;
            case "components": package.Components = null!; break;
            case "null-component": package.Components.Add(null!); break;
            case "duplicate": package.Components.Add(hero); break;
            case "id": hero.Id = 0; break;
            case "cursor": package.NextComponentId = hero.Id; break;
            case "cursor-overflow": package.NextComponentId = (long)int.MaxValue + 2; break;
            case "type": hero.TypeId = 999; break;
            case "category": hero.CategoryId = 999; break;
            case "state": hero.State = (ComponentState)99; break;
            case "stats": hero.Stats = null!; break;
            case "missing-stat": hero.Stats.Remove(HP); break;
            case "stat-key": hero.Stats[HP].TypeId = Mana; break;
            case "stat-current": hero.Stats[HP].Current = 101; break;
            case "stat-nan": hero.Stats[HP].Max = double.NaN; break;
            case "modifier": package.Components.Single(x => x.Id == sword.Id).ModifierData[0].TypeId = "unknown"; break;
            case "endpoint": relation.TargetId = 999; break;
            case "duplicate-relation": package.Relations.Add(relation); break;
            case "relation-kind": relation.Type = (RelationType)99; break;
            case "cycle": package.Relations.Add(new() { Id = 2, ParentId = sword.Id, TargetId = hero.Id }); break;
            case "slots": relation.SlotIds = new() { 999 }; break;
            case "slot-conflict": package.Relations.Add(new() { Id = 2, ParentId = source.Hero.Id, TargetId = source.Ally.Id, Type = RelationType.Equipped, SlotIds = new() { MainHand } }); break;
            case "slot-category": relation.SlotIds = new() { RingLeft }; break;
            case "inventory-slot": relation.Type = RelationType.Inventory; break;
            case "parents": package.Relations.Add(new() { Id = 2, ParentId = source.Ally.Id, TargetId = sword.Id }); break;
            case "effect-origin": effect.Items[0] = effect.Items[0] with { InstrumentId = 999 }; break;
            case "effect-target": effect.TargetId = source.Hero.Id; break;
            case "effect-duration": effect.Items[0] = effect.Items[0] with { Duration = -1 }; break;
            case "effect-type": effect.Items[0] = effect.Items[0] with { EffectTypeId = 999 }; break;
        }
        var target = new Game();
        var before = target.Snapshot();
        var options = new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        var input = JsonSerializer.Serialize(package, options);
        Fail(service.Load(target.World, package));
        target.Snapshot().ShouldBe(before);
        JsonSerializer.Serialize(package, options).ShouldBe(input);
        Ok(target.Repo.Create(Item, Sword)).Id.ShouldBe(7);
    }

    [Fact]
    public void WorldPackage_Should_ReplaceExistingStateAndAllowAnEmptySave()
    {
        var g = new Game();
        var service = new WorldPackageService();
        Ok(service.Load(g.World, new WorldPackage()));
        g.Repo.Items.ShouldBeEmpty();
        g.Repo.Relations.ShouldBeEmpty();
        var package = Ok(service.Save(g.World));
        package.Components.ShouldBeEmpty();
        Ok(g.Repo.Create(Item, Sword)).Id.ShouldBe(1);
    }

    [Fact]
    public void WorldPackage_Should_ValidateSavedStateAgainstConfiguredDefinitions()
    {
        var g = new Game();
        var service = new WorldPackageService();
        var package = Ok(service.Save(g.World));
        var target = new Game();
        target.World.Stats.RemoveAll(x => x.Id == HP);
        Fail(service.Load(target.World, package));
        g.Hero.Stats[HP].Current = double.NaN;
        Fail(service.Save(g.World));
    }

    [Fact]
    public void WorldPackage_Should_RejectNullArguments()
    {
        var service = new WorldPackageService();
        Fail(service.Save(null!));
        Fail(service.Load(null!, new WorldPackage()));
        Fail(service.Load(new Game().World, null!));
    }

    [Fact]
    public void WorldPackage_Should_PreserveSavedStatValuesWithoutCallingCalculators()
    {
        var source = new Game();
        source.Hero.Stats[HP].Bonus = 7;
        source.Hero.Stats[HP].ModifiedMax = 107;
        source.Hero.Stats[HP].Current = 42;
        var service = new WorldPackageService();
        var package = Ok(service.Save(source.World));
        var target = new Game();
        var calculator = Substitute.For<IStatCalculator>();
        calculator.ComponentTypeId.Returns(Character);
        calculator.StatTypeId.Returns(HP);
        target.World.Calculators.Add(calculator);
        Ok(service.Load(target.World, package));
        var stat = target.Repo.Items[source.Hero.Id].Stats[HP];
        (stat.Max, stat.ModifiedMax, stat.Bonus, stat.Current).ShouldBe((100d, 107d, 7d, 42d));
        calculator.DidNotReceive().Calculate(Arg.Any<int>(), Arg.Any<World>());
        package.Components.Single(x => x.Id == source.Hero.Id).Stats[HP].Current = 0;
        stat.Current.ShouldBe(42);
    }

    [Fact]
    public void WorldPackage_Should_RestoreAnExhaustedIdentitySpace()
    {
        var g = new Game();
        var service = new WorldPackageService();
        var package = Ok(service.Save(g.World));
        package.NextComponentId = (long)int.MaxValue + 1;
        Ok(service.Load(g.World, package));
        Fail(g.Repo.Create(Item, Sword));
        Ok(service.Save(g.World)).NextComponentId.ShouldBe((long)int.MaxValue + 1);
    }

    [Fact]
    public void WorldPackage_Should_RejectDuplicateSharedSkillLevels()
    {
        var g = new Game();
        g.Own("Cleave1");
        g.Own("Cleave2");
        Fail(new WorldPackageService().Save(g.World));
    }

    [Fact]
    public void WorldPackage_Should_AllocateFreshRelationIdsAfterLoad()
    {
        var source = new Game();
        source.Own("Longsword", MainHand);
        source.Repo.Relations[0].Id = 500;
        var service = new WorldPackageService();
        var target = new Game();
        Ok(service.Load(target.World, Ok(service.Save(source.World))));
        var item = Ok(target.Repo.Create(Item, Sword));
        Ok(target.Relations.Add(target.Hero.Id, item.Id));
        target.Repo.Relations.Single(x => x.TargetId == item.Id).Id.ShouldBe(501);
    }
}
