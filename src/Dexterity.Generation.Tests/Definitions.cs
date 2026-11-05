using static Dexterity.Generation.Tests.Game;

namespace Dexterity.Generation.Tests;

public sealed partial class AcceptanceTests
{
    // CAT-01..14, DET-06: filtering is discovery, not generation.
    [Fact]
    public void Catalog_Should_CombineFiltersAndReturnOrderedDefinitionReferences()
    {
        var g = new Game();
        g.Template.Tags = new() { "Weapon", "Melee" };
        g.Template.Conditions.Add(new GenerationCondition { Key = "missing", Value = "yes" });
        var second = new GenerationTemplate { Id = "Sword", Name = "Longsword", TypeId = Item, CategoryId = Sword, Tags = new() { "weapon" } };
        g.Catalog.Templates.Add(second);
        Ok(g.Service.Get("sword")).ShouldBeSameAs(g.Template);
        Ok(g.Service.Get("Sword")).ShouldBeSameAs(second);
        Ok(g.Service.Find(new TemplateFilter())).ShouldBe(g.Catalog.Templates);
        Ok(g.Service.Find(new TemplateFilter { TypeId = Item, CategoryId = Sword, Tags = new() { "weapon", "MELEE" } })).ShouldBe(new[] { g.Template });
        Ok(g.Service.Find(new TemplateFilter { CategoryId = 6 })).ShouldBeEmpty();
        Ok(g.Service.Find(new TemplateFilter { Tags = new() { "absent" } })).ShouldBeEmpty();
        Fail(g.Service.Get("LONGSWORD"));
        Fail(g.Service.Find(new TemplateFilter { TypeId = 999 }));
        Fail(g.Service.Find(new TemplateFilter { CategoryId = 999 }));
        Fail(g.Service.Find(null!));
        second.Name = "";
        Fail(g.Service.Validate());
        Fail(g.Service.Find(new TemplateFilter()));
        Ok(g.Service.Get("sword")).ShouldBeSameAs(g.Template);
        g.Template.Conditions.Clear();
        g.Create();
        second.Id = "sword";
        Fail(g.Service.Get("sword"));
        Fail(g.Service.Create(g.Request()));
        g.Random.Calls.ShouldBeEmpty();
    }

    // VAL-02..12, VAL-14, VAL-16, MAP-04..05, MAP-16: whole-tree validation before randomness.
    [Theory]
    [InlineData("name")]
    [InlineData("type")]
    [InlineData("category")]
    [InlineData("unknown-stat")]
    [InlineData("charges")]
    [InlineData("null-items")]
    [InlineData("null-entry")]
    [InlineData("blank-id")]
    [InlineData("duplicate")]
    [InlineData("nested-duplicate")]
    [InlineData("cycle")]
    [InlineData("reused")]
    [InlineData("chance-low")]
    [InlineData("chance-high")]
    [InlineData("negative-weight")]
    [InlineData("zero-weight")]
    [InlineData("negative-variation")]
    [InlineData("weight-overflow")]
    [InlineData("cost")]
    [InlineData("bounds")]
    [InlineData("negative-min")]
    [InlineData("null-fixed")]
    [InlineData("kind")]
    [InlineData("binding-stat")]
    [InlineData("duplicate-stat")]
    [InlineData("duplicate-data")]
    [InlineData("source")]
    [InlineData("modifier")]
    [InlineData("effect")]
    [InlineData("phase")]
    [InlineData("parameter")]
    [InlineData("null-conditions")]
    [InlineData("null-tags")]
    public void InvalidDefinitions_Should_FailBeforeDrawingOrCreating(string defect)
    {
        var g = new Game();
        g.SwordTemplate();
        var value = (GenerationValue)g.Template.Items[0];
        switch (defect)
        {
            case "name": g.Template.Name = " "; break;
            case "type": g.Template.TypeId = 999; break;
            case "category": g.Template.CategoryId = 999; break;
            case "unknown-stat": g.World.ComponentTypes[1].StatIds.Add(999); break;
            case "charges": g.Template.CategoryId = Wand; g.World.ChargesStatId = null; break;
            case "null-items": g.Template.Items = null!; break;
            case "null-entry": g.Template.Items.Add(null!); break;
            case "blank-id": value.Id = " "; break;
            case "duplicate": g.Template.Items.Add(new GenerationGroup { Id = value.Id }); break;
            case "nested-duplicate": g.Template.Items.Add(new GenerationGroup { Id = "nested", Items = new() { new GenerationValue { Id = value.Id } } }); break;
            case "cycle": var group = new GenerationGroup { Id = "loop" }; group.Items.Add(group); g.Template.Items.Add(group); break;
            case "reused": g.Template.Items.Add(new GenerationGroup { Id = "reused", Items = new() { value } }); break;
            case "chance-low": value.ChanceToGet = -1; break;
            case "chance-high": value.ChanceToGet = 101; break;
            case "negative-weight": value.Weight = -1; break;
            case "zero-weight": value.Weight = 0; break;
            case "negative-variation": value.VaryWeightBy = -1; break;
            case "weight-overflow": value.Weight = int.MaxValue; value.VaryWeightBy = 1; break;
            case "cost": value.WorthPerValue = 0; break;
            case "bounds": value.Kind = GenerationValueKind.Random; value.MinValue = 4; value.MaxValue = 3; break;
            case "negative-min": value.Kind = GenerationValueKind.Random; value.MinValue = -1; break;
            case "null-fixed": value.Kind = GenerationValueKind.Fixed; value.Value = null; break;
            case "kind": value.Kind = (GenerationValueKind)999; break;
            case "binding-stat": g.Template.StatBindings[0].StatTypeId = HP; break;
            case "duplicate-stat": g.Template.StatBindings.Add(g.Template.StatBindings[0]); break;
            case "duplicate-data": g.Template.DataBindings.Add(g.Template.DataBindings[0]); break;
            case "source": g.Template.StatBindings[0].MaxKey = "missing"; break;
            case "modifier": g.Template.Modifiers[0].Base.TypeId = "missing"; break;
            case "effect": g.Template.Modifiers[0].Base.EffectTypeId = 999; break;
            case "phase": g.Template.Modifiers[0].Base.Phase = Phase.Incoming; break;
            case "parameter": g.Template.Modifiers[0].DataKeys = new() { [" "] = "damage" }; break;
            case "null-conditions": value.Conditions = null!; break;
            case "null-tags": g.Template.Tags = null!; break;
        }
        var before = g.World.Repository.Items.Count;
        Fail(g.Service.Create(g.Request()));
        g.World.Repository.Items.Count.ShouldBe(before);
        g.Random.Calls.ShouldBeEmpty();
        Ok(g.World.Repository.Create(Character, 1)).Id.ShouldBe(1);
    }

    // VAL-01, RES-02.
    [Theory]
    [InlineData("null")]
    [InlineData("context")]
    [InlineData("id")]
    [InlineData("worth")]
    public void InvalidRequests_Should_ReturnDescriptiveFailures(string defect)
    {
        var g = new Game();
        var request = g.Request();
        switch (defect)
        {
            case "null": request = null!; break;
            case "context": request.Context = null!; break;
            case "id": request.TemplateId = " "; break;
            case "worth": request.Worth = -1; break;
        }
        Fail(g.Service.Create(request));
        g.World.Repository.Items.ShouldBeEmpty();
        g.Random.Calls.ShouldBeEmpty();
    }

    // VAL-07, CAT-13: edits take effect; group IDs are local, leaf IDs global.
    [Fact]
    public void LocalGroupIds_Should_BeReusableButDefinitionEditsRevalidated()
    {
        var g = new Game();
        g.Template.Items = new()
        {
            new GenerationGroup { Id = "a", Items = new() { new GenerationGroup { Id = "same", Items = new() { new GenerationValue { Id = "one" } } } } },
            new GenerationGroup { Id = "b", Items = new() { new GenerationGroup { Id = "same", Items = new() { new GenerationValue { Id = "two" } } } } }
        };
        Ok(g.Service.Validate());
        g.Create();
        g.Template.Name = "";
        Fail(g.Service.Create(g.Request()));
        g.World.Repository.Items.Count.ShouldBe(1);
    }

    // VAL-13.
    [Fact]
    public void ActualWorthOverflow_Should_FailWithoutInsertingAComponent()
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationValue { Id = "overflow", Kind = GenerationValueKind.Random, MinValue = int.MaxValue, MaxValue = int.MaxValue, WorthPerValue = 2 });
        Fail(g.Service.Create(g.Request(0)));
        g.World.Repository.Items.ShouldBeEmpty();
    }
}
