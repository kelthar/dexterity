using static Dexterity.Generation.Tests.Game;
using static Dexterity.Generation.Tests.FluentTestSupport;

namespace Dexterity.Generation.Tests;

public sealed class FluentGenerationTests
{
    /// <summary>E2E-01–03, ISO-01–03: random chance is budget-capped and accounting matches the manual definition.</summary>
    [Theory]
    [InlineData(120, 27, 9, 117, 3, 0)]
    [InlineData(80, 20, 6, 80, 0, 0)]
    [InlineData(0, 20, 0, 20, 0, 20)]
    public void Critical_budget_matches_manual(int worth, int chance, int multiplier, int total, int unused, int overspend)
    {
        var fluent = new Game();
        var manual = new Game();
        fluent.Template.EffectType(1).Attack().Critical(x => x.Chance.Random(1, 20, 30).Multiplier.Weight(10, 3));
        var chanceLeaf = new GenerationValue { Id = "fluent/0/chance", Kind = GenerationValueKind.Random, MinValue = 20, MaxValue = 30 };
        var multiplierLeaf = new GenerationValue { Id = "fluent/0/multiplier", WorthPerValue = 10, Weight = 3 };
        manual.Template.Items.AddRange(new[] { chanceLeaf, multiplierLeaf });
        manual.Template.Modifiers.Add(new ModifierTemplate
        {
            Base = new ModifierData { TypeId = "critical", EffectTypeId = 1, Scope = Scope.Use, Phase = Phase.Outgoing, RelationKind = RelationKind.Enemies },
            DataKeys = new() { ["chance"] = chanceLeaf.Id, ["multiplier"] = multiplierLeaf.Id }
        });
        Ok(fluent.Service.Validate());
        Ok(fluent.Service.Get(fluent.Template.Id)).ShouldBeSameAs(fluent.Template);
        Ok(fluent.Service.Find(new TemplateFilter())).Single().ShouldBeSameAs(fluent.Template);
        fluent.Random.Calls.ShouldBeEmpty();
        var report = Parity(fluent, manual, worth, 27).Generation;
        report.Values["fluent/0/chance"].ShouldBe(chance.ToString());
        report.Values["fluent/0/multiplier"].ShouldBe(multiplier.ToString());
        (report.TotalWorth, report.UnusedWorth, report.Overspend).ShouldBe((total, unused, overspend));
    }

    /// <summary>E2E-04/05, BUD-01: explicit group boundaries determine shares without implicit modifier groups.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Group_shapes_preserve_budget_boundaries(int variant)
    {
        var fluent = new Game();
        var manual = new Game();
        fluent.Template.Group("offense", 3, offense => offense.EffectType(1, effect =>
        {
            effect.Attack().Decrease(x => x.Value.Weight(2, 5));
            if (variant == 0)
                effect.Critical(x => x.Chance.Random(1, 20, 30).Multiplier.Weight(10, 3));
            else
                effect.Group("critical", 2, group => group.Critical(x =>
                {
                    if (variant == 1)
                        x.Chance.Random(1, 20, 30);
                    else
                        x.Chance.Fixed(25);
                    x.Multiplier.Weight(10, variant == 1 ? 3 : 1);
                }));
        }));
        var offense = new GenerationGroup { Id = "offense", Weight = 3 };
        manual.Template.Items.Add(offense);
        offense.Items.Add(new GenerationValue { Id = "fluent/0/value", WorthPerValue = 2, Weight = 5 });
        manual.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "decrease", EffectTypeId = 1, RelationKind = RelationKind.Enemies }, DataKeys = new() { ["value"] = "fluent/0/value" } });
        GenerationNode critical = offense;
        if (variant != 0)
        {
            critical = new GenerationGroup { Id = "critical", Weight = 2 };
            offense.Items.Add((GenerationGroup)critical);
        }
        critical.Items.Add(variant == 2
            ? new GenerationValue { Id = "fluent/1/chance", Kind = GenerationValueKind.Fixed, WorthPerValue = 0, Value = "25" }
            : new GenerationValue { Id = "fluent/1/chance", Kind = GenerationValueKind.Random, MinValue = 20, MaxValue = 30 });
        critical.Items.Add(new GenerationValue { Id = "fluent/1/multiplier", WorthPerValue = 10, Weight = variant == 2 ? 1 : 3 });
        manual.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "critical", EffectTypeId = 1, RelationKind = RelationKind.Enemies }, DataKeys = new() { ["chance"] = "fluent/1/chance", ["multiplier"] = "fluent/1/multiplier" } });
        if (variant == 2)
        {
            fluent.Template.Group("defense", 1, g => g.EffectType(1).Defend().PreventFlat(x => x.Amount.Weight(1)));
            var defense = new GenerationGroup { Id = "defense" };
            manual.Template.Items.Add(defense);
            defense.Items.Add(new GenerationValue { Id = "fluent/2/amount" });
            manual.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "preventflat", EffectTypeId = 1, Scope = Scope.Passive, Phase = Phase.Incoming, RelationKind = RelationKind.Enemies }, DataKeys = new() { ["amount"] = "fluent/2/amount" } });
        }
        var result = Parity(fluent, manual, variant == 0 ? 90 : 280, variant == 2 ? Array.Empty<int>() : new[] { 27 }).Generation;
        result.Values["fluent/0/value"].ShouldBe(variant == 0 ? "25" : variant == 1 ? "100" : "75");
        result.Values["fluent/1/multiplier"].ShouldBe(variant == 0 ? "3" : "6");
        result.TotalWorth.ShouldBe(variant == 0 ? 100 : 280);
        result.Overspend.ShouldBe(variant == 0 ? 10 : 0);
    }

    /// <summary>GAT-01–04/15/16: fixed-only group inclusion still controls its bound modifier without variation draws.</summary>
    [Theory]
    [InlineData(50, 49, "rare", true)]
    [InlineData(50, 50, "rare", false)]
    [InlineData(50, -1, "common", false)]
    [InlineData(50, -1, "Rare", false)]
    [InlineData(50, -1, null, false)]
    [InlineData(0, -1, "rare", true)]
    [InlineData(100, -1, "rare", true)]
    [InlineData(1, 0, "rare", true)]
    [InlineData(1, 1, "rare", false)]
    [InlineData(99, 98, "rare", true)]
    [InlineData(99, 99, "rare", false)]
    public void Fixed_group_gates_match_manual(int chance, int sample, string? rarity, bool included)
    {
        var fluent = new Game();
        var manual = new Game();
        fluent.Template.Group("critical-package", 2, group =>
        {
            group.Order(1).VaryWeightBy(2).IncludeChance(chance).When("rarity", ConditionOperator.Equal, "rare");
            group.EffectType(1).Attack().Critical(x => x.Chance.Fixed(25).Multiplier.Fixed(2));
        });
        var group = new GenerationGroup
        {
            Id = "critical-package", Weight = 2, Order = 1, VaryWeightBy = 2, ChanceToGet = chance,
            Conditions = new() { new GenerationCondition { Key = "rarity", Operator = ConditionOperator.Equal, Value = "rare" } }
        };
        manual.Template.Items.Add(group);
        Manual(manual.Template, group, "critical");
        var request = fluent.Request(10);
        var other = manual.Request(10);
        if (rarity != null)
        {
            request.Context["rarity"] = rarity;
            other.Context["rarity"] = rarity;
        }
        Snapshot(fluent.Template).ShouldBe(Snapshot(manual.Template));
        if (sample >= 0)
        {
            fluent.Random.Values.Enqueue(sample);
            manual.Random.Values.Enqueue(sample);
        }
        var actual = Ok(fluent.Service.Create(request));
        var expected = Ok(manual.Service.Create(other));
        actual.Generation.Values.ShouldBe(expected.Generation.Values);
        actual.Component.ModifierData.Count.ShouldBe(included ? 1 : 0);
        actual.Generation.Values.Count.ShouldBe(included ? 2 : 0);
        actual.Generation.TotalWorth.ShouldBe(0);
        actual.Generation.UnusedWorth.ShouldBe(10);
        fluent.Random.Calls.ShouldBe(sample < 0 ? Array.Empty<(int, int)>() : new[] { (0, 99) });
    }

    /// <summary>BUD-02/03/07: largest remainders obey explicit order and retain unit-cost remainders.</summary>
    [Theory]
    [InlineData(1, 1, false, "1", "0", 1)]
    [InlineData(1, 1, true, "0", "1", 1)]
    [InlineData(10, 3, false, "1", "1", 6)]
    public void Integer_shares_preserve_order_and_unused_worth(int worth, int cost, bool reverse, string a, string b, int total)
    {
        var fluent = new Game();
        var manual = new Game();
        fluent.Template.Value("a", x => x.Weight(cost, configure: entry => entry.Order(reverse ? 2 : 0)));
        fluent.Template.Value("b", x => x.Weight(cost, configure: entry => entry.Order(reverse ? 1 : 0)));
        manual.Template.Items.Add(new GenerationValue { Id = "a", WorthPerValue = cost, Order = reverse ? 2 : 0 });
        manual.Template.Items.Add(new GenerationValue { Id = "b", WorthPerValue = cost, Order = reverse ? 1 : 0 });
        var report = Parity(fluent, manual, worth).Generation;
        report.Values["a"].ShouldBe(a);
        report.Values["b"].ShouldBe(b);
        report.TotalWorth.ShouldBe(total);
        report.UnusedWorth.ShouldBe(worth - total);
    }

    /// <summary>BUD-04/10/11/15/16: samples, integer limits and generated overflow retain exact contracts.</summary>
    [Fact]
    public void Random_order_bounds_and_overflow_are_preserved()
    {
        var game = new Game();
        game.Template.EffectType(1).Critical(x => x.Multiplier.Random(1, 2, 3).Chance.Random(1, 20, 30));
        game.Random.Values.Enqueue(3);
        game.Random.Values.Enqueue(27);
        game.Create(60).Generation.TotalWorth.ShouldBe(30);
        game.Random.Calls.ShouldBe(new[] { (2, 3), (20, 30) });
        foreach (var sample in new[] { 20, 30 })
        {
            game = new Game();
            game.Template.Value("v", x => x.Random(1, 20, 30));
            game.Random.Values.Enqueue(sample);
            game.Create(30).Generation.Values["v"].ShouldBe(sample.ToString());
        }
        game = new Game();
        game.Template.Value("v", x => x.Random(1, 7, 7));
        game.Create(0).Generation.Overspend.ShouldBe(7);
        game.Random.Calls.ShouldBeEmpty();
        game = new Game();
        game.Template.Value("v", x => x.Weight(1, int.MaxValue));
        game.Create(int.MaxValue).Generation.TotalWorth.ShouldBe(int.MaxValue);
        game = new Game();
        game.Template.Value("a", x => x.Random(1, int.MaxValue, int.MaxValue));
        game.Template.Value("b", x => x.Random(1, int.MaxValue, int.MaxValue));
        Rejected(game, 0);
    }

    /// <summary>BUD-05/06/08/09 and GAT-05: empty/fixed/skipped descendants have distinct allocation rules.</summary>
    [Fact]
    public void Groups_do_not_redistribute_unused_shares_or_overspend()
    {
        foreach (var emptyGroup in new[] { false, true })
        {
            var empty = new Game();
            if (emptyGroup)
                empty.Template.Group("empty", 1, _ => { });
            empty.Create(10).Generation.Values.ShouldBeEmpty();
            empty.Random.Calls.ShouldBeEmpty();
        }
        for (var mode = 0; mode < 4; mode++)
        {
            var game = new Game();
            game.Template.Group("A", 1, group =>
            {
                if (mode == 0)
                    group.VaryWeightBy(99).EffectType(1).Critical(x => x.Chance.Fixed(25).Multiplier.Fixed(2));
                else if (mode == 1)
                    group.Value("a", x => x.Random(2, 4, 4));
                else if (mode == 2)
                    group.Value("a", x => x.Weight(1, configure: entry => entry.When("missing", ConditionOperator.Equal, "yes")));
                else
                    group.IncludeChance(50).Value("a", x => x.Random(1, 2, 3, configure: entry => entry.IncludeChance(50).VaryWeightBy(2)));
            });
            game.Template.Group("B", 1, group => group.Value("b", x => x.Weight(mode == 1 ? 3 : 1)));
            if (mode == 3)
                game.Random.Values.Enqueue(50);
            var report = game.Create(10).Generation;
            report.Values["b"].ShouldBe(mode is 0 or 3 ? "10" : mode == 1 ? "1" : "5");
            report.TotalWorth.ShouldBe(mode is 0 or 3 ? 10 : mode == 1 ? 11 : 5);
            game.Random.Calls.Count.ShouldBe(mode == 3 ? 1 : 0);
        }
    }

    /// <summary>BUD-12/13/14, ENT-01: variation, gate phases and deep nesting do not change the engine's order.</summary>
    [Fact]
    public void Traversal_and_variation_keep_exact_draw_order()
    {
        var game = new Game();
        game.Template.Group("a", 1, g => g.VaryWeightBy(2).Value("a", x => x.Weight(1)));
        game.Template.Group("b", 1, g => g.VaryWeightBy(2).Value("b", x => x.Weight(1)));
        game.Random.Values.Enqueue(0);
        game.Random.Values.Enqueue(2);
        var report = game.Create(12).Generation;
        report.Values["a"].ShouldBe("3");
        report.Values["b"].ShouldBe("9");
        game = new Game();
        game.Template.Group("a", 1, g => g.IncludeChance(50).Value("a", x => x.Random(1, 2, 3)));
        game.Template.Value("b", x => x.Random(1, 4, 5, configure: entry => entry.IncludeChance(50)));
        foreach (var sample in new[] { 1, 2, 3, 5 })
            game.Random.Values.Enqueue(sample);
        game.Create(12).Generation.TotalWorth.ShouldBe(8);
        game.Random.Calls.ShouldBe(new[] { (0, 99), (0, 99), (2, 3), (4, 5) });
        game = new Game();
        game.Template.Group("root", 1, g => Deep(g, 127));
        game.Create(7).Generation.Values["deep"].ShouldBe("7");

        game = new Game();
        game.Template.EffectType(1).Multiplier(x => x.Multiplier.Weight(10, 3, entry => entry.Order(2).VaryWeightBy(1).IncludeChance(75).When("level", ConditionOperator.GreaterOrEqual, "5")));
        game.Random.Values.Enqueue(74);
        game.Random.Values.Enqueue(1);
        var request = game.Request(40);
        request.Context["level"] = "5";
        Ok(game.Service.Create(request)).Generation.TotalWorth.ShouldBe(40);
        game.Random.Calls.ShouldBe(new[] { (0, 99), (0, 1) });
    }

    /// <summary>Authors a bounded deep tree to exercise nested callbacks without adding runtime logic to tests.</summary>
    private static void Deep(GenerationGroupBuilder group, int depth)
    {
        if (depth == 0)
        {
            group.Value("deep", x => x.Weight(1));
            return;
        }
        group.Group($"level{depth}", 1, child => Deep(child, depth - 1));
    }

    /// <summary>DUR-02/03 and GAT-06: missing bound inputs omit a modifier but retain other leaf cost.</summary>
    [Fact]
    public void Missing_parameter_and_duration_outputs_omit_whole_modifiers()
    {
        var game = new Game();
        game.Template.EffectType(1).Decrease(x => x.Value.Fixed(10));
        game.Template.Modifiers.Single().DurationKey.ShouldBeNull();
        game.Create(10).Component.ModifierData.Single().Duration.ShouldBe(0);
        game = new Game();
        game.Template.EffectType(1).Decrease(x => x.Value.Fixed(10).Duration.Fixed(2, entry => entry.When("missing", ConditionOperator.Equal, "yes")));
        var result = game.Create(10);
        result.Component.ModifierData.ShouldBeEmpty();
        result.Generation.Values.Count.ShouldBe(1);
        game = new Game();
        game.Template.EffectType(1).Critical(x => x.Chance.Fixed(25, entry => entry.When("level", ConditionOperator.Equal, "5")).Multiplier.Weight(10));
        result = game.Create(40);
        result.Component.ModifierData.ShouldBeEmpty();
        result.Generation.Values["fluent/0/multiplier"].ShouldBe("4");
        result.Generation.TotalWorth.ShouldBe(40);
    }
}
