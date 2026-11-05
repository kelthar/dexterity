using System.Globalization;
using static Dexterity.Generation.Tests.Game;
using static Dexterity.Generation.Tests.FluentTestSupport;

namespace Dexterity.Generation.Tests;

public sealed class FluentAuthoringTests
{
    /// <summary>AUT-01–03/06–08, API-01: calls synchronously populate the same ordinary model and preserve prior entries.</summary>
    [Fact]
    public void Authoring_is_live_and_does_not_execute_generation()
    {
        var game = new Game();
        var template = game.Template;
        var first = template.AddFixedValue("manual", "kept");
        var effect = template.EffectType(1);
        template.Items.Count.ShouldBe(1);
        template.Modifiers.ShouldBeEmpty();
        template.EffectType(1, x => x.Critical(c => c.Chance.Fixed(25).Multiplier.Fixed(2))).ShouldBeSameAs(template);
        game.Catalog.Templates.Single().ShouldBeSameAs(template);
        template.Items[0].ShouldBeSameAs(first);
        template.Items.Count.ShouldBe(3);
        var leaf = (GenerationValue)template.Items[1];
        (leaf.Order, leaf.Weight, leaf.VaryWeightBy, leaf.ChanceToGet, leaf.WorthPerValue).ShouldBe((0, 1, 0, 0, 0));
        leaf.Conditions.ShouldBeEmpty();
        game.Random.Calls.ShouldBeEmpty();
        game.World.Repository.Items.ShouldBeEmpty();
        game.Create().Component.ModifierData.Count.ShouldBe(1);
    }

    /// <summary>AUT-04/05 and SCP-04/07: independent models and captured effect filters cannot leak caller mutations.</summary>
    [Fact]
    public void Settings_and_caller_arrays_are_copied()
    {
        var game = new Game();
        var tags = new[] { "organic" };
        var effect = game.Template.EffectType(1).Attack().IncludeTags(tags);
        effect.Decrease(x => x.Value.Fixed(10));
        tags[0] = "immune";
        effect.UseSelf().IncludeTags("magical").Increase(x => x.Value.Fixed(5));
        game.Template.EffectType(2).Defend().ExcludeTags("other");
        var first = game.Template.Modifiers[0].Base;
        var second = game.Template.Modifiers[1].Base;
        first.IncludedTags.ShouldBe(new[] { "organic" });
        first.RelationKind.ShouldBe(RelationKind.Enemies);
        second.IncludedTags.ShouldBe(new[] { "organic", "magical" });
        second.RelationKind.ShouldBe(RelationKind.Self);
        first.IncludedTags.ShouldNotBeSameAs(second.IncludedTags);

        var other = new Game();
        other.Template.EffectType(1).Decrease(x => x.Value.Fixed(10));
        ((GenerationValue)game.Template.Items[0]).Value = "99";
        ((GenerationValue)other.Template.Items[0]).Value.ShouldBe("10");
    }

    /// <summary>SCP-01/02/03: presets and explicit flags map exactly to captured runtime metadata.</summary>
    [Fact]
    public void Target_presets_have_the_documented_meaning()
    {
        var game = new Game();
        game.Template.EffectType(1).Decrease(x => x.Value.Fixed(1));
        game.Template.EffectType(1).Attack().Decrease(x => x.Value.Fixed(1));
        game.Template.EffectType(1).UseSelf().Increase(x => x.Value.Fixed(1));
        game.Template.EffectType(1).Defend().PreventFlat(x => x.Amount.Fixed(1));
        game.Template.EffectType(2).Passive().Increase(x => x.Value.Fixed(1));
        game.Template.EffectType(1).Target(Scope.Passive, Phase.Incoming, RelationKind.Friends | RelationKind.Enemies)
            .IncludeTags("organic", "armored").ExcludeTags("immune").PreventPercent(x => x.Percent.Fixed(10));
        var bases = game.Template.Modifiers.Select(x => x.Base).ToArray();
        bases.Select(x => (x.Scope, x.Phase, x.RelationKind)).ShouldBe(new[]
        {
            (Scope.Use, Phase.Outgoing, RelationKind.Enemies), (Scope.Use, Phase.Outgoing, RelationKind.Enemies),
            (Scope.Use, Phase.Outgoing, RelationKind.Self), (Scope.Passive, Phase.Incoming, RelationKind.Enemies),
            (Scope.Passive, Phase.StatsRecalc, RelationKind.None), (Scope.Passive, Phase.Incoming, RelationKind.Friends | RelationKind.Enemies)
        });
        bases[^1].IncludedTags.ShouldBe(new[] { "organic", "armored" });
        bases[^1].ExcludedTags.ShouldBe(new[] { "immune" });
    }

    /// <summary>SCP-05/06: inherited group settings are isolated; opening another effect restores fresh defaults.</summary>
    [Fact]
    public void Groups_inherit_without_leaking_settings()
    {
        var game = new Game();
        var parent = game.Template.EffectType(1).Attack().IncludeTags("organic");
        parent.Group("nested", 2, group =>
        {
            group.Defend().ExcludeTags("immune").PreventFlat(x => x.Amount.Fixed(4));
            group.EffectType(2).Decrease(x => x.Value.Fixed(3));
        });
        parent.Decrease(x => x.Value.Fixed(10));
        var modifiers = game.Template.Modifiers;
        modifiers[0].Base.Phase.ShouldBe(Phase.Incoming);
        modifiers[0].Base.IncludedTags.ShouldBe(new[] { "organic" });
        modifiers[0].Base.ExcludedTags.ShouldBe(new[] { "immune" });
        modifiers[1].Base.Phase.ShouldBe(Phase.Outgoing);
        modifiers[1].Base.EffectTypeId.ShouldBe(2);
        modifiers[1].Base.IncludedTags.ShouldBeEmpty();
        modifiers[2].Base.ExcludedTags.ShouldBeEmpty();
        modifiers[2].Base.Phase.ShouldBe(Phase.Outgoing);
    }

    /// <summary>SCP-08/09: the fluent layer does not silently repair invalid recalculation combinations.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_passive_combinations_fail_service_validation(bool critical)
    {
        var game = new Game();
        var effect = game.Template.EffectType(1).Passive();
        if (critical)
            effect.Critical(x => x.Chance.Fixed(25).Multiplier.Fixed(2));
        else
            effect.IncludeTags("organic").Increase(x => x.Value.Fixed(3));
        Rejected(game);
        game.Service.Validate().Failed(out _).ShouldBeTrue();
        game.Random.Calls.ShouldBeEmpty();
    }

    /// <summary>KEY-01–09: identities account for manual edits without a stale registry or key renumbering.</summary>
    [Fact]
    public void Keys_are_stable_and_collisions_are_rejected()
    {
        var game = new Game();
        game.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "decrease", EffectTypeId = 1, Data = new() { ["value"] = 1 } } });
        game.Template.EffectType(1).Decrease(x => x.Value.Fixed(10).Duration.Fixed(2));
        game.Template.Modifiers[1].DataKeys["value"].ShouldBe("fluent/1/value");
        game.Template.Modifiers[1].DurationKey.ShouldBe("fluent/1/duration");
        game.Template.Modifiers.Reverse();
        game.Template.Modifiers[0].DataKeys["value"].ShouldBe("fluent/1/value");
        game.Template.Modifiers.RemoveAt(1);
        Should.Throw<InvalidOperationException>(() => game.Template.EffectType(1).Decrease(x => x.Value.Fixed(20)));
        ((GenerationValue)game.Template.Items[0]).Value.ShouldBe("10");

        var template = new Game().Template;
        template.Group("a", 1, a => a.Group("shared", 1, _ => { }));
        template.Group("b", 1, b => b.Group("shared", 1, _ => { }));
        Should.Throw<InvalidOperationException>(() => template.Group("a", 1, _ => { }));
        Should.Throw<InvalidOperationException>(() => template.Value("a", x => x.Fixed("x")));
        template.Group("fluent/group", 1, g => g.Value("named", x => x.Fixed("x")));
        Should.Throw<InvalidOperationException>(() => template.Group("c", 1, g => g.Value("named", x => x.Fixed("again"))));
        Should.Throw<ArgumentException>(() => template.Value("fluent/0/value", x => x.Fixed("bad")));
        template.Group("d", 1, g => g.EffectType(1).Critical(x => x.Chance.Fixed(25).Multiplier.Fixed(2)));
        template.Group("e", 1, g => g.EffectType(2).Critical(x => x.Chance.Fixed(25).Multiplier.Fixed(2)));
        template.Modifiers.Select(x => x.DataKeys["chance"]).ShouldBe(new[] { "fluent/0/chance", "fluent/1/chance" });
    }

    /// <summary>VAL-01–05: argument guards reject malformed configuration before any execution.</summary>
    [Fact]
    public void Authoring_arguments_are_guarded()
    {
        Should.Throw<ArgumentNullException>(() => FluentGenerationExtensions.EffectType(null!, 1));
        var template = new Game().Template;
        Should.Throw<ArgumentNullException>(() => template.EffectType(1, null!));
        Should.Throw<ArgumentNullException>(() => template.Group("g", 1, null!));
        Should.Throw<ArgumentNullException>(() => template.Value("v", null!));
        Should.Throw<ArgumentNullException>(() => template.EffectType(1).Critical(null!));
        foreach (var key in new[] { "", " ", null! })
        {
            Should.Throw<ArgumentException>(() => template.Group(key, 1, _ => { }));
            Should.Throw<ArgumentException>(() => template.Value(key, x => x.Fixed("x")));
            Should.Throw<ArgumentException>(() => template.Tags(key));
        }
        Should.Throw<ArgumentException>(() => template.EffectType(1).Target((Scope)999, Phase.Outgoing, RelationKind.None));
        Should.Throw<ArgumentException>(() => template.When("x", (ConditionOperator)999, "y"));
        foreach (var cost in new[] { 0, -1 })
        {
            var fresh = new Game().Template;
            Should.Throw<ArgumentException>(() => fresh.EffectType(1).Decrease(x => x.Value.Weight(cost)));
            Should.Throw<ArgumentException>(() => fresh.EffectType(1).Increase(x => x.Value.Random(cost, 0, 1)));
        }
        Should.Throw<ArgumentException>(() => template.EffectType(1).Decrease(x => x.Value.Random(1, -1, 1)));
        Should.Throw<ArgumentException>(() => template.EffectType(1).Decrease(x => x.Value.Random(1, 2, 1)));
        Should.Throw<ArgumentException>(() => template.Group("negative", -1, _ => { }));
        Should.Throw<ArgumentException>(() => template.Group("overflow", int.MaxValue, x => x.VaryWeightBy(1)));
        foreach (var chance in new[] { -1, 101 })
            Should.Throw<ArgumentException>(() => new Game().Template.Group("g", 1, x => x.IncludeChance(chance)));
    }

    /// <summary>VAL-06/07: every required parameter and optional duration rejects duplicate configuration.</summary>
    [Fact]
    public void Parameters_are_required_once_and_duplicates_leave_no_orphans()
    {
        // Independently omit each required input, then duplicate every public numeric accessor.
        foreach (var type in Types)
        {
            foreach (var omitted in Keys(type))
            {
                var template = new Game().Template;
                Should.Throw<InvalidOperationException>(() => Visit(template.EffectType(1), type, owner =>
                {
                    foreach (var key in Keys(type).Where(key => key != omitted))
                    {
                        dynamic input = ((object)owner).GetType().GetProperties().Single(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)).GetValue(owner)!;
                        input.Fixed(Constant(key));
                    }
                }));
            }
            foreach (var key in Keys(type).Append("duration"))
            {
                for (var strategy = 0; strategy < 3; strategy++)
                {
                    var template = new Game().Template;
                    Should.Throw<InvalidOperationException>(() => Visit(template.EffectType(1), type, owner =>
                    {
                        dynamic input = ((object)owner).GetType().GetProperties().Single(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)).GetValue(owner)!;
                        input.Fixed(10d);
                        if (strategy == 0)
                            input.Fixed(10d);
                        else if (strategy == 1)
                            input.Weight(1);
                        else
                            input.Random(1, 0, 1);
                    }));
                    template.Items.Count.ShouldBe(1);
                }
            }
        }
        foreach (var type in Types)
        {
            var template = new Game().Template;
            // The deliberately empty callback must fail for every typed modifier.
            var effect = template.EffectType(1);
            switch (type)
            {
                case "decrease": Should.Throw<InvalidOperationException>(() => effect.Decrease(_ => { })); break;
                case "increase": Should.Throw<InvalidOperationException>(() => effect.Increase(_ => { })); break;
                case "randomdecrease": Should.Throw<InvalidOperationException>(() => effect.RandomDecrease(x => x.Min.Fixed(1))); break;
                case "randomincrease": Should.Throw<InvalidOperationException>(() => effect.RandomIncrease(x => x.Max.Fixed(2))); break;
                case "critical": Should.Throw<InvalidOperationException>(() => effect.Critical(x => x.Chance.Fixed(25))); break;
                case "splash": Should.Throw<InvalidOperationException>(() => effect.Splash(x => x.Range.Fixed(3))); break;
                case "multiplier": Should.Throw<InvalidOperationException>(() => effect.Multiplier(_ => { })); break;
                case "preventflat": Should.Throw<InvalidOperationException>(() => effect.PreventFlat(_ => { })); break;
                case "preventpercent": Should.Throw<InvalidOperationException>(() => effect.PreventPercent(_ => { })); break;
                default: Should.Throw<InvalidOperationException>(() => effect.Steal(_ => { })); break;
            }
        }
        for (var mode = 0; mode < 3; mode++)
        {
            var game = new Game();
            Should.Throw<InvalidOperationException>(() => game.Template.EffectType(1).Critical(x =>
            {
                x.Chance.Fixed(10);
                if (mode == 0)
                    x.Chance.Fixed(10);
                else if (mode == 1)
                    x.Chance.Weight(1);
                else
                    x.Chance.Random(1, 0, 1);
            }));
            game.Template.Items.Count.ShouldBe(1);
            game.Template.Modifiers.Single().DataKeys.Count.ShouldBe(1);
        }
    }

    /// <summary>VAL-08: callback exceptions propagate while already authored model edits remain live.</summary>
    [Fact]
    public void Callback_errors_do_not_roll_back_prior_edits()
    {
        var template = new Game().Template;
        var exception = new FormatException("test callback");
        Should.Throw<FormatException>(() => template.Group("g", 1, group =>
        {
            group.Value("a", x => x.Fixed("kept"));
            throw exception;
        })).ShouldBeSameAs(exception);
        ((GenerationValue)((GenerationGroup)template.Items.Single()).Items.Single()).Value.ShouldBe("kept");
    }

    /// <summary>NUM-01–03/05: numeric literals round-trip without culture/exponent leakage and reject nonfinite inputs.</summary>
    [Fact]
    public void Fixed_numbers_are_invariant_and_round_trip()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "", "en-US", "sv-SE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var value in new[] { 0d, -0d, 1.5, 1e-10, 1e20, double.Epsilon, double.MaxValue })
                {
                    var game = new Game();
                    game.Template.EffectType(1).Multiplier(x => x.Multiplier.Fixed(value));
                    var text = ((GenerationValue)game.Template.Items.Single()).Value!;
                    text.ShouldMatch(@"\A[+-]?[0-9]+(?:\.[0-9]+)?\z");
                    double.Parse(text, CultureInfo.InvariantCulture).ShouldBe(value);
                    game.Create().Component.ModifierData.Single().Data["multiplier"].ShouldBe(value);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Should.Throw<ArgumentException>(() => new Game().Template.EffectType(1).Multiplier(x => x.Multiplier.Fixed(value)));
        foreach (var value in new[] { -1d, 101d })
        {
            var game = new Game();
            game.Template.EffectType(1).Critical(x => x.Chance.Fixed(value).Multiplier.Fixed(2));
            Rejected(game);
        }
    }
}
