using static Dexterity.Generation.Tests.Game;

namespace Dexterity.Generation.Tests;

public sealed partial class AcceptanceTests
{
    // WGT-01, MAP-01..03, CRT-01..05, MAP-17, MAP-20, MAP-22, CRT-19, DET-08.
    [Fact]
    public void Sword_Should_GenerateBoundStatsAndModifiersWithoutApplyingEffects()
    {
        var g = new Game();
        g.SwordTemplate();
        var result = g.Create();
        var report = result.Generation;
        report.Values.ShouldBe(new Dictionary<string, string> { ["damage"] = "15", ["durability"] = "10", ["tier"] = "2" });
        (report.RequestedWorth, report.TotalWorth, report.UnusedWorth, report.Overspend).ShouldBe((40, 40, 0, 0));
        var component = g.Component(result);
        (component.Name, component.TypeId, component.CategoryId, component.State).ShouldBe(("Longsword", Item, Sword, ComponentState.Active));
        component.Tags.ShouldBe(new[] { "crafted" });
        component.MetaData.ShouldBe(new Dictionary<string, string> { ["tier"] = "2" });
        var stat = component.Stats[Durability];
        (stat.Max, stat.ModifiedMax, stat.Current, stat.Bonus).ShouldBe((10d, 10d, 10d, 0d));
        component.Stats[Charges].Current.ShouldBe(0);
        component.ModifierData.Single().Data["value"].ShouldBe(15);
        component.Effects.ShouldBeEmpty();
        g.World.Repository.Relations.ShouldBeEmpty();
        g.World.Repository.Items.Count.ShouldBe(1);
        result.Component.ShouldBeSameAs(component);
        component.ModifierData[0].Data["value"] = 99;
        component.MetaData["tier"] = "other";
        component.Tags.Add("changed");
        report.Values["damage"].ShouldBe("15");
        report.Values["tier"].ShouldBe("2");
        g.Template.ComponentTags.ShouldBe(new[] { "crafted" });
        g.Template.Modifiers[0].Base.Data.ShouldBeEmpty();
    }

    // WGT-02..05, WGT-08..11, WGT-17, VAL-15: exact allocation and no redistribution.
    [Theory]
    [InlineData(5, 1, 1, 1, 1, "3", "2", 5)]
    [InlineData(5, 1, 2, 1, 1, "2", "3", 5)]
    [InlineData(5, 1, 1, 3, 1, "1", "2", 5)]
    [InlineData(4, 1, 1, 3, 1, "0", "2", 2)]
    [InlineData(0, 1, 1, 1, 1, "0", "0", 0)]
    [InlineData(2147483647, 2147483647, 2147483647, 1, 1, "1073741824", "1073741823", 2147483647)]
    public void WeightedValues_Should_UseExactSharesAndReportUnusedWorth(int worth, int firstWeight, int secondWeight, int firstCost, int secondCost, string first, string second, int total)
    {
        var g = new Game();
        g.Template.Items = new()
        {
            new GenerationValue { Id = "a", Weight = firstWeight, WorthPerValue = firstCost },
            new GenerationValue { Id = "b", Weight = secondWeight, WorthPerValue = secondCost }
        };
        var report = g.Create(worth).Generation;
        report.Values["a"].ShouldBe(first);
        report.Values["b"].ShouldBe(second);
        report.TotalWorth.ShouldBe(total);
        report.UnusedWorth.ShouldBe(worth - total);
        report.Overspend.ShouldBe(0);
        g.Random.Calls.ShouldBeEmpty();
    }

    // WGT-04, ORD-01..02.
    [Fact]
    public void RemainderTies_Should_UseOrderBeforeListPosition()
    {
        var g = new Game();
        g.Template.Items = new() { new GenerationValue { Id = "a", Order = 2 }, new GenerationValue { Id = "b", Order = -1 } };
        var values = g.Create(5).Generation.Values;
        values["a"].ShouldBe("2");
        values["b"].ShouldBe("3");
    }

    // WGT-13, RND-07: groups cost only their generated leaves.
    [Fact]
    public void NestedGroups_Should_CountLeafCostsExactlyOnce()
    {
        var g = new Game();
        g.Template.Items = new()
        {
            new GenerationGroup { Id = "offence", Weight = 3, Items = new()
            {
                new GenerationValue { Id = "physical", Weight = 2, WorthPerValue = 2 },
                new GenerationValue { Id = "fire" }
            } },
            new GenerationValue { Id = "defence" }
        };
        var report = g.Create().Generation;
        report.Values.ShouldBe(new Dictionary<string, string> { ["physical"] = "10", ["fire"] = "10", ["defence"] = "10" });
        report.TotalWorth.ShouldBe(40);
    }

    // WGT-12, WGT-15..16, WGT-18..24, FIX-01..03.
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 10)]
    [InlineData(true, 10)]
    public void MetadataGroups_Should_EmitFixedValuesWithoutTakingBudget(bool nested, int worth)
    {
        var g = new Game();
        var metadata = new GenerationGroup { Id = "metadata", Weight = 0, VaryWeightBy = 99 };
        var target = nested ? new GenerationGroup { Id = "nested", Weight = 0 } : metadata;
        target.Items.Add(new GenerationValue { Id = "tier", Kind = GenerationValueKind.Fixed, Value = "", Weight = 5, VaryWeightBy = 4, WorthPerValue = 8 });
        if (nested)
            metadata.Items.Add(target);
        g.Template.Items = new() { metadata, new GenerationValue { Id = "damage" } };
        var report = g.Create(worth).Generation;
        report.Values["damage"].ShouldBe(worth.ToString());
        report.Values["tier"].ShouldBe("");
        report.TotalWorth.ShouldBe(worth);
        g.Random.Calls.ShouldBeEmpty();
    }

    // RES-03, WGT-12, WGT-16, WGT-21.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyTemplates_Should_CreateOneComponentAndReportUnspentBudget(bool group)
    {
        var g = new Game();
        if (group)
            g.Template.Items.Add(new GenerationGroup { Id = "empty", Weight = 0 });
        var report = g.Create(10).Generation;
        report.Values.ShouldBeEmpty();
        (report.TotalWorth, report.UnusedWorth, report.Overspend).ShouldBe((0, 10, 0));
        g.World.Repository.Items.Count.ShouldBe(1);
    }

    // RND-01..06, RND-08.
    [Theory]
    [InlineData(20, 2, 5, 1, 2, 2, 0)]
    [InlineData(20, 2, 5, 1, 5, 5, 0)]
    [InlineData(8, 1, 10, 2, 10, 4, 0)]
    [InlineData(8, 3, 5, 4, 5, 3, 4)]
    [InlineData(0, 3, 5, 4, 5, 3, 12)]
    [InlineData(0, 3, 3, 4, 3, 3, 12)]
    public void RandomMinimums_Should_OverrideAffordabilityAndReportOverspend(int worth, int min, int max, int cost, int sample, int expected, int overspend)
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationValue { Id = "random", Kind = GenerationValueKind.Random, MinValue = min, MaxValue = max, WorthPerValue = cost });
        g.Random.Values.Enqueue(sample);
        var report = g.Create(worth).Generation;
        report.Values["random"].ShouldBe(expected.ToString());
        report.TotalWorth.ShouldBe(expected * cost);
        report.Overspend.ShouldBe(overspend);
        report.UnusedWorth.ShouldBe(Math.Max(0, worth - expected * cost));
        g.Random.Calls.Count.ShouldBe(min == max ? 0 : 1);
        if (min != max)
            g.Random.Calls[0].ShouldBe((min, max));
    }

    // RND-07..08, WGT-17.
    [Fact]
    public void Overspend_Should_NotReduceSiblingSharesOrReportLocalRemainders()
    {
        var g = new Game();
        g.Template.Items = new()
        {
            new GenerationGroup { Id = "group", Items = new() { new GenerationValue { Id = "a", Kind = GenerationValueKind.Random, MinValue = 4, MaxValue = 4, WorthPerValue = 2 } } },
            new GenerationValue { Id = "b", WorthPerValue = 3 }
        };
        var report = g.Create(10).Generation;
        report.Values["a"].ShouldBe("4");
        report.Values["b"].ShouldBe("1");
        (report.TotalWorth, report.UnusedWorth, report.Overspend).ShouldBe((11, 0, 1));
    }

    // CHN-01..05, WGT-14, WGT-22, ORD-03..05, WGT-06..07, DET-04.
    [Fact]
    public void RandomDraws_Should_RunGatesThenVariationThenDepthFirstValues()
    {
        var g = new Game();
        g.Template.Items = new()
        {
            new GenerationGroup { Id = "group", ChanceToGet = 30, VaryWeightBy = 3, Items = new()
            {
                new GenerationValue { Id = "nested", Kind = GenerationValueKind.Random, MinValue = 2, MaxValue = 5, ChanceToGet = 50 }
            } },
            new GenerationValue { Id = "sibling", ChanceToGet = 30, VaryWeightBy = 2, Kind = GenerationValueKind.Random, MinValue = 1, MaxValue = 6 }
        };
        foreach (var value in new[] { 29, 29, 3, 2, 49, 5, 6 })
            g.Random.Values.Enqueue(value);
        var report = g.Create(40).Generation;
        report.Values["nested"].ShouldBe("5");
        report.Values["sibling"].ShouldBe("6");
        g.Random.Calls.ShouldBe(new[] { (0, 99), (0, 99), (0, 3), (0, 2), (0, 99), (2, 5), (1, 6) });
        g.Random.Values.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(100, 0, true)]
    [InlineData(30, 29, true)]
    [InlineData(30, 30, false)]
    public void Chance_Should_UseStrictThresholdAndPreserveAlwaysSentinels(int chance, int draw, bool included)
    {
        var g = new Game();
        g.Template.Items = new() { new GenerationValue { Id = "a", ChanceToGet = chance }, new GenerationValue { Id = "b" } };
        g.Random.Values.Enqueue(draw);
        var report = g.Create(10).Generation;
        report.Values.ContainsKey("a").ShouldBe(included);
        report.Values["b"].ShouldBe(included ? "5" : "10");
        g.Random.Calls.Count.ShouldBe(chance is 0 or 100 ? 0 : 1);
    }
}
