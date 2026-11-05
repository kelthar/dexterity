using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static Dexterity.Generation.Tests.Game;
using static Dexterity.Generation.Tests.FluentTestSupport;

namespace Dexterity.Generation.Tests;

public sealed class FluentServiceTests
{
    /// <summary>GAT-07–14: context comparisons retain ordinal keys, AND semantics and strict numeric eligibility.</summary>
    [Fact]
    public void Conditions_read_only_request_context()
    {
        foreach (var op in Enum.GetValues<ConditionOperator>())
        {
            foreach (var value in new[] { "4", "5", "6", "5,0", "5e0", " 5", "NaN", null })
            {
                var game = new Game();
                game.Template.Group("g", 1, g => g.When("level", op, "5").Value("ok", x => x.Fixed("yes")));
                var request = game.Request();
                if (value != null)
                    request.Context["level"] = value;
                var included = value != null && op switch
                {
                    ConditionOperator.Equal => value == "5",
                    ConditionOperator.NotEqual => value != "5",
                    ConditionOperator.LessThan => value == "4",
                    ConditionOperator.LessOrEqual => value is "4" or "5",
                    ConditionOperator.GreaterThan => value == "6",
                    _ => value is "5" or "6"
                };
                Ok(game.Service.Create(request)).Generation.Values.ContainsKey("ok").ShouldBe(included);
                game.Random.Calls.ShouldBeEmpty();
            }
        }
        foreach (var tuple in new[] { ("rare", "4", false), ("common", "5", false), ("rare", "5", true) })
        {
            var game = new Game();
            game.Template.Group("g", 1, g => g.When("rarity", ConditionOperator.Equal, "rare").When("level", ConditionOperator.GreaterOrEqual, "5").Value("ok", x => x.Fixed("yes")));
            var request = game.Request();
            request.Context = new(StringComparer.OrdinalIgnoreCase) { ["rarity"] = tuple.Item1, ["level"] = tuple.Item2 };
            Ok(game.Service.Create(request)).Generation.Values.ContainsKey("ok").ShouldBe(tuple.Item3);
        }
        var root = new Game();
        root.Template.When("level", ConditionOperator.GreaterOrEqual, "5");
        Rejected(root, code: 409);
        var eligible = root.Request();
        eligible.Context["level"] = "5";
        Ok(root.Service.Create(eligible));
        eligible.Context = new(StringComparer.OrdinalIgnoreCase) { ["Level"] = "5" };
        root.Service.Create(eligible).Code.ShouldBe(409);
    }

    /// <summary>BND-01/02/04/09/11: named outputs bind stats and verbatim data independently of discovery tags.</summary>
    [Fact]
    public void Named_values_and_bindings_match_manual_weapon()
    {
        var fluent = new Game();
        var manual = new Game();
        fluent.Template.Tags("weapon", "melee").ComponentTags("crafted")
            .Value("durability", x => x.Weight(1)).Value("tier", x => x.Fixed("2"))
            .BindStat(Durability, "durability").BindData("tier", "tier");
        fluent.Template.EffectType(1).Decrease(x => x.Value.Weight(2, 3));
        manual.Template.Tags.AddRange(new[] { "weapon", "melee" });
        manual.Template.ComponentTags.Add("crafted");
        manual.Template.Items.Add(new GenerationValue { Id = "durability" });
        manual.Template.Items.Add(new GenerationValue { Id = "tier", Kind = GenerationValueKind.Fixed, WorthPerValue = 0, Value = "2" });
        manual.Template.Items.Add(new GenerationValue { Id = "fluent/0/value", WorthPerValue = 2, Weight = 3 });
        manual.Template.StatBindings.Add(new StatBinding { StatTypeId = Durability, MaxKey = "durability" });
        manual.Template.DataBindings.Add(new DataBinding { Key = "tier", ValueKey = "tier" });
        manual.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "decrease", EffectTypeId = 1, RelationKind = RelationKind.Enemies }, DataKeys = new() { ["value"] = "fluent/0/value" } });
        var result = Parity(fluent, manual, 40);
        result.Generation.Values["fluent/0/value"].ShouldBe("15");
        result.Component.Stats[Durability].Current.ShouldBe(10);
        result.Component.MetaData["tier"].ShouldBe("2");
        result.Component.Tags.ShouldBe(new[] { "crafted" });
        Ok(fluent.Service.Find(new TemplateFilter { Tags = new() { "WEAPON", "MELEE" } })).Single().ShouldBeSameAs(fluent.Template);
        var strings = new Game();
        foreach (var text in new[] { "", "1,5", "x y" })
        {
            var key = $"v{strings.Template.Items.Count}";
            strings.Template.Value(key, x => x.Fixed(text)).BindData(key, key);
        }
        strings.Create().Component.MetaData.Values.ShouldBe(new[] { "", "1,5", "x y" });
    }

    /// <summary>BND-03–06/10: optional outputs, invalid currents and duplicate destination guards are distinct.</summary>
    [Fact]
    public void Missing_bindings_and_initial_currents_follow_existing_rules()
    {
        var game = new Game();
        game.Template.Value("max", x => x.Fixed("10"))
            .Value("current", x => x.Fixed("4"))
            .BindStat(Durability, "max", "current");
        game.Create().Component.Stats[Durability].Current.ShouldBe(4);
        Should.Throw<InvalidOperationException>(() => game.Template.BindStat(Durability, "max"));
        game.Template.BindData("data", "max");
        Should.Throw<InvalidOperationException>(() => game.Template.BindData("data", "current"));
        foreach (var current in new[] { "11", "-1", null })
        {
            game = new Game();
            game.Template.Value("max", x => x.Fixed("10"));
            game.Template.Value("current", x => x.Fixed(current ?? "4", entry =>
            {
                if (current == null)
                    entry.When("missing", ConditionOperator.Equal, "yes");
            }));
            game.Template.BindStat(Durability, "max", "current");
            Rejected(game);
        }
        game = new Game();
        game.Template.Value("max", x => x.Fixed("10", entry => entry.When("missing", ConditionOperator.Equal, "yes")))
            .BindStat(Durability, "max").BindData("omitted", "max");
        var output = game.Create().Component;
        output.Stats[Durability].Max.ShouldBe(0);
        output.MetaData.ShouldBeEmpty();
    }

    /// <summary>BND-07/08: calculators operate after generation and current defaults use the derived maxima.</summary>
    [Fact]
    public void Calculated_maxima_and_current_defaults_are_retained()
    {
        var game = new Game();
        game.Template.TypeId = Character;
        game.Template.CategoryId = 1;
        game.Template.Value("strength", x => x.Fixed("5")).BindStat(Str, "strength")
            .Value("stun", x => x.Fixed("7")).BindStat(Stun, "stun");
        var calculator = Substitute.For<IStatCalculator>();
        calculator.StatTypeId.Returns(HP);
        calculator.ComponentTypeId.Returns(Character);
        calculator.Calculate(Arg.Any<int>(), game.World).Returns(Result.Ok(50d));
        game.World.Calculators.Add(calculator);
        var component = game.Create().Component;
        component.Stats[HP].Current.ShouldBe(50);
        component.Stats[Str].Current.ShouldBe(5);
        component.Stats[Stun].Current.ShouldBe(0);
        game = new Game();
        game.Template.Value("max", x => x.Fixed("10")).Value("current", x => x.Fixed("8")).BindStat(Durability, "max", "current");
        calculator = Substitute.For<IStatCalculator>();
        calculator.StatTypeId.Returns(Durability);
        calculator.ComponentTypeId.Returns(Item);
        calculator.Calculate(Arg.Any<int>(), game.World).Returns(Result.Ok(6d));
        game.World.Calculators.Add(calculator);
        game.Create().Component.Stats[Durability].Current.ShouldBe(6);
    }

    /// <summary>VAL-09/10/14–17, CAT-03: validation sees current malformed definitions, even in skipped branches.</summary>
    [Fact]
    public void Service_revalidates_direct_edits_before_randomness()
    {
        var corruptions = new Action<GenerationTemplate>[]
        {
            t => t.Id = "", t => t.Name = "", t => t.TypeId = 999, t => t.CategoryId = 999,
            t => t.Modifiers[0].Base.EffectTypeId = 999, t => t.Modifiers[0].Base.TypeId = "missing",
            t => t.Items.Add(t.Items[0]), t => ((GenerationValue)t.Items[0]).Kind = (GenerationValueKind)999,
            t => t.Items.Add(new GenerationValue { Id = "zero", Weight = 0 }),
            t => { var g = new GenerationGroup { Id = "cycle" }; g.Items.Add(g); t.Items.Add(g); },
            t => t.Conditions.Add(new GenerationCondition { Key = "level", Operator = ConditionOperator.GreaterThan, Value = "5,0" }),
            t => t.StatBindings.Add(new StatBinding { StatTypeId = HP, MaxKey = "fluent/0/value" }),
            t => t.DataBindings.Add(new DataBinding { Key = "data", ValueKey = "absent" })
        };
        foreach (var corrupt in corruptions)
        {
            var game = new Game();
            game.Template.EffectType(1).Decrease(x => x.Value.Random(1, 0, 1));
            Ok(game.Service.Get(game.Template.Id));
            corrupt(game.Template);
            Rejected(game);
            game.Random.Calls.ShouldBeEmpty();
        }
        foreach (var value in new[] { "5,0", "5e0" })
        {
            var game = new Game();
            game.Template.When("level", ConditionOperator.GreaterThan, value);
            Rejected(game);
        }
        var skipped = new Game();
        skipped.Template.Value("r", x => x.Random(1, 0, 1));
        skipped.Template.Group("g", 1, g => g.When("missing", ConditionOperator.Equal, "yes").Value("bad", x => x.Weight(1)));
        ((GenerationValue)((GenerationGroup)skipped.Template.Items[1]).Items[0]).WorthPerValue = 0;
        Rejected(skipped);
        skipped.Random.Calls.ShouldBeEmpty();
    }

    /// <summary>VAL-11–13, ISO-04/05: concrete invalid generated data consumes draws but does not allocate a component.</summary>
    [Fact]
    public void Concrete_failures_do_not_rewind_the_random_stream()
    {
        var game = new Game();
        game.Template.Value("r", x => x.Random(1, 0, 1));
        game.Template.EffectType(1).Critical(x => x.Chance.Fixed(101).Multiplier.Fixed(2));
        game.Random.Values.Enqueue(1);
        Rejected(game, 1);
        game.Random.Calls.ShouldBe(new[] { (0, 1) });
        ((GenerationValue)game.Template.Items[1]).Value = "25";
        game.Random.Values.Enqueue(0);
        game.Create(1).Generation.Values["r"].ShouldBe("0");
        game.Random.Calls.Count.ShouldBe(2);
        game = new Game();
        game.Template.EffectType(1).RandomDecrease(x => x.Min.Fixed(5).Max.Random(1, 1, 3));
        game.Random.Values.Enqueue(3);
        Rejected(game);
        game = new Game();
        game.Template.EffectType(1).PreventPercent(x => x.Percent.Random(1, 100, 101));
        game.Random.Values.Enqueue(101);
        Rejected(game, 101);
        game = new Game();
        game.Template.Value("random", x => x.Random(1, 20, 30));
        game.Random.Values.Enqueue(31);
        Rejected(game, code: 500);
    }

    /// <summary>VAL-16/17: invalid requests and binding identities fail through the existing Result contract.</summary>
    [Fact]
    public void Invalid_requests_and_binding_sources_are_rejected()
    {
        foreach (var mode in Enumerable.Range(0, 4))
        {
            var game = new Game();
            var request = game.Request();
            if (mode == 0)
                request.Worth = -1;
            else if (mode == 1)
                request.TemplateId = "missing";
            else if (mode == 2)
                request.Context = null!;
            else
                request.Context[""] = "x";
            var result = game.Service.Create(request);
            result.Failed(out _).ShouldBeTrue();
            result.Code.ShouldBe(mode == 1 ? 404 : 400);
            game.Random.Calls.ShouldBeEmpty();
            game.World.Repository.Items.ShouldBeEmpty();
        }
        var invalid = new Game();
        invalid.Template.Value("current", x => x.Fixed("4"));
        invalid.Template.StatBindings.Add(new StatBinding { StatTypeId = Durability, MaxKey = "", CurrentKey = "current" });
        Rejected(invalid);
    }

    /// <summary>ISO-02/03/07: seeds are request-local, unseeded streams advance and successful creation preserves prior objects.</summary>
    [Fact]
    public void Seeded_creation_and_unseeded_streams_remain_separate()
    {
        var game = new Game();
        game.Template.EffectType(1).Critical(x => x.Chance.Random(1, 20, 30).Multiplier.Weight(10, 3));
        var before = Ok(game.World.Repository.Create(Character, 1));
        var first = Ok(game.Service.Create(game.Request(120, 42)));
        var second = Ok(game.Service.Create(game.Request(120, 42)));
        first.Generation.Values.ShouldBe(second.Generation.Values);
        game.Random.Calls.ShouldBeEmpty();
        game.World.Repository.Items[before.Id].ShouldBeSameAs(before);
        game.Random.Values.Enqueue(21);
        game.Random.Values.Enqueue(29);
        game.Create(120).Generation.TotalWorth.ShouldBe(111);
        game.Create(120).Generation.TotalWorth.ShouldBe(119);
        game.Random.Calls.Count.ShouldBe(2);
        game.World.Repository.Relations.ShouldBeEmpty();
    }

    /// <summary>CAT-01/02/04, PAR-02: discovery, reference identity and post-create edits work through existing DI registration.</summary>
    [Fact]
    public void Catalog_references_remain_live_through_service_registration()
    {
        var game = new Game();
        game.Template.Tags("weapon", "melee");
        game.Catalog.Templates.Add(new GenerationTemplate { Id = "other", Name = "Other", TypeId = Item, CategoryId = Sword, Tags = new() { "weapon" } });
        var services = new ServiceCollection();
        services.AddSingleton(game.World).AddSingleton(game.Catalog).AddSingleton<IGenerationRandom>(game.Random);
        Dexterity.Generation.ServiceExtensions.Register(services);
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IGenerationService>();
        Ok(service.Find(new TemplateFilter { TypeId = Item, CategoryId = Sword, Tags = new() { "WEAPON", "MELEE" } })).Single().ShouldBeSameAs(game.Template);
        Ok(service.Find(new TemplateFilter { Tags = new() { "absent" } })).ShouldBeEmpty();
        Ok(service.Get(game.Template.Id)).ShouldBeSameAs(game.Template);
        var first = game.Create();
        game.Template.Value("tier", x => x.Fixed("2")).BindData("tier", "tier");
        var second = Ok(service.Create(game.Request()));
        second.Component.MetaData["tier"].ShouldBe("2");
        first.Component.MetaData.ShouldBeEmpty();
        first.Generation.Values.ShouldBeEmpty();
    }
}
