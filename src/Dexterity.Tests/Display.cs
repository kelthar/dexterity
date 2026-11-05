using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Dexterity.Tests;

public sealed class DisplayTests
{
    private readonly IRandomSource _random = Substitute.For<IRandomSource>();
    private readonly World _world;
    private readonly IDisplayService _service;

    public DisplayTests()
    {
        _world = new World
        {
            Stats = new() { new() { Id = 1, Name = "HP" } },
            EffectTypes = new()
            {
                new() { Id = 1, Name = "Physical", StatTypeId = 1 },
                new() { Id = 2, Name = "Heal", StatTypeId = 1 }
            }
        };
        _world.Modifiers = new()
        {
            new Decrease(), new Increase(), new RandomDecrease(_random), new RandomIncrease(_random),
            new Multiplier(), new Critical(_random), new Splash(_world), new PreventFlat(), new PreventPercent(), new Steal()
        };
        _service = new DisplayService(_world, new Dictionary<int, EffectDisplayText>
        {
            [1] = new() { Quantity = "damage", Decrease = "{amount} dmg" },
            [2] = new() { Increase = "restore {amount} HP" }
        });
    }

    public static TheoryData<string, string, string> BuiltIns => new()
    {
        { "decrease", "Physical 10", "Physical 10 (10 dmg)" },
        { "increase", "Physical 10", "Physical 10 (increase HP by 10)" },
        { "randomdecrease", "Physical 2–6", "Physical 2–6 (random 2–6 dmg)" },
        { "randomincrease", "Physical 2–6", "Physical 2–6 (random increase HP by 2–6)" },
        { "multiplier", "Physical: ×2", "Physical: multiply damage by 2" },
        { "critical", "Physical: 25% crit ×2", "Physical: 25% chance to multiply damage by 2" },
        { "preventflat", "Physical: prevent 3", "Physical: prevent 3 damage" },
        { "preventpercent", "Physical: prevent 50%", "Physical: prevent 50% of damage" },
        { "splash", "Physical: 50% splash, range 3", "Physical: splash 50% of damage within range 3" },
        { "steal", "Physical: steal 50%", "Physical: restore 50% of actual damage lost by the target" }
    };

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void Describes_all_built_in_operations(string type, string shortText, string longText)
    {
        var data = Data(type);
        var display = Get(_service.Describe(_world.Modifiers.Single(x => x.Id == type), data));
        display.Short.ShouldBe(shortText);
        display.Long.ShouldBe(longText);
    }

    [Fact]
    public void Combines_base_and_random_damage_and_keeps_heal_separate()
    {
        var flat = Data("decrease");
        flat.Data["value"] = 1;
        var heal = Data("increase");
        heal.EffectTypeId = 2;
        var display = Get(_service.Describe(new[] { flat, Data("randomdecrease"), heal }));
        display.Short.ShouldBe("Physical 3–7; Heal 10");
        display.Long.ShouldBe("Physical 3–7 (base 1 dmg, random 2–6 dmg); Heal 10 (restore 10 HP)");
    }

    [Theory]
    [InlineData("direction")]
    [InlineData("effect")]
    [InlineData("phase")]
    [InlineData("scope")]
    [InlineData("duration")]
    [InlineData("relation")]
    [InlineData("include")]
    [InlineData("exclude")]
    public void Does_not_merge_different_applicability(string difference)
    {
        var first = Data("decrease");
        var second = Data("decrease");
        switch (difference)
        {
            case "direction": second.TypeId = "increase"; break;
            case "effect": second.EffectTypeId = 2; break;
            case "phase":
                first.Scope = second.Scope = Scope.Passive;
                second.Phase = Phase.Incoming;
                break;
            case "scope": second.Scope = Scope.Passive; break;
            case "duration": second.Duration = 3; break;
            case "relation": second.RelationKind = RelationKind.Enemies; break;
            case "include": second.IncludedTags.Add("sword"); break;
            case "exclude": second.ExcludedTags.Add("sword"); break;
        }
        var display = Get(_service.Describe(new[] { first, second }));
        display.Short.ShouldContain("; ");
        display.Short.ShouldNotContain("20");
    }

    [Fact]
    public void Tags_are_sets_and_all_qualifiers_are_visible_in_both_summaries()
    {
        var first = Data("decrease");
        var second = Data("randomdecrease");
        foreach (var data in new[] { first, second })
        {
            data.Scope = Scope.Passive;
            data.Phase = Phase.Incoming;
            data.Duration = 2.5;
            data.RelationKind = RelationKind.Self | RelationKind.Friends;
            data.ExcludedTags.Add("immune");
        }
        first.IncludedTags.AddRange(new[] { "sword", "item", "sword" });
        second.IncludedTags.AddRange(new[] { "item", "sword" });
        var display = Get(_service.Describe(new[] { first, second }));
        display.Short.ShouldStartWith("Physical 12–16 [");
        foreach (var summary in new[] { display.Short, display.Long })
        {
            summary.ShouldContain("passive");
            summary.ShouldContain("incoming");
            summary.ShouldContain("for 2.5 time units");
            summary.ShouldContain("targets: Self, Friends");
            summary.ShouldContain("includes tags: item, sword");
            summary.ShouldContain("excludes tags: immune");
        }
        first.IncludedTags.ShouldBe(new[] { "sword", "item", "sword" });
    }

    [Fact]
    public void Conditional_modifiers_do_not_change_the_displayed_damage_range()
    {
        var display = Get(_service.Describe(new[] { Data("randomdecrease"), Data("multiplier"), Data("critical") }));
        display.Short.ShouldBe("Physical 2–6; Physical: ×2; Physical: 25% crit ×2");
    }

    [Fact]
    public void Uses_generic_stat_wording_without_configuration_or_name_inference()
    {
        var data = Data("increase");
        data.EffectTypeId = 2;
        Get(new DisplayService(_world).Describe(new[] { data })).Long.ShouldBe("Heal 10 (increase HP by 10)");
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(2.125, "2.125")]
    public void Equal_bounds_and_zero_preserve_invariant_precision(double value, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            var data = Data("randomdecrease");
            data.Data["min"] = data.Data["max"] = value;
            Get(_service.Describe(new[] { data })).Short.ShouldBe($"Physical {expected}");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Empty_input_and_missing_effect_are_explicit()
    {
        var empty = Get(_service.Describe(Array.Empty<ModifierData>()));
        empty.Short.ShouldBeEmpty();
        empty.Long.ShouldBeEmpty();
        var data = Data("decrease");
        data.EffectTypeId = null;
        var inactive = Get(_service.Describe(new[] { data }));
        inactive.Short.ShouldBe("No effect: no effect type");
        inactive.Long.ShouldBe(inactive.Short);
    }

    [Theory]
    [InlineData("unknown type")]
    [InlineData("unknown effect")]
    [InlineData("missing key")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("negative")]
    [InlineData("reversed bounds")]
    [InlineData("null tags")]
    [InlineData("empty tag")]
    [InlineData("unknown stat")]
    [InlineData("null data")]
    public void Invalid_input_returns_failure(string invalid)
    {
        var data = Data("randomdecrease");
        switch (invalid)
        {
            case "unknown type": data.TypeId = "missing"; break;
            case "unknown effect": data.EffectTypeId = 999; break;
            case "missing key": data.Data.Remove("min"); break;
            case "nan": data.Data["min"] = double.NaN; break;
            case "infinity": data.Data["max"] = double.PositiveInfinity; break;
            case "negative": data.Duration = -1; break;
            case "reversed bounds": data.Data["min"] = 7; break;
            case "null tags": data.IncludedTags = null!; break;
            case "empty tag": data.IncludedTags.Add(""); break;
            case "unknown stat": _world.EffectTypes[0].StatTypeId = 999; break;
            case "null data": data.Data = null!; break;
        }
        _service.Describe(new[] { data }).Failed(out _).ShouldBeTrue();
    }

    [Fact]
    public void Rejects_null_inputs_mismatched_identity_and_custom_types()
    {
        _service.Describe(null!).Failed(out _).ShouldBeTrue();
        _service.Describe(new ModifierData[] { null! }).Failed(out _).ShouldBeTrue();
        _service.Describe(null!, Data("decrease")).Failed(out _).ShouldBeTrue();
        _service.Describe(new Decrease(), null!).Failed(out _).ShouldBeTrue();
        _service.Describe(new Increase(), Data("decrease")).Failed(out _).ShouldBeTrue();
        _world.Modifiers.Add(new Custom());
        _service.Describe(new Custom(), Data("custom")).Failed(out _).ShouldBeTrue();
    }

    [Fact]
    public void Rejects_overflow_when_combining_finite_amounts()
    {
        var data = Data("decrease");
        data.Data["value"] = double.MaxValue;
        _service.Describe(new[] { data, data }).Failed(out _).ShouldBeTrue();
    }

    [Fact]
    public void Does_not_roll_randomness_or_mutate_parameters()
    {
        _random.Range(Arg.Any<double>(), Arg.Any<double>()).Returns(_ => throw new InvalidOperationException("Must not roll"));
        _random.Roll().Returns(_ => throw new InvalidOperationException("Must not roll"));
        var random = Data("randomdecrease");
        var before = random.Data.ToArray();
        var input = new[] { random, Data("critical") };
        var first = Get(_service.Describe(input));
        var second = Get(_service.Describe(input));
        first.Short.ShouldBe(second.Short);
        first.Long.ShouldBe(second.Long);
        random.Data.ToArray().ShouldBe(before);
        _random.DidNotReceive().Range(Arg.Any<double>(), Arg.Any<double>());
        _random.DidNotReceive().Roll();
    }

    [Fact]
    public void Registers_the_display_service_by_interface()
    {
        var services = new ServiceCollection();
        services.Register();
        services.Single(x => x.ServiceType == typeof(IDisplayService)).ImplementationType.ShouldBe(typeof(DisplayService));
    }

    private static ModifierDisplay Get(Result<ModifierDisplay> result)
    {
        result.Failed(out _).ShouldBeFalse();
        return result.Data!;
    }

    private static ModifierData Data(string type) => new()
    {
        TypeId = type, EffectTypeId = 1, Scope = Scope.Use, Phase = Phase.Outgoing,
        Data = new()
        {
            ["value"] = 10, ["min"] = 2, ["max"] = 6, ["amount"] = 3,
            ["multiplier"] = 2, ["chance"] = 25, ["percent"] = 50, ["range"] = 3
        }
    };

    private sealed class Custom : ModifierType
    {
        public override string Id => "custom";
        public override string Name => "Custom";
    }
}
