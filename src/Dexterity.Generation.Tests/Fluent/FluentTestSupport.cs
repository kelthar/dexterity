using System.Globalization;
using System.Text.Json;
using static Dexterity.Generation.Tests.Game;

namespace Dexterity.Generation.Tests;

/// <summary>Independent manual definitions and comparison helpers for the fluent acceptance suite.</summary>
internal static class FluentTestSupport
{
    internal static readonly string[] Types = { "decrease", "increase", "randomdecrease", "randomincrease", "multiplier", "critical", "splash", "preventflat", "preventpercent", "steal" };

    /// <summary>Lists required parameters without consulting the fluent implementation.</summary>
    internal static string[] Keys(string type) => type switch
    {
        "decrease" or "increase" => new[] { "value" },
        "randomdecrease" or "randomincrease" => new[] { "min", "max" },
        "multiplier" => new[] { "multiplier" },
        "critical" => new[] { "chance", "multiplier" },
        "splash" => new[] { "range", "percent" },
        "preventflat" => new[] { "amount" },
        _ => new[] { "percent" }
    };

    /// <summary>Visits a typed callback to test its public accessors uniformly, without mocking a builder.</summary>
    internal static void Visit(GenerationEffectBuilder effect, string type, Action<dynamic> configure)
    {
        switch (type)
        {
            case "decrease": effect.Decrease(x => configure(x)); break;
            case "increase": effect.Increase(x => configure(x)); break;
            case "randomdecrease": effect.RandomDecrease(x => configure(x)); break;
            case "randomincrease": effect.RandomIncrease(x => configure(x)); break;
            case "critical": effect.Critical(x => configure(x)); break;
            case "multiplier": effect.Multiplier(x => configure(x)); break;
            case "splash": effect.Splash(x => configure(x)); break;
            case "preventflat": effect.PreventFlat(x => configure(x)); break;
            case "preventpercent": effect.PreventPercent(x => configure(x)); break;
            default: effect.Steal(x => configure(x)); break;
        }
    }

    /// <summary>Provides safe fixed companions when varying one input independently.</summary>
    internal static double Constant(string key) => key switch
    {
        "min" => 3, "max" => 20, "chance" => 25, "multiplier" => 2, "range" => 3, "percent" => 50, _ => 10
    };

    /// <summary>Authors one typed modifier, varying an input through fixed, weighted or random strategies.</summary>
    internal static void Fluent(GenerationEffectBuilder effect, string type, string varied = "", int strategy = 0, bool duration = false)
    {
        switch (type)
        {
            case "decrease": effect.Decrease(x => Set(x.Value, "value", varied, strategy, duration, x)); break;
            case "increase": effect.Increase(x => Set(x.Value, "value", varied, strategy, duration, x)); break;
            case "randomdecrease": effect.RandomDecrease(x => { Set(x.Min, "min", varied, strategy, duration, x); Set(x.Max, "max", varied, strategy); }); break;
            case "randomincrease": effect.RandomIncrease(x => { Set(x.Min, "min", varied, strategy, duration, x); Set(x.Max, "max", varied, strategy); }); break;
            case "multiplier": effect.Multiplier(x => Set(x.Multiplier, "multiplier", varied, strategy, duration, x)); break;
            case "critical": effect.Critical(x => { Set(x.Chance, "chance", varied, strategy, duration, x); Set(x.Multiplier, "multiplier", varied, strategy); }); break;
            case "splash": effect.Splash(x => { Set(x.Range, "range", varied, strategy, duration, x); Set(x.Percent, "percent", varied, strategy); }); break;
            case "preventflat": effect.PreventFlat(x => Set(x.Amount, "amount", varied, strategy, duration, x)); break;
            case "preventpercent": effect.PreventPercent(x => Set(x.Percent, "percent", varied, strategy, duration, x)); break;
            case "steal": effect.Steal(x => Set(x.Percent, "percent", varied, strategy, duration, x)); break;
            default: throw new ArgumentException(type);
        }
    }

    /// <summary>Applies a selected strategy and optionally adds duration, without mocking authoring logic.</summary>
    private static void Set<T>(GenerationParameterBuilder<T> input, string key, string varied, int strategy, bool duration = false, T? owner = null) where T : GenerationModifierBuilder<T>
    {
        if (key != varied)
            input.Fixed(Constant(key));
        else if (strategy == 0)
            input.Fixed(10);
        else if (strategy == 1)
            input.Weight(4);
        else
            input.Random(4, 8, 12);

        if (duration)
        {
            if (strategy == 0)
                owner!.Duration.Fixed(2.5);
            else if (strategy == 1)
                owner!.Duration.Weight(2);
            else
                owner!.Duration.Random(1, 2, 5);
        }
    }

    /// <summary>Constructs the expected model independently, using only explicit existing domain models.</summary>
    internal static void Manual(GenerationTemplate template, GenerationNode node, string type, string varied = "", int strategy = 0, bool duration = false, Scope scope = Scope.Use, Phase phase = Phase.Outgoing, RelationKind relations = RelationKind.Enemies)
    {
        var ordinal = template.Modifiers.Count;
        var modifier = new ModifierTemplate { Base = new ModifierData { TypeId = type, EffectTypeId = 1, Scope = scope, Phase = phase, RelationKind = relations } };
        foreach (var key in Keys(type))
        {
            var id = $"fluent/{ordinal}/{key}";
            GenerationValue leaf = key == varied && strategy != 0
                ? new() { Id = id, Kind = strategy == 1 ? GenerationValueKind.Weighted : GenerationValueKind.Random, WorthPerValue = 4, MinValue = strategy == 2 ? 8 : 0, MaxValue = strategy == 2 ? 12 : 0 }
                : new() { Id = id, Kind = GenerationValueKind.Fixed, WorthPerValue = 0, Value = (key == varied ? 10 : Constant(key)).ToString(CultureInfo.InvariantCulture) };
            node.Items.Add(leaf);
            modifier.DataKeys.Add(key, id);
            // Match the fixture's explicit callback order, including duration between two parameters.
            if (duration && key == Keys(type)[0])
            {
                var idDuration = $"fluent/{ordinal}/duration";
                node.Items.Add(strategy switch
                {
                    0 => new GenerationValue { Id = idDuration, Kind = GenerationValueKind.Fixed, WorthPerValue = 0, Value = "2.5" },
                    1 => new GenerationValue { Id = idDuration, WorthPerValue = 2 },
                    _ => new GenerationValue { Id = idDuration, Kind = GenerationValueKind.Random, WorthPerValue = 1, MinValue = 2, MaxValue = 5 }
                });
                modifier.DurationKey = idDuration;
            }
        }
        template.Modifiers.Add(modifier);
    }

    /// <summary>Walks runtime entry types so interface serialization cannot hide leaf-specific fields.</summary>
    internal static string Snapshot(GenerationTemplate template)
    {
        var parts = new List<string> { JsonSerializer.Serialize(template) };
        var nodes = new Stack<GenerationNode>();
        nodes.Push(template);
        while (nodes.TryPop(out var node))
        {
            foreach (var entry in node.Items)
            {
                parts.Add(JsonSerializer.Serialize(entry, entry.GetType()));
                if (entry is GenerationGroup group)
                    nodes.Push(group);
            }
        }
        return string.Join("\n", parts);
    }

    /// <summary>Checks full supported model/report/component parity and generation definition purity.</summary>
    internal static GeneratedComponent Parity(Game fluent, Game manual, int worth, params int[] samples)
    {
        Snapshot(fluent.Template).ShouldBe(Snapshot(manual.Template));
        var snapshot = Snapshot(fluent.Template);
        foreach (var sample in samples)
        {
            fluent.Random.Values.Enqueue(sample);
            manual.Random.Values.Enqueue(sample);
        }
        var left = Ok(fluent.Service.Create(fluent.Request(worth)));
        var right = Ok(manual.Service.Create(manual.Request(worth)));
        JsonSerializer.Serialize(left.Generation).ShouldBe(JsonSerializer.Serialize(right.Generation));
        JsonSerializer.Serialize(left.Component).ShouldBe(JsonSerializer.Serialize(right.Component));
        fluent.Random.Calls.ShouldBe(manual.Random.Calls);
        fluent.Random.Values.ShouldBeEmpty();
        manual.Random.Values.ShouldBeEmpty();
        Snapshot(fluent.Template).ShouldBe(snapshot);
        return left;
    }

    /// <summary>Checks a failed service result and pre-write isolation.</summary>
    internal static void Rejected(Game game, int worth = 40, int code = 400)
    {
        var result = game.Service.Create(game.Request(worth));
        result.Failed(out _).ShouldBeTrue();
        result.Code.ShouldBe(code);
        result.States.ShouldNotBeEmpty();
        game.World.Repository.Items.ShouldBeEmpty();
    }
}
