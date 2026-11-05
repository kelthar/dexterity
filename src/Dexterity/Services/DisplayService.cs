using System.Globalization;

namespace Dexterity;

/// <summary>Describes definitions without executing modifiers or predicting combat results.</summary>
public sealed class DisplayService : IDisplayService
{
    private readonly World _world;
    private readonly IReadOnlyDictionary<int, EffectDisplayText> _text;

    public DisplayService(World world, IReadOnlyDictionary<int, EffectDisplayText>? text = null)
    {
        _world = world;
        _text = text ?? new Dictionary<int, EffectDisplayText>();
    }

    public Result<ModifierDisplay> Describe(IModifierType modifier, ModifierData data)
    {
        // validate identity before resolving the definition
        if (modifier == null || data == null || modifier.Id != data.TypeId)
            return Result.Fail("Modifier and matching modifier data are required.", 400);

        return Describe(new[] { data });
    }

    public Result<ModifierDisplay> Describe(IEnumerable<ModifierData> modifiers)
    {
        // validate the entire input before formatting
        if (modifiers == null || _world == null || _world.Modifiers == null ||
            _world.EffectTypes == null || _world.Stats == null)
            return Result.Fail("Modifiers and world definitions are required.", 400);

        var groups = new List<List<ModifierData>>();
        var validator = new WorldBuilder(_world);
        foreach (var data in modifiers)
        {
            if (validator.Validate(data).Failed(out var validation))
                return validation;
            if (!Supported(data.TypeId))
                return Result.Fail($"Display is not supported for modifier {data.TypeId}.", 400);
            if (data.IncludedTags.Any(string.IsNullOrWhiteSpace) || data.ExcludedTags.Any(string.IsNullOrWhiteSpace))
                return Result.Fail("Display tags must be non-empty strings.", 400);
            if (data.EffectTypeId.HasValue)
            {
                var effect = _world.EffectTypes.First(x => x.Id == data.EffectTypeId.Value);
                if (string.IsNullOrWhiteSpace(effect.Name) || !_world.Stats.Any(x => x.Id == effect.StatTypeId && !string.IsNullOrWhiteSpace(x.Name)))
                    return Result.Fail("Effect and referenced stat display names are required.", 400);
            }

            // only unconditional amounts with identical applicability can combine
            var group = Additive(data) ? groups.FirstOrDefault(x => Compatible(x[0], data)) : null;
            if (group == null)
                groups.Add(new List<ModifierData> { data });
            else
                group.Add(data);
        }

        // render stable, independent clauses
        var displays = new List<ModifierDisplay>();
        foreach (var group in groups)
        {
            if (DescribeGroup(group).Failed(out var result))
                return result;
            displays.Add(result.Data!);
        }
        return Result.Ok(new ModifierDisplay
        {
            Short = string.Join("; ", displays.Select(x => x.Short)),
            Long = string.Join("; ", displays.Select(x => x.Long))
        });
    }

    private Result<ModifierDisplay> DescribeGroup(List<ModifierData> group)
    {
        var data = group[0];
        var suffix = Qualifiers(data);
        if (!data.EffectTypeId.HasValue)
            return Result.Ok(new ModifierDisplay
            {
                Short = "No effect: no effect type" + suffix,
                Long = "No effect: no effect type" + suffix
            });

        var effect = _world.EffectTypes.First(x => x.Id == data.EffectTypeId.Value);
        var stat = _world.Stats.First(x => x.Id == effect.StatTypeId);
        _text.TryGetValue(effect.Id, out var text);
        var quantity = string.IsNullOrWhiteSpace(text?.Quantity) ? stat.Name : text.Quantity;

        // sum bounds without invoking any modifier implementation
        if (Additive(data))
        {
            var min = group.Sum(x => x.Data[Random(x) ? "min" : "value"]);
            var max = group.Sum(x => x.Data[Random(x) ? "max" : "value"]);
            if (!EffectRules.Finite(min) || !EffectRules.Finite(max))
                return Result.Fail("Combined display amounts must be finite.", 400);
            var amount = Range(min, max);
            var details = group.Select(x =>
            {
                var value = Random(x) ? Range(x.Data["min"], x.Data["max"]) : Number(x.Data["value"]);
                var prefix = Random(x) ? "random " : group.Count > 1 ? "base " : "";
                return prefix + Phrase(x, value, stat.Name, text);
            });
            return Result.Ok(new ModifierDisplay
            {
                Short = $"{effect.Name} {amount}{suffix}",
                Long = $"{effect.Name} {amount} ({string.Join(", ", details)}){suffix}"
            });
        }

        // conditional operations remain explicit rather than inflating the range
        string Value(string key) => Number(data.Data[key]);
        var (shortText, longText) = data.TypeId switch
        {
            "multiplier" => ($"×{Value("multiplier")}", $"multiply {quantity} by {Value("multiplier")}"),
            "critical" => ($"{Value("chance")}% crit ×{Value("multiplier")}", $"{Value("chance")}% chance to multiply {quantity} by {Value("multiplier")}"),
            "preventflat" => ($"prevent {Value("amount")}", $"prevent {Value("amount")} {quantity}"),
            "preventpercent" => ($"prevent {Value("percent")}%", $"prevent {Value("percent")}% of {quantity}"),
            "splash" => ($"{Value("percent")}% splash, range {Value("range")}", $"splash {Value("percent")}% of {quantity} within range {Value("range")}"),
            "steal" => ($"steal {Value("percent")}%", $"restore {Value("percent")}% of actual {quantity} lost by the target"),
            _ => throw new InvalidOperationException("Validated modifier has no display formatter.")
        };
        return Result.Ok(new ModifierDisplay
        {
            Short = $"{effect.Name}: {shortText}{suffix}",
            Long = $"{effect.Name}: {longText}{suffix}"
        });
    }

    private static string Phrase(ModifierData data, string amount, string stat, EffectDisplayText? text)
    {
        var phrase = Increase(data) ? text?.Increase : text?.Decrease;
        if (!string.IsNullOrWhiteSpace(phrase))
            return phrase.Replace("{amount}", amount, StringComparison.Ordinal);
        return $"{(Increase(data) ? "increase" : "decrease")} {stat} by {amount}";
    }

    private static string Qualifiers(ModifierData data)
    {
        var parts = new List<string>();
        if (data.Scope == Scope.Passive)
            parts.Add("passive");
        if (data.Phase != Phase.Outgoing)
            parts.Add(data.Phase == Phase.Incoming ? "incoming" : "stat recalculation");
        if (data.Duration > 0)
            parts.Add($"for {Number(data.Duration)} time units");
        if (data.RelationKind != RelationKind.None)
            parts.Add($"targets: {data.RelationKind}");
        if (data.IncludedTags.Count > 0)
            parts.Add($"includes tags: {string.Join(", ", data.IncludedTags.Distinct().OrderBy(x => x, StringComparer.Ordinal))}");
        if (data.ExcludedTags.Count > 0)
            parts.Add($"excludes tags: {string.Join(", ", data.ExcludedTags.Distinct().OrderBy(x => x, StringComparer.Ordinal))}");
        return parts.Count == 0 ? "" : $" [{string.Join("; ", parts)}]";
    }

    private static bool Compatible(ModifierData first, ModifierData next) =>
        Additive(first) && first.EffectTypeId == next.EffectTypeId && Increase(first) == Increase(next) &&
        first.Scope == next.Scope && first.Phase == next.Phase && first.Duration == next.Duration &&
        first.RelationKind == next.RelationKind &&
        first.IncludedTags.ToHashSet(StringComparer.Ordinal).SetEquals(next.IncludedTags) &&
        first.ExcludedTags.ToHashSet(StringComparer.Ordinal).SetEquals(next.ExcludedTags);

    private static bool Additive(ModifierData data) => data.TypeId is "increase" or "decrease" or "randomincrease" or "randomdecrease";
    private static bool Increase(ModifierData data) => data.TypeId is "increase" or "randomincrease";
    private static bool Random(ModifierData data) => data.TypeId is "randomincrease" or "randomdecrease";
    private static bool Supported(string id) => id is "increase" or "decrease" or "randomincrease" or "randomdecrease" or
        "multiplier" or "critical" or "preventflat" or "preventpercent" or "splash" or "steal";
    private static string Number(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    private static string Range(double min, double max) => min == max ? Number(min) : $"{Number(min)}–{Number(max)}";
}
