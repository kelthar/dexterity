using System.Globalization;

namespace Dexterity.Generation;

internal static class GenerationEngine
{
    /// <summary>Traverses a validated template depth-first, applying conditions and chance gates before distributing budgets to the supplied numeric participants.</summary>
    internal static Result<GenerationResult> Generate(GenerationTemplate template, GenerationRequest request, HashSet<IGenerationEntry> participants, IGenerationRandom random)
    {
        // evaluate request context independently of the caller's dictionary comparer
        var context = new Dictionary<string, string>(request.Context, StringComparer.Ordinal);
        if (!GenerationRules.Matches(template.Conditions, context))
            return GenerationRules.Fail<GenerationResult>("The template is not eligible for this context.", 409);
        var result = new GenerationResult { TemplateId = template.Id, RequestedWorth = request.Worth };

        // traverse depth-first without recursive calls on authored trees
        var work = new Stack<(object Entry, int Worth)>();
        work.Push((template, request.Worth));
        while (work.TryPop(out var step))
        {
            if (step.Entry is GenerationValue value)
            {
                if (GenerateValue(value, step.Worth, result, random).Failed(out var generated))
                    return GenerationRules.Failure<GenerationResult>(generated);
                continue;
            }
            var node = (GenerationNode)step.Entry;
            var accepted = node.Items.OrderBy(x => x.Order)
                .Where(x => GenerationRules.Matches(x.Conditions, context))
                .Where(x => x.ChanceToGet is 0 or 100 || GenerationRules.Draw(random, 0, 99) < x.ChanceToGet).ToArray();

            // sample direct weights only after all direct chance gates
            var weighted = accepted.Where(participants.Contains)
                .Select(x => (Entry: x, Weight: x.Weight + (x.VaryWeightBy == 0 ? 0 : GenerationRules.Draw(random, 0, x.VaryWeightBy)))).ToArray();
            var shares = Allocate(step.Worth, weighted);
            for (var index = accepted.Length - 1; index >= 0; index--)
            {
                var entry = accepted[index];
                work.Push((entry, shares.GetValueOrDefault(entry)));
            }
        }

        // report net accounting, not summed local remainders
        result.UnusedWorth = Math.Max(0, result.RequestedWorth - result.TotalWorth);
        result.Overspend = Math.Max(0, result.TotalWorth - result.RequestedWorth);
        return Result.Ok(result);
    }

    /// <summary>Splits an integer budget proportionally by positive weight, awarding leftover units by largest remainder with input order breaking ties.</summary>
    private static Dictionary<IGenerationEntry, int> Allocate(int worth, (IGenerationEntry Entry, int Weight)[] entries)
    {
        var shares = new Dictionary<IGenerationEntry, int>(ReferenceEqualityComparer.Instance);
        if (entries.Length == 0)
            return shares;

        // floor exact shares; all remainder denominators are identical
        var total = entries.Sum(x => (long)x.Weight);
        var remainders = new List<(IGenerationEntry Entry, long Remainder)>();
        var assigned = 0;
        foreach (var entry in entries)
        {
            var numerator = (long)worth * entry.Weight;
            var share = (int)(numerator / total);
            shares.Add(entry.Entry, share);
            assigned += share;
            remainders.Add((entry.Entry, numerator % total));
        }
        foreach (var remainder in remainders.OrderByDescending(x => x.Remainder).Take(worth - assigned))
            shares[remainder.Entry]++;
        return shares;
    }

    /// <summary>Emits one fixed or numeric value and updates total worth; random minima may exceed the allocated share, and integer cost overflow fails.</summary>
    private static Result GenerateValue(GenerationValue value, int worth, GenerationResult result, IGenerationRandom random)
    {
        if (value.Kind == GenerationValueKind.Fixed)
        {
            result.Values.Add(value.Id, value.Value!);
            return Result.Ok();
        }

        // random minima deliberately override affordability
        var amount = worth / value.WorthPerValue;
        if (value.Kind == GenerationValueKind.Random)
        {
            var sample = value.MinValue == value.MaxValue ? value.MinValue : GenerationRules.Draw(random, value.MinValue, value.MaxValue);
            amount = Math.Max(value.MinValue, Math.Min(sample, amount));
        }
        var cost = (long)amount * value.WorthPerValue;
        if (cost + result.TotalWorth > int.MaxValue)
            return Result.Fail("Generated worth exceeds the supported integer range.", 400);
        result.TotalWorth += (int)cost;
        result.Values.Add(value.Id, amount.ToString(CultureInfo.InvariantCulture));
        return Result.Ok();
    }
}
