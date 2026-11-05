namespace Dexterity;

internal static class EffectRules
{
    internal static Result<T> Failure<T>(IResult source)
    {
        var failure = Failure(source);
        return new Result<T> { Success = false, Code = failure.Code, States = failure.States };
    }

    internal static Result Failure(IResult source)
    {
        var failure = Result.Fail(source.States.LastOrDefault()?.Text ?? "Operation failed.",
            source.Code != 0 ? source.Code : source.States.LastOrDefault()?.Code ?? 400);
        if (source.States.Count > 0)
            failure.States = source.States.ToList();
        return failure;
    }

    internal static Result Exception(Exception exception, string message)
    {
        System.Diagnostics.Trace.TraceError("{0}: {1}", message, exception);
        return Result.Fail(message, 500).Add(StateType.Error, exception.Message, 500, exception);
    }
    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    internal static double Clamp(double value, double max) => Math.Max(0, Math.Min(value, Math.Max(0, max)));
    internal static EffectResult Copy(EffectResult result, IEnumerable<EffectResultItem>? items = null) => new()
    {
        TargetId = result.TargetId, SourceId = result.SourceId, InstrumentId = result.InstrumentId,
        Items = (items ?? result.Items).Select(x => x with { }).ToList()
    };
    internal static bool Descends(World world, int categoryId, int ancestorId)
    {
        var seen = new HashSet<int>();
        int? current = categoryId;
        while (current.HasValue && seen.Add(current.Value))
        {
            if (current == ancestorId)
                return true;
            current = world.Categories.FirstOrDefault(x => x.Id == current)?.ParentCategoryId;
        }
        return false;
    }
    internal static IEnumerable<Component> Passives(World world, int parentId) => world.Repository.Relations
        .Where(x => x.ParentId == parentId && x.Type != RelationType.Inventory)
        .Select(x => x.TargetId).Distinct().Where(world.Repository.Items.ContainsKey)
        .Select(id => world.Repository.Items[id]).Where(x => x.State == ComponentState.Active).OrderBy(x => x.Id);

    internal static bool Matches(ModifierData data, RelationKind relation, IEnumerable<string> tags)
    {
        if (!data.EffectTypeId.HasValue)
            return false;
        if (data.RelationKind != RelationKind.None && (data.RelationKind & relation) == 0)
            return false;
        var set = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
        return !data.ExcludedTags.Any(set.Contains) && (data.IncludedTags.Count == 0 || data.IncludedTags.Any(set.Contains));
    }
}
