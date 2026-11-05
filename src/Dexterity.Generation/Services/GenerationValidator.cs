namespace Dexterity.Generation;

internal sealed class GenerationValidator(World world, GenerationCatalog catalog)
{
    /// <summary>Checks required world/catalog collections and unique definition identities without modifying configuration or consuming randomness.</summary>
    internal Result CheckCatalog()
    {
        // validate environment and catalog identities without wiring defaults
        if (world == null || world.Repository == null || catalog?.Templates == null ||
            world.Stats == null || world.ComponentTypes == null || world.Categories == null ||
            world.EffectTypes == null || world.Modifiers == null || world.Calculators == null)
            return Result.Fail("A configured world, repository and catalog are required.", 400);
        if (catalog.Templates.Any(x => x == null || string.IsNullOrWhiteSpace(x.Id)) ||
            catalog.Templates.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1))
            return Result.Fail("Template IDs must be non-empty and unique.", 400);
        if (world.Stats.Any(x => x == null || !Enum.IsDefined(x.Type)) || world.ComponentTypes.Any(x => x == null) ||
            world.Categories.Any(x => x == null) || world.EffectTypes.Any(x => x == null) ||
            world.Modifiers.Any(x => x == null) || world.Calculators.Any(x => x == null))
            return Result.Fail("World definitions cannot contain null or invalid entries.", 400);
        var identities = new[] { world.Stats.Select(x => x.Id), world.ComponentTypes.Select(x => x.Id), world.Categories.Select(x => x.Id), world.EffectTypes.Select(x => x.Id) };
        if (identities.Any(ids => ids.GroupBy(x => x).Any(g => g.Count() > 1)) ||
            world.Modifiers.Any(x => string.IsNullOrWhiteSpace(x.Id)) || world.Modifiers.GroupBy(x => x.Id, StringComparer.Ordinal).Any(g => g.Count() > 1))
            return Result.Fail("World definition IDs must be unique.", 400);
        return Result.Ok();
    }

    /// <summary>Checks catalog integrity and resolves an ordinal template ID, returning 404 when no template matches.</summary>
    internal Result<GenerationTemplate> Lookup(string id)
    {
        if (CheckCatalog().Failed(out var catalogResult))
            return GenerationRules.Failure<GenerationTemplate>(catalogResult);
        if (string.IsNullOrWhiteSpace(id))
            return GenerationRules.Fail<GenerationTemplate>("Template ID is required.");
        var template = catalog.Templates.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        if (template == null)
            return GenerationRules.Fail<GenerationTemplate>($"Template '{id}' was not found.", 404);
        return Result.Ok(template);
    }

    /// <summary>Validates the full authored tree and bindings, rejecting cycles and reused entries, and returns entries that participate in budget allocation.</summary>
    internal Result<HashSet<IGenerationEntry>> Template(GenerationTemplate template)
    {
        // validate referenced world definitions
        if (CheckTemplate(template).Failed(out var definition))
            return GenerationRules.Failure<HashSet<IGenerationEntry>>(definition);

        // walk the full authored tree, including ineligible branches
        var seen = new HashSet<IGenerationEntry>(ReferenceEqualityComparer.Instance);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<GenerationGroup>();
        var participants = new HashSet<IGenerationEntry>(ReferenceEqualityComparer.Instance);
        var nodes = new Stack<GenerationNode>();
        nodes.Push(template);
        while (nodes.TryPop(out var node))
        {
            if (node.Items == null)
                return GenerationRules.Fail<HashSet<IGenerationEntry>>("Node items are required.");
            var siblings = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in node.Items)
            {
                if (entry == null || !seen.Add(entry) || string.IsNullOrWhiteSpace(entry.Id) || !siblings.Add(entry.Id))
                    return GenerationRules.Fail<HashSet<IGenerationEntry>>("Entries require unique sibling IDs and a non-cyclic tree without reused instances.");
                if (CheckEntry(entry).Failed(out var entryResult))
                    return GenerationRules.Failure<HashSet<IGenerationEntry>>(entryResult);
                if (entry is GenerationGroup group)
                {
                    groups.Add(group);
                    nodes.Push(group);
                    continue;
                }
                if (entry is not GenerationValue value || !keys.Add(entry.Id))
                    return GenerationRules.Fail<HashSet<IGenerationEntry>>("Entries must be groups or values with globally unique output keys.");
                if (!Enum.IsDefined(value.Kind) || value.WorthPerValue < 0 || value.MinValue < 0 || value.MaxValue < 0)
                    return GenerationRules.Fail<HashSet<IGenerationEntry>>("Value kind and numeric fields are invalid.");
                if (value.Kind == GenerationValueKind.Fixed)
                {
                    if (value.Value == null)
                        return GenerationRules.Fail<HashSet<IGenerationEntry>>("Fixed values require text.");
                    continue;
                }
                if (value.WorthPerValue == 0 || (value.Kind == GenerationValueKind.Random && value.MinValue > value.MaxValue))
                    return GenerationRules.Fail<HashSet<IGenerationEntry>>("Numeric values require a positive cost and valid random bounds.");
                participants.Add(value);
            }
        }

        // classify metadata-only groups bottom-up without evaluating their gates
        for (var index = groups.Count - 1; index >= 0; index--)
        {
            var group = groups[index];
            if (group.Items.Any(participants.Contains))
                participants.Add(group);
        }
        if (participants.Any(x => x.Weight <= 0))
            return GenerationRules.Fail<HashSet<IGenerationEntry>>("Budget participants require positive weights.");
        if (CheckBindings(template, keys).Failed(out var bindings))
            return GenerationRules.Failure<HashSet<IGenerationEntry>>(bindings);
        return Result.Ok(participants);
    }

    /// <summary>Validates template metadata, referenced world definitions, category ancestry, charges configuration, calculators and root conditions.</summary>
    private Result CheckTemplate(GenerationTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Name) || !Tags(template.Tags) || !Tags(template.ComponentTags) ||
            template.StatBindings == null || template.DataBindings == null || template.Modifiers == null)
            return Result.Fail("Template name, tags and binding lists are required.", 400);
        var type = world.ComponentTypes.FirstOrDefault(x => x.Id == template.TypeId);
        var category = world.Categories.FirstOrDefault(x => x.Id == template.CategoryId);
        if (type?.StatIds == null || category == null || !Tags(type.Tags) || type.StatIds.Any(id => world.Stats.All(x => x.Id != id)))
            return Result.Fail("Template type/category or type stats are invalid.", 400);
        var ancestors = new HashSet<int>();
        while (category != null)
        {
            if (!ancestors.Add(category.Id) || !Tags(category.Tags) || !Enum.IsDefined(category.Usable))
                return Result.Fail("Category ancestry, tags or usage is invalid.", 400);
            if (!category.ParentCategoryId.HasValue)
                break;
            category = world.Categories.FirstOrDefault(x => x.Id == category.ParentCategoryId.Value);
            if (category == null)
                return Result.Fail("Category has an unknown parent.", 400);
        }
        var ownCategory = world.Categories.First(x => x.Id == template.CategoryId);
        if (ownCategory.Usable == Usable.Charges && (!world.ChargesStatId.HasValue || !type.StatIds.Contains(world.ChargesStatId.Value) ||
            !world.Stats.Any(x => x.Id == world.ChargesStatId && x.Type == StatKind.Pool)))
            return Result.Fail("Charges categories require the type's configured Pool charges stat.", 400);
        var calculators = world.Calculators.Where(x => x.ComponentTypeId == type.Id).ToArray();
        if (calculators.Any(x => !type.StatIds.Contains(x.StatTypeId)) || calculators.GroupBy(x => x.StatTypeId).Any(x => x.Count() > 1))
            return Result.Fail("Calculators require unique stats declared by the template's type.", 400);
        return Conditions(template.Conditions);
    }

    /// <summary>Checks non-negative weights/variation, combined-weight overflow, percentage bounds and entry conditions.</summary>
    private static Result CheckEntry(IGenerationEntry entry)
    {
        if (entry.Weight < 0 || entry.VaryWeightBy < 0 || (long)entry.Weight + entry.VaryWeightBy > int.MaxValue || entry.ChanceToGet is < 0 or > 100)
            return Result.Fail("Entry weight, variation or chance is invalid.", 400);
        return Conditions(entry.Conditions);
    }

    /// <summary>Checks condition keys and operators, requiring strict finite numeric operands for ordering comparisons.</summary>
    private static Result Conditions(List<GenerationCondition> conditions)
    {
        if (conditions == null)
            return Result.Fail("Conditions are required.", 400);
        foreach (var condition in conditions)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.Key) || condition.Value == null || !Enum.IsDefined(condition.Operator))
                return Result.Fail("Condition key, value and operator are required.", 400);
            if (condition.Operator is not (ConditionOperator.Equal or ConditionOperator.NotEqual) && !GenerationRules.Numeric(condition.Value, out _))
                return Result.Fail("Numeric condition operands require finite dot-decimal text without separators.", 400);
        }
        return Result.Ok();
    }

    /// <summary>Checks binding destinations and authored output keys, then validates modifier shapes using placeholders for generated numeric fields.</summary>
    private Result CheckBindings(GenerationTemplate template, HashSet<string> keys)
    {
        // validate binding identities and source keys
        // accept only non-empty keys declared by a value in the authored tree
        bool Source(string? key) => !string.IsNullOrWhiteSpace(key) && keys.Contains(key);
        var stats = world.ComponentTypes.First(x => x.Id == template.TypeId).StatIds;
        var statIds = new HashSet<int>();
        foreach (var binding in template.StatBindings)
        {
            if (binding == null || !statIds.Add(binding.StatTypeId) || !stats.Contains(binding.StatTypeId) || !Source(binding.MaxKey) ||
                (binding.CurrentKey != null && !Source(binding.CurrentKey)))
                return Result.Fail("Stat bindings require unique declared stats and known output keys.", 400);
        }
        var dataKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in template.DataBindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Key) || !dataKeys.Add(binding.Key) || !Source(binding.ValueKey))
                return Result.Fail("Data bindings require unique destination keys and known output keys.", 400);
        }
        foreach (var modifier in template.Modifiers)
        {
            if (modifier?.Base?.Data == null || modifier.Base.IncludedTags == null || modifier.Base.ExcludedTags == null || modifier.DataKeys == null ||
                modifier.DataKeys.Any(x => string.IsNullOrWhiteSpace(x.Key) || !Source(x.Value)) || (modifier.DurationKey != null && !Source(modifier.DurationKey)))
                return Result.Fail("Modifier definitions require data, tags and known output keys.", 400);

            // validate the merged shape with legal placeholders for generated fields
            var probe = GenerationMapper.Copy(modifier.Base);
            foreach (var key in modifier.DataKeys.Keys)
                probe.Data[key] = 0;
            if (modifier.DataKeys.ContainsKey("max") && probe.Data.TryGetValue("min", out var min))
                probe.Data["max"] = Math.Max(0, min);
            if (modifier.DurationKey != null)
                probe.Duration = 0;
            if (new WorldBuilder(world).Validate(probe).Failed(out var validation))
                return validation;
            if (probe.EffectTypeId.HasValue && world.EffectTypes.Any(x => x.Id == probe.EffectTypeId && world.Stats.All(s => s.Id != x.StatTypeId)))
                return Result.Fail("Modifier effect type references an unknown stat.", 400);
        }
        return Result.Ok();
    }

    /// <summary>Accepts an empty tag list but rejects a null list or any blank tag.</summary>
    internal static bool Tags(List<string> tags) => tags != null && tags.All(x => !string.IsNullOrWhiteSpace(x));
}
