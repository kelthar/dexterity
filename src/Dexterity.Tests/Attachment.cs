using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed class AttachmentTests
{
    [Fact]
    public void DetachedComponents_Should_NotAllocateIdsOrAppearInTheWorld()
    {
        var g = new Game();
        var before = g.Snapshot();
        var nextId = Ok(g.Repo.Save()).NextComponentId;
        var first = Ok(g.Repo.CreateDetached(Item, Sword));
        var second = Ok(g.Repo.CreateDetached(Character, SkillRoot));

        new Component().Id.ShouldBe(-1);
        new Component().State.ShouldBe(ComponentState.Detached);
        first.Id.ShouldBe(-1);
        second.Id.ShouldBe(-1);
        first.State.ShouldBe(ComponentState.Detached);
        second.State.ShouldBe(ComponentState.Detached);
        second.Stats.Keys.OrderBy(x => x).ShouldBe(new[] { HP, Mana, Str, Stun });
        first.Stats[Charges].TypeId.ShouldBe(Charges);
        g.Snapshot().ShouldBe(before);
        Ok(g.Repo.Save()).NextComponentId.ShouldBe(nextId);
        Fail(g.Repo.Get(-1));
        Fail(g.Relations.Add(g.Hero.Id, first.Id));
        Fail(g.Effects.Recalculate(first.Id));
        Ok(g.Repo.Create(Item, Sword)).Id.ShouldBe((int)nextId);
    }

    [Fact]
    public void Attach_Should_RegisterThePreparedInstanceWithoutCreatingRelations()
    {
        var g = new Game();
        var component = Ok(g.Repo.CreateDetached(Item, Sword));
        component.Name = "Prepared sword";
        component.Tags.Add("crafted");
        component.MetaData["quality"] = "rare";
        component.Stats[Charges].Max = 10;
        component.Stats[Charges].ModifiedMax = 10;
        component.Stats[Charges].Current = 4;
        component.ModifierData.Add(Mod("decrease", Physical, 2));
        var stat = component.Stats[Charges];
        var modifier = component.ModifierData[0];
        var nextId = Ok(g.Repo.Save()).NextComponentId;

        Ok(g.Repo.Attach(component)).ShouldBeSameAs(component);
        component.Id.ShouldBe((int)nextId);
        component.State.ShouldBe(ComponentState.Active);
        Ok(g.Repo.Get(component.Id)).ShouldBeSameAs(component);
        component.Stats[Charges].ShouldBeSameAs(stat);
        stat.Current.ShouldBe(4);
        component.ModifierData.Single().ShouldBeSameAs(modifier);
        component.Name.ShouldBe("Prepared sword");
        component.Tags.ShouldBe(new[] { "crafted" });
        component.MetaData["quality"].ShouldBe("rare");
        g.Repo.GetParents(component.Id).ShouldBeEmpty();
        Ok(g.Relations.Add(g.Hero.Id, component.Id));
        Ok(g.Relations.Equip(g.Hero.Id, component.Id, MainHand));
        Ok(g.Repo.Save()).Components.ShouldContain(x => x.Id == component.Id);
    }

    [Theory]
    [ClassData(typeof(InvalidComponents))]
    public void InvalidAttachment_Should_LeaveTheObjectDetachedAndAllocateNoId(string defect)
    {
        var g = new Game();
        var component = Ok(g.Repo.CreateDetached(Item, Sword));
        var nextId = Ok(g.Repo.Save()).NextComponentId;
        var before = g.Snapshot();
        switch (defect)
        {
            case "type": component.TypeId = 999; break;
            case "category": component.CategoryId = 999; break;
            case "name": component.Name = null!; break;
            case "stats": component.Stats = null!; break;
            case "data": component.MetaData = null!; break;
            case "tags": component.Tags = null!; break;
            case "modifiers": component.ModifierData = null!; break;
            case "effects": component.Effects = null!; break;
            case "null-tag": component.Tags.Add(null!); break;
            case "null-data": component.MetaData["bad"] = null!; break;
            case "missing-stat": component.Stats.Clear(); break;
            case "extra-stat": component.Stats[999] = new Stat { TypeId = 999 }; break;
            case "null-stat": component.Stats[Charges] = null!; break;
            case "stat-type": component.Stats[Charges].TypeId = HP; break;
            case "nan": component.Stats[Charges].Max = double.NaN; break;
            case "infinity": component.Stats[Charges].Bonus = double.PositiveInfinity; break;
            case "negative-max": component.Stats[Charges].Max = -1; break;
            case "negative-modified": component.Stats[Charges].ModifiedMax = -1; break;
            case "negative-current": component.Stats[Charges].Current = -1; break;
            case "excess-current": component.Stats[Charges].Current = 1; break;
            case "null-modifier": component.ModifierData.Add(null!); break;
            case "invalid-modifier": component.ModifierData.Add(new ModifierData { TypeId = "unknown" }); break;
            case "scheduled-effect": component.Effects.Add(g.Hit(Poison, -2, 3)); break;
        }

        Fail(g.Repo.Attach(component));
        component.Id.ShouldBe(-1);
        component.State.ShouldBe(ComponentState.Detached);
        g.Snapshot().ShouldBe(before);
        Ok(g.Repo.Save()).NextComponentId.ShouldBe(nextId);
        Ok(g.Repo.Create(Item, Sword)).Id.ShouldBe((int)nextId);
    }

    [Fact]
    public void FailedAttachment_Should_AllowCorrectionAndRetry()
    {
        var g = new Game();
        var component = Ok(g.Repo.CreateDetached(Item, Sword));
        var nextId = Ok(g.Repo.Save()).NextComponentId;
        component.Stats[Charges].Current = 3;
        Fail(g.Repo.Attach(component));
        component.Stats[Charges].Max = 5;
        component.Stats[Charges].ModifiedMax = 5;
        Ok(g.Repo.Attach(component)).Id.ShouldBe((int)nextId);
        component.Stats[Charges].Current.ShouldBe(3);
    }

    [Fact]
    public void Attach_Should_RevalidateDefinitionsChangedSinceConstruction()
    {
        var g = new Game();
        var component = Ok(g.Repo.CreateDetached(Item, Sword));
        var type = g.World.ComponentTypes.Single(x => x.Id == Item);
        type.StatIds.Add(HP);
        Fail(g.Repo.Attach(component));
        component.Id.ShouldBe(-1);
        type.StatIds.Remove(HP);
        Ok(g.Repo.Attach(component));
    }

    [Theory]
    [InlineData(-1, ComponentState.Active)]
    [InlineData(-1, ComponentState.Deleted)]
    [InlineData(-1, (ComponentState)99)]
    [InlineData(0, ComponentState.Detached)]
    [InlineData(42, ComponentState.Detached)]
    [InlineData(-2, ComponentState.Detached)]
    public void Attach_Should_RejectInvalidLifecycle(int id, ComponentState state)
    {
        var g = new Game();
        var component = Ok(g.Repo.CreateDetached(Item, Sword));
        component.Id = id;
        component.State = state;
        var before = g.Snapshot();
        Fail(g.Repo.Attach(component));
        component.Id.ShouldBe(id);
        component.State.ShouldBe(state);
        g.Snapshot().ShouldBe(before);
        Fail(g.Repo.Attach(null!));
    }

    [Fact]
    public void AttachedAndDeletedComponents_Should_KeepTheirIdentityAndRejectReattachment()
    {
        var g = new Game();
        var component = Ok(g.Repo.Create(Item, Sword));
        var id = component.Id;
        Fail(g.Repo.Attach(component));
        Ok(g.Repo.Delete(id));
        component.Id.ShouldBe(id);
        component.State.ShouldBe(ComponentState.Deleted);
        g.Repo.Items.ShouldNotContainKey(id);
        Fail(g.Repo.Attach(component));
        Ok(g.Repo.Create(Item, Sword)).Id.ShouldBeGreaterThan(id);
    }

    [Fact]
    public void ExhaustedIds_Should_StillAllowDetachedConstructionButRejectAttachment()
    {
        var g = new Game();
        var package = Ok(g.Repo.Save());
        package.NextComponentId = (long)int.MaxValue + 1;
        Ok(g.Repo.Load(package));
        var component = Ok(g.Repo.CreateDetached(Item, Sword));
        Fail(g.Repo.Attach(component));
        component.Id.ShouldBe(-1);
        component.State.ShouldBe(ComponentState.Detached);
        Ok(g.Repo.Save()).NextComponentId.ShouldBe(package.NextComponentId);
    }

    [Fact]
    public void Packages_Should_RejectDetachedStateEvenWithAPositiveIdentity()
    {
        var g = new Game();
        var package = Ok(g.Repo.Save());
        package.Components[0].State = ComponentState.Detached;
        var before = g.Snapshot();
        Fail(g.Repo.Load(package));
        g.Snapshot().ShouldBe(before);
        g.Hero.State = ComponentState.Detached;
        Fail(g.Repo.Save());
        ((int)ComponentState.Active).ShouldBe(0);
        ((int)ComponentState.Deleted).ShouldBe(1);
    }

    public sealed class InvalidComponents : TheoryData<string>
    {
        public InvalidComponents()
        {
            foreach (var defect in new[]
            {
                "type", "category", "name", "stats", "data", "tags", "modifiers", "effects", "null-tag", "null-data",
                "missing-stat", "extra-stat", "null-stat", "stat-type", "nan", "infinity", "negative-max", "negative-modified",
                "negative-current", "excess-current", "null-modifier", "invalid-modifier", "scheduled-effect"
            })
                Add(defect);
        }
    }
}
