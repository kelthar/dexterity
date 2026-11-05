using System.Globalization;

namespace Dexterity.Generation;

/// <summary>Authoring guards and live-tree identity checks shared by fluent helpers.</summary>
internal static class FluentRules
{
    /// <summary>Rejects blank authoring keys.</summary>
    internal static void Key(string key) => ArgumentException.ThrowIfNullOrWhiteSpace(key);

    /// <summary>Checks nonnegative weight/variation bounds without overflowing.</summary>
    internal static void Weight(int weight, int variation = 0)
    {
        if (weight < 0 || variation < 0 || (long)weight + variation > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight and variation must be nonnegative and their sum must fit an integer.");
    }

    /// <summary>Checks numeric strategy costs, weights and inclusive random bounds.</summary>
    internal static void Numeric(int cost, int weight, int min = 0, int max = 0)
    {
        Weight(weight);
        if (cost <= 0 || weight == 0 || min < 0 || max < min)
            throw new ArgumentOutOfRangeException(nameof(cost), "Numeric values require positive cost/weight and nonnegative ordered bounds.");
    }

    /// <summary>Appends a copied request condition after validating its authoring arguments.</summary>
    internal static void Condition(List<GenerationCondition> target, string key, ConditionOperator @operator, string value)
    {
        Key(key);
        ArgumentNullException.ThrowIfNull(value);
        if (!Enum.IsDefined(@operator))
            throw new ArgumentOutOfRangeException(nameof(@operator));
        target.Add(new GenerationCondition { Key = key, Operator = @operator, Value = value });
    }

    /// <summary>Validates tags before appending their values to an independently owned list.</summary>
    internal static void Tags(List<string> target, string[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var tag in tags)
            Key(tag);
        target.AddRange(tags);
    }

    /// <summary>Checks global output and sibling identities against the current tree, including manual edits.</summary>
    internal static void Identity(GenerationTemplate template, GenerationNode node, string id, bool output)
    {
        Key(id);
        if (node.Items.Any(x => x?.Id == id))
            throw new InvalidOperationException($"Sibling ID '{id}' already exists.");
        if (!output)
            return;

        // Inspect the live graph rather than caching identities across manual edits.
        var nodes = new Stack<GenerationNode>();
        var seen = new HashSet<GenerationNode>(ReferenceEqualityComparer.Instance);
        nodes.Push(template);
        while (nodes.TryPop(out var current))
        {
            if (!seen.Add(current))
                throw new InvalidOperationException("The authored tree contains a cycle or reused group.");
            foreach (var entry in current.Items)
            {
                if (entry is GenerationValue && entry.Id == id)
                    throw new InvalidOperationException($"Output ID '{id}' already exists.");
                if (entry is GenerationGroup group)
                    nodes.Push(group);
            }
        }
    }

    /// <summary>Appends a new group with a distinct entry instance.</summary>
    internal static GenerationGroup Group(GenerationTemplate template, GenerationNode node, string id, int weight)
    {
        Weight(weight);
        Identity(template, node, id, false);
        var group = new GenerationGroup { Id = id, Weight = weight };
        node.Items.Add(group);
        return group;
    }

    /// <summary>Runs a named-output callback against the referenced node.</summary>
    internal static void Value(GenerationTemplate template, GenerationNode node, string id, Action<GenerationValueBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Key(id);
        if (id.StartsWith("fluent/", StringComparison.Ordinal))
            throw new ArgumentException("Explicit output IDs cannot use the reserved 'fluent/' prefix.", nameof(id));
        Identity(template, node, id, true);
        var builder = new GenerationValueBuilder(template, node, id);
        configure(builder);
        builder.Complete();
    }

    /// <summary>Formats finite numeric literals as invariant exponent-free round-trippable text.</summary>
    internal static string Number(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Fixed numeric values must be finite.");

        // Expand the round-trip mantissa without changing its significant digits.
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentAt = text.IndexOf('E');
        if (exponentAt < 0)
            return text;
        var exponent = int.Parse(text[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        var mantissa = text[..exponentAt];
        var sign = mantissa.StartsWith('-') ? "-" : "";
        mantissa = mantissa.TrimStart('-');
        var decimalAt = mantissa.IndexOf('.');
        var digits = mantissa.Replace(".", "");
        var point = (decimalAt < 0 ? mantissa.Length : decimalAt) + exponent;
        var expanded = point <= 0 ? "0." + new string('0', -point) + digits
            : point >= digits.Length ? digits + new string('0', point - digits.Length)
            : digits.Insert(point, ".");
        var result = sign + expanded;
        if (!GenerationRules.Numeric(result, out var parsed) || parsed != value)
            throw new ArgumentException("The fixed value cannot be represented without rounding.", nameof(value));
        return result;
    }
}
