using NSubstitute;
using static Dexterity.Generation.Tests.Game;

namespace Dexterity.Generation.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public void Creation_Should_NotSnapshotUnrelatedComponents()
    {
        var g = new Game();
        var existing = Ok(g.World.Repository.Create(Character, 1));
        existing.Stats = null!;
        g.Create().Component.Id.ShouldBe(2);
        g.World.Repository.Items[existing.Id].ShouldBeSameAs(existing);
    }

    // MAP-21, NUM-01: custom numeric keys are not limited to the built-in modifier schema.
    [Fact]
    public void CustomModifierBindings_Should_AcceptSignedDecimalsAndKeepCustomKeys()
    {
        var g = new Game();
        var modifier = Substitute.For<IModifierType>();
        modifier.Id.Returns("custom");
        g.World.Modifiers.Add(modifier);
        g.Template.Items.Add(new GenerationValue { Id = "coefficient", Kind = GenerationValueKind.Fixed, Value = "-1.5" });
        g.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "custom" }, DataKeys = new() { ["coefficient"] = "coefficient" } });
        g.Component(g.Create()).ModifierData[0].Data["coefficient"].ShouldBe(-1.5);
        modifier.DidNotReceive().Calculate(Arg.Any<EffectResult>(), Arg.Any<ModifierData>(), Arg.Any<EffectContext>(), Arg.Any<int>());
    }

    // VAL-14: invalid definitions are rejected even in skipped branches.
    [Fact]
    public void SkippedBranches_Should_StillRequireValidDefinitions()
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationGroup
        {
            Id = "hidden", Conditions = new() { new() { Key = "absent", Value = "yes" } },
            Items = new() { new GenerationValue { Id = "invalid", WorthPerValue = 0 } }
        });
        Fail(g.Service.Create(g.Request(0)));
        g.Random.Calls.ShouldBeEmpty();
        g.World.Repository.Items.ShouldBeEmpty();
    }

    [Fact]
    public void ContextKeys_Should_StayOrdinalEvenWhenCallerDictionaryIsNot()
    {
        var g = new Game();
        g.Template.Conditions.Add(new GenerationCondition { Key = "Faction", Value = "orc" });
        var request = g.Request();
        request.Context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["faction"] = "orc" };
        Fail(g.Service.Create(request));
        request.Context.Single().Key.ShouldBe("faction");
    }

    [Theory]
    [InlineData(-1d, 0d)]
    [InlineData(5d, 6d)]
    public void InvalidInitialStats_Should_FailBeforeAllocatingAnIdentity(double max, double current)
    {
        var g = new Game();
        g.Template.AddFixedValue("max", max.ToString(System.Globalization.CultureInfo.InvariantCulture));
        g.Template.AddFixedValue("current", current.ToString(System.Globalization.CultureInfo.InvariantCulture));
        g.Template.StatBindings.Add(new StatBinding { StatTypeId = Durability, MaxKey = "max", CurrentKey = "current" });
        Fail(g.Service.Create(g.Request()));
        Ok(g.World.Repository.Create(Item, Sword)).Id.ShouldBe(1);
    }

    // WGT-14, WGT-21..22: structural budget-bearing status does not depend on later gates.
    [Fact]
    public void SkippedBudgetDescendants_Should_NotRedistributeAnAllocatedGroupShare()
    {
        var g = new Game();
        var group = g.Template.AddGroup("budget");
        var value = group.AddWeightValue("hidden", 1);
        value.Conditions.Add(new GenerationCondition { Key = "missing", Value = "yes" });
        g.Template.AddWeightValue("visible", 1);
        var report = g.Create(12).Generation;
        report.Values.ShouldBe(new Dictionary<string, string> { ["visible"] = "6" });
        (report.TotalWorth, report.UnusedWorth).ShouldBe((6, 6));
    }

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    public void MetadataGroupChances_Should_GateItsSubtreeWithoutTakingBudget(int draw, bool included)
    {
        var g = new Game();
        g.Template.AddGroup("metadata", 1, 99, 50).AddFixedValue("tier", "2");
        g.Template.AddWeightValue("damage", 1);
        g.Random.Values.Enqueue(draw);
        var report = g.Create(10).Generation;
        report.Values.ContainsKey("tier").ShouldBe(included);
        report.Values["damage"].ShouldBe("10");
        g.Random.Calls.ShouldBe(new[] { (0, 99) });
    }

    [Fact]
    public void MetadataOnlyTrees_Should_PreserveTheEntireRequestedBudget()
    {
        var g = new Game();
        g.Template.AddGroup("metadata", 0, 99).AddGroup("nested", 0).AddFixedValue("tier", "2");
        var report = g.Create(10).Generation;
        report.Values.ShouldBe(new Dictionary<string, string> { ["tier"] = "2" });
        (report.TotalWorth, report.UnusedWorth, report.Overspend).ShouldBe((0, 10, 0));
        g.Random.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void GeneratedBindingOverrides_Should_NotValidateDiscardedBaseValues()
    {
        var g = new Game();
        g.Template.AddFixedValue("max", "20");
        g.Template.Modifiers.Add(new ModifierTemplate
        {
            Base = new ModifierData { TypeId = "randomdecrease", EffectTypeId = 1, Data = new() { ["min"] = 10, ["max"] = 0 } },
            DataKeys = new() { ["max"] = "max" }
        });
        g.Component(g.Create()).ModifierData[0].Data["max"].ShouldBe(20);
    }

    [Fact]
    public void DeepTemplates_Should_GenerateWithoutRecursiveStackGrowth()
    {
        var g = new Game();
        GenerationNode node = g.Template;
        for (var index = 0; index < 10000; index++)
            node = node.AddGroup("group");
        node.AddWeightValue("value", 1);
        g.Create(10).Generation.Values["value"].ShouldBe("10");
    }
}
