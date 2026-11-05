using NSubstitute;
using System.Text.Json;
using static Dexterity.Generation.Tests.Game;
using static Dexterity.Generation.Tests.FluentTestSupport;

namespace Dexterity.Generation.Tests;

public sealed class FluentRuntimeTests
{
    /// <summary>RUN-01–06/11/12 and NUM-04: generated modifiers join the real attack/use pipeline with manual parity.</summary>
    [Theory]
    [InlineData("decrease", 0, 100, 90, 50)]
    [InlineData("increase", 0, 100, 100, 60)]
    [InlineData("randomdecrease", 0, 100, 93, 50)]
    [InlineData("randomincrease", 0, 100, 100, 56)]
    [InlineData("multiplier", 0, 100, 85, 50)]
    [InlineData("critical", 24.999, 100, 80, 50)]
    [InlineData("critical", 25, 100, 90, 50)]
    [InlineData("steal", 0, 100, 90, 52)]
    [InlineData("steal", 0, 3, 0, 50.6)]
    public void Runtime_actions_match_manual_templates(string type, double roll, double targetHp, double expectedTarget, double expectedHero)
    {
        var fluent = Runtime(type, true, roll, targetHp);
        var manual = Runtime(type, false, roll, targetHp);
        fluent.ShouldBe(manual);
        fluent.Target.ShouldBe(expectedTarget);
        fluent.Hero.ShouldBe(expectedHero);
    }

    /// <summary>Creates the same generated instrument and resolves its normal runtime action in independent worlds.</summary>
    private static (double Hero, double Target, string Results) Runtime(string type, bool fluent, double roll, double targetHp, double chance = 25, bool baseDamage = true)
    {
        var game = new Game();
        var random = Substitute.For<IRandomSource>();
        random.Roll().Returns(roll);
        random.Range(Arg.Any<double>(), Arg.Any<double>()).Returns(type == "randomincrease" ? 6 : 7);
        game.World.Modifiers.RemoveAll(x => x.Id is "critical" or "randomincrease" or "randomdecrease");
        game.World.Modifiers.AddRange(new IModifierType[] { new Critical(random), new RandomIncrease(random), new RandomDecrease(random) });
        var self = type is "increase" or "randomincrease";
        if (fluent)
        {
            var effect = game.Template.EffectType(1);
            if (self)
                effect.UseSelf();
            else
                effect.Attack();
            if (baseDamage && type is "multiplier" or "critical" or "steal")
                effect.Decrease(x => x.Value.Fixed(10));
            if (type == "steal")
                effect.Steal(x => x.Percent.Fixed(20));
            else if (type == "multiplier")
                effect.Multiplier(x => x.Multiplier.Fixed(1.5));
            else if (type == "critical")
                effect.Critical(x => x.Chance.Fixed(chance).Multiplier.Fixed(2));
            else
                Fluent(effect, type);
        }
        else
        {
            if (baseDamage && type is "multiplier" or "critical" or "steal")
                Manual(game.Template, game.Template, "decrease");
            Manual(game.Template, game.Template, type, relations: self ? RelationKind.Self : RelationKind.Enemies);
            if (type == "steal")
                ((GenerationValue)game.Template.Items[^1]).Value = "20";
            else if (type == "multiplier")
                ((GenerationValue)game.Template.Items[^1]).Value = "1.5";
            else if (type == "critical")
                ((GenerationValue)game.Template.Items[^2]).Value = chance.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        var instrument = game.Create().Component;
        random.DidNotReceive().Roll();
        random.DidNotReceive().Range(Arg.Any<double>(), Arg.Any<double>());
        var hero = Actor(game, 50);
        var target = Actor(game, targetHp);
        var relation = new RelationService(game.World);
        Ok(relation.Add(hero.Id, instrument.Id));
        Ok(relation.Equip(hero.Id, instrument.Id, 1));
        var effects = new DefaultEffectService(game.World);
        var results = Ok(effects.Attack(hero.Id, instrument.Id, self ? hero.Id : target.Id));
        return (hero.Stats[HP].Current, target.Stats[HP].Current, JsonSerializer.Serialize(results));
    }

    /// <summary>NUM-04/RUN-06: chance endpoints and absence of base damage never invent a contribution.</summary>
    [Fact]
    public void Critical_endpoints_and_empty_scaling_are_exact()
    {
        Runtime("critical", true, 0, 100, 0).Target.ShouldBe(90);
        Runtime("critical", true, 0, 100, 100).Target.ShouldBe(80);
        foreach (var type in new[] { "multiplier", "critical" })
        {
            var fluent = Runtime(type, true, 0, 100, baseDamage: false);
            fluent.ShouldBe(Runtime(type, false, 0, 100, baseDamage: false));
            fluent.Target.ShouldBe(100);
        }
    }

    /// <summary>Creates a live actor with explicit pool values for deterministic runtime assertions.</summary>
    private static Component Actor(Game game, double current)
    {
        var component = Ok(game.World.Repository.Create(Character, 1));
        Ok(game.World.Repository.SetStat(component.Id, new Stat { TypeId = HP, Max = 100, ModifiedMax = 100, Current = current }));
        return component;
    }

    /// <summary>RUN-07/08: splash uses inclusive distance and retains primary damage when secondary is out of range.</summary>
    [Theory]
    [InlineData(2, 95)]
    [InlineData(3, 95)]
    [InlineData(3.1, 100)]
    [InlineData(double.PositiveInfinity, 100)]
    public void Splash_distance_is_inclusive(double distance, double expected)
    {
        var left = Splash(true, distance);
        var right = Splash(false, distance);
        left.ShouldBe(right);
        left.Primary.ShouldBe(90);
        left.Secondary.ShouldBe(expected);
    }

    /// <summary>Resolves explicit primary/secondary targets without substituting the production effect pipeline.</summary>
    private static (double Primary, double Secondary, string Results) Splash(bool fluent, double distance)
    {
        var game = new Game();
        if (fluent)
            game.Template.EffectType(1).Attack().Decrease(x => x.Value.Fixed(10)).Splash(x => x.Range.Fixed(3).Percent.Fixed(50));
        else
        {
            Manual(game.Template, game.Template, "decrease");
            Manual(game.Template, game.Template, "splash");
        }
        var instrument = game.Create().Component;
        var hero = Actor(game, 50);
        var primary = Actor(game, 100);
        var secondary = Actor(game, 100);
        var logic = Substitute.For<IWorldLogic>();
        logic.GetRelationKind(Arg.Any<int>(), Arg.Any<int>()).Returns(RelationKind.Enemies);
        logic.GetDistance(primary.Id, secondary.Id).Returns(distance);
        game.World.Logic = logic;
        var relations = new RelationService(game.World);
        Ok(relations.Add(hero.Id, instrument.Id));
        Ok(relations.Equip(hero.Id, instrument.Id, 1));
        var results = Ok(new DefaultEffectService(game.World).Attack(hero.Id, instrument.Id, primary.Id, new[] { secondary.Id }));
        return (primary.Stats[HP].Current, secondary.Stats[HP].Current, JsonSerializer.Serialize(results));
    }

    /// <summary>RUN-09/10/14: defensive modifiers are effective only when attached and use existing prevention limits.</summary>
    [Theory]
    [InlineData(false, 4, true, 94)]
    [InlineData(false, 20, true, 100)]
    [InlineData(true, 25, true, 85)]
    [InlineData(false, 4, false, 90)]
    public void Defense_requires_attachment(bool percent, double amount, bool attached, double expected)
    {
        foreach (var fluent in new[] { true, false })
        {
            var game = new Game();
            if (fluent)
            {
                var effect = game.Template.EffectType(1).Defend();
                if (percent)
                    effect.PreventPercent(x => x.Percent.Fixed(amount));
                else
                    effect.PreventFlat(x => x.Amount.Fixed(amount));
            }
            else
            {
                game.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = percent ? "preventpercent" : "preventflat", EffectTypeId = 1, Scope = Scope.Passive, Phase = Phase.Incoming, RelationKind = RelationKind.Enemies, Data = new() { [percent ? "percent" : "amount"] = amount } } });
            }
            var provider = game.Create().Component;
            var enemy = Actor(game, 100);
            var hero = Actor(game, 50);
            if (attached)
            {
                var relations = new RelationService(game.World);
                Ok(relations.Add(enemy.Id, provider.Id));
                Ok(relations.Equip(enemy.Id, provider.Id, 1));
            }
            var input = new EffectResult { SourceId = hero.Id, TargetId = enemy.Id, InstrumentId = provider.Id, Items = new() { new EffectResultItem { EffectTypeId = 1, Amount = percent ? -20 : -10, SourceId = hero.Id, InstrumentId = provider.Id } } };
            var effects = new DefaultEffectService(game.World);
            foreach (var result in Ok(effects.Incoming(enemy.Id, input)))
                Ok(effects.Apply(result.TargetId!.Value, result));
            enemy.Stats[HP].Current.ShouldBe(expected);
        }
    }

    /// <summary>RUN-13: generated passive bonuses activate on attachment and do not accumulate on repeated recalculation.</summary>
    [Fact]
    public void Passive_bonus_activates_after_attachment()
    {
        var game = new Game();
        game.Template.EffectType(2).Passive().Increase(x => x.Value.Fixed(3));
        var provider = game.Create().Component;
        var actor = Actor(game, 50);
        actor.Stats[Str].ModifiedMax.ShouldBe(0);
        var relations = new RelationService(game.World);
        Ok(relations.Add(actor.Id, provider.Id));
        Ok(relations.Equip(actor.Id, provider.Id, 1));
        var effects = new DefaultEffectService(game.World);
        Ok(effects.Recalculate(actor.Id));
        Ok(effects.Recalculate(actor.Id));
        actor.Stats[Str].ModifiedMax.ShouldBe(3);
    }

    /// <summary>RUN-16: timed effects and origin deletion follow exactly the manual counterpart's existing lifecycle.</summary>
    [Fact]
    public void Timed_effect_lifecycle_matches_manual()
    {
        var snapshots = new List<string>();
        foreach (var fluent in new[] { true, false })
        {
            var game = new Game();
            if (fluent)
                game.Template.EffectType(1).Decrease(x => x.Value.Fixed(10).Duration.Fixed(2));
            else
            {
                Manual(game.Template, game.Template, "decrease");
                game.Template.Items.Add(new GenerationValue { Id = "fluent/0/duration", Kind = GenerationValueKind.Fixed, WorthPerValue = 0, Value = "2" });
                game.Template.Modifiers[0].DurationKey = "fluent/0/duration";
            }
            var instrument = game.Create().Component;
            instrument.Effects.ShouldBeEmpty();
            var hero = Actor(game, 50);
            var enemy = Actor(game, 100);
            var relations = new RelationService(game.World);
            Ok(relations.Add(hero.Id, instrument.Id));
            Ok(relations.Equip(hero.Id, instrument.Id, 1));
            var effects = new DefaultEffectService(game.World);
            Ok(effects.Attack(hero.Id, instrument.Id, enemy.Id));
            var before = JsonSerializer.Serialize(enemy);
            foreach (var result in Ok(effects.Tick(enemy.Id, 1)))
                Ok(effects.Apply(result.TargetId!.Value, result));
            var middle = JsonSerializer.Serialize(enemy);
            Ok(game.World.Repository.Delete(instrument.Id));
            var applications = new List<string>();
            foreach (var result in Ok(effects.Tick(enemy.Id, 1)))
            {
                // The current runtime rejects Apply after the origin was removed; compare that contract too.
                var application = effects.Apply(result.TargetId!.Value, result);
                application.Failed(out _).ShouldBeTrue();
                application.Code.ShouldBe(404);
                applications.Add(JsonSerializer.Serialize(application));
            }
            snapshots.Add(before + middle + JsonSerializer.Serialize(enemy) + string.Join("", applications));
        }
        snapshots[0].ShouldBe(snapshots[1]);
    }
}
