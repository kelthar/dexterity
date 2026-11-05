using static Dexterity.Generation.Tests.Game;

namespace Dexterity.Generation.Tests;

public sealed partial class AcceptanceTests
{
    // MAP-06..07, MAP-10, MAP-12: conditional output must not leave partial modifiers.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOutputs_Should_OmitOptionalBindingsButRejectIncompleteCurrent(bool missingCurrent)
    {
        var g = new Game();
        g.SwordTemplate();
        ((GenerationValue)g.Template.Items[0]).Conditions.Add(new GenerationCondition { Key = "missing", Value = "yes" });
        ((GenerationValue)g.Template.Items[2]).Conditions.Add(new GenerationCondition { Key = "missing", Value = "yes" });
        if (missingCurrent)
            g.Template.StatBindings[0].CurrentKey = "tier";
        var result = g.Service.Create(g.Request());
        if (missingCurrent)
        {
            Fail(result);
            g.World.Repository.Items.ShouldBeEmpty();
            return;
        }
        var component = g.Component(Ok(result));
        component.ModifierData.ShouldBeEmpty();
        component.MetaData.ShouldBeEmpty();
        component.Stats[Durability].Current.ShouldBe(40);
    }

    // MAP-08, MAP-14..15.
    [Theory]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("invalid")]
    public void InvalidGeneratedModifierValues_Should_FailBeforeRepositoryWrites(string value)
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationValue { Id = "number", Kind = GenerationValueKind.Fixed, Value = value });
        g.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "critical", EffectTypeId = 1, Data = new() { ["multiplier"] = 2 } }, DataKeys = new() { ["chance"] = "number" } });
        Fail(g.Service.Create(g.Request()));
        g.World.Repository.Items.ShouldBeEmpty();
        Ok(g.World.Repository.Create(Item, Sword)).Id.ShouldBe(1);
    }

    // MAP-11, MAP-13, MAP-18: constants and numeric bindings deep-copy their source.
    [Fact]
    public void ModifierBindings_Should_CopyBaseAndOverrideNumericValuesAndDuration()
    {
        var g = new Game();
        g.Template.Items = new()
        {
            new GenerationValue { Id = "amount", Kind = GenerationValueKind.Fixed, Value = "2.5" },
            new GenerationValue { Id = "duration", Kind = GenerationValueKind.Fixed, Value = "3.5" }
        };
        var source = new ModifierData { TypeId = "decrease", EffectTypeId = 1, IncludedTags = new() { "weapon" }, Data = new() { ["value"] = 1 } };
        g.Template.Modifiers.Add(new ModifierTemplate { Base = source, DataKeys = new() { ["value"] = "amount" }, DurationKey = "duration" });
        g.Template.Modifiers.Add(new ModifierTemplate { Base = source });
        var first = g.Create();
        var second = g.Create();
        var modifiers = g.Component(first).ModifierData;
        modifiers[0].Data["value"].ShouldBe(2.5);
        modifiers[0].Duration.ShouldBe(3.5);
        modifiers[1].Data["value"].ShouldBe(1);
        modifiers[0].IncludedTags.Add("changed");
        modifiers[0].Data["value"] = 9;
        source.IncludedTags.ShouldBe(new[] { "weapon" });
        source.Data["value"].ShouldBe(1);
        g.Component(second).ModifierData[0].Data["value"].ShouldBe(2.5);
        g.Component(second).ModifierData[0].IncludedTags.ShouldBe(new[] { "weapon" });
    }

    [Fact]
    public void GeneratedPassiveModifiers_Should_ActivateOnlyAfterAttachment()
    {
        var g = new Game();
        g.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "increase", EffectTypeId = 2, Phase = Phase.StatsRecalc, Scope = Scope.Passive, Data = new() { ["value"] = 2 } } });
        var item = g.Component(g.Create());
        var parent = Ok(g.World.Repository.Create(Character, 1));
        Ok(g.World.Repository.SetStat(parent.Id, new Stat { TypeId = Str, Max = 5, ModifiedMax = 5, Current = 5 }));
        parent.Stats[Str].ModifiedMax.ShouldBe(5);
        var relations = new RelationService(g.World);
        Ok(relations.Add(parent.Id, item.Id));
        Ok(relations.Equip(parent.Id, item.Id, 1));
        parent.Stats[Str].ModifiedMax.ShouldBe(7);
    }

    // INT-01..02, ROL-10.
    [Fact]
    public void GeneratedWeapons_Should_JoinNormalEquipmentAndEffectPipelines()
    {
        var g = new Game();
        g.SwordTemplate();
        var generated = g.Create();
        var weapon = g.Component(generated);
        var source = Ok(g.World.Repository.Create(Character, 1));
        var target = Ok(g.World.Repository.Create(Character, 1));
        source.MetaData["faction"] = "hero";
        target.MetaData["faction"] = "enemy";
        var relations = new RelationService(g.World);
        Ok(relations.Add(source.Id, weapon.Id));
        Ok(relations.Equip(source.Id, weapon.Id, 1));
        var effects = new DefaultEffectService(g.World);
        var outgoing = Ok(effects.Outgoing(source.Id, weapon.Id, target.Id));
        outgoing.Single().Sum(1).ShouldBe(-15);
        var tags = Ok(new TagService(g.World).GetTags(weapon.Id));
        tags.ShouldContain("crafted");
        tags.ShouldContain("item");
        tags.ShouldContain("weapon-category");
        tags.ShouldNotContain("melee");
        weapon.MetaData["cost"] = generated.Generation.TotalWorth.ToString();
        Fail(new RelationService(g.World).Equip(999, weapon.Id, 1));
        g.World.Repository.Items[weapon.Id].ShouldBeSameAs(weapon);
        weapon.MetaData["cost"].ShouldBe("40");
    }
}
