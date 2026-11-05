namespace Dexterity;

public class WorldBuilder : IWorldBuilder
{
    private readonly World _world;
    public WorldBuilder(World world) => _world = world;

    public Result<World> Build(World world)
    {
        // validate definitions before wiring runtime defaults
        if (world == null)
            return Result.Fail("World is required.", 400);
        if (world.Stats == null || world.EffectTypes == null || world.ComponentTypes == null || world.Categories == null ||
            world.Slots == null || world.Modifiers == null || world.Calculators == null)
            return Result.Fail("World definition lists are required.", 400);
        var sets = new[]
        {
            ("stat", world.Stats.Select(x => x.Id)), ("effect type", world.EffectTypes.Select(x => x.Id)),
            ("component type", world.ComponentTypes.Select(x => x.Id)), ("category", world.Categories.Select(x => x.Id)),
            ("slot", world.Slots.Select(x => x.Id))
        };
        foreach (var (name, ids) in sets)
        {
            var duplicate = ids.GroupBy(x => x).FirstOrDefault(x => x.Count() > 1);
            if (duplicate != null)
                return Result.Fail($"Duplicate {name} Id {duplicate.Key}.", 400);
        }
        if (world.Modifiers.Any(x => string.IsNullOrWhiteSpace(x.Id)))
            return Result.Fail("Modifier type Id is required.", 400);
        var duplicateModifier = world.Modifiers.GroupBy(x => x.Id).FirstOrDefault(x => x.Count() > 1);
        if (duplicateModifier != null)
            return Result.Fail($"Duplicate modifier type Id {duplicateModifier.Key}.", 400);
        if (world.EffectTypes.Any(x => world.Stats.All(s => s.Id != x.StatTypeId)))
            return Result.Fail("An effect type references an unknown stat.", 400);
        if (world.ComponentTypes.Any(x => x.StatIds.Any(id => world.Stats.All(s => s.Id != id))))
            return Result.Fail("A component type references an unknown stat.", 400);
        foreach (var category in world.Categories)
        {
            var visited = new HashSet<int>();
            ComponentCategory? current = category;
            while (current != null)
            {
                if (!visited.Add(current.Id))
                    return Result.Fail($"Category cycle at {current.Name}.", 400);
                if (!current.ParentCategoryId.HasValue)
                    break;
                var parentId = current.ParentCategoryId.Value;
                current = world.Categories.FirstOrDefault(x => x.Id == parentId);
                if (current == null)
                    return Result.Fail($"Category {category.Name} has unknown parent {parentId}.", 400);
            }
        }
        if (world.Slots.Any(x => x.Categories != null && x.Categories.Any(id => world.Categories.All(cat => cat.Id != id))))
            return Result.Fail("An equipment slot references an unknown category.", 400);
        if (world.Calculators.Any(x => world.Stats.All(s => s.Id != x.StatTypeId) || world.ComponentTypes.All(t => t.Id != x.ComponentTypeId)))
            return Result.Fail("A calculator references an unknown stat or component type.", 400);
        if (world.Calculators.GroupBy(x => (x.StatTypeId, x.ComponentTypeId)).Any(x => x.Count() > 1))
            return Result.Fail("Duplicate calculator for the same stat and component type.", 400);
        if (world.Categories.Any(x => x.Usable == Usable.Charges) &&
            !world.Stats.Any(x => x.Id == world.ChargesStatId && x.Type == StatKind.Pool))
            return Result.Fail("Charges categories require a named Pool charges stat.", 400);

        // validate existing components with this world's definitions
        var validator = new WorldBuilder(world);
        if (world.Repository != null)
        {
            foreach (var component in world.Repository.Items.Values)
                foreach (var modifier in component.ModifierData)
                    if (validator.Validate(modifier).Failed(out var validation))
                        return validation;
        }

        world.Random ??= new DefaultRandomSource();
        world.Repository ??= new ComponentRepository(world);
        world.Logic ??= new DefaultWorldLogic(world);
        return Result.Ok(world);
    }

    public Result Validate(ModifierData modifier)
    {
        // validate identity and phase
        if (modifier == null)
            return Result.Fail("Modifier is required.", 400);
        if (modifier.Data == null || modifier.IncludedTags == null || modifier.ExcludedTags == null)
            return Result.Fail("Modifier data and tag lists are required.", 400);
        if (string.IsNullOrWhiteSpace(modifier.TypeId) || _world.Modifiers.All(x => x.Id != modifier.TypeId))
            return Result.Fail($"Unknown modifier type {modifier.TypeId}.", 400);
        if (modifier.EffectTypeId.HasValue && _world.EffectTypes.All(x => x.Id != modifier.EffectTypeId))
            return Result.Fail($"Unknown effect type {modifier.EffectTypeId}.", 400);
        if (!Enum.IsDefined(typeof(Phase), modifier.Phase) || !Enum.IsDefined(typeof(Scope), modifier.Scope) ||
            (modifier.RelationKind & ~(RelationKind.Self | RelationKind.Friends | RelationKind.Enemies)) != 0)
            return Result.Fail("Invalid modifier phase, scope or relation.", 400);
        if (modifier.Scope == Scope.Use && modifier.Phase != Phase.Outgoing)
            return Result.Fail("Use modifiers require the Outgoing phase.", 400);
        if (modifier.Phase == Phase.StatsRecalc && (modifier.IncludedTags.Count != 0 || modifier.ExcludedTags.Count != 0 || modifier.RelationKind != RelationKind.None))
            return Result.Fail("StatsRecalc modifiers cannot filter tags or relations.", 400);
        if (modifier.Phase == Phase.StatsRecalc && modifier.TypeId != "decrease" && modifier.TypeId != "increase")
            return Result.Fail("StatsRecalc only supports Increase and Decrease.", 400);

        // validate data, including non-finite values before arithmetic
        if (modifier.Data == null || !EffectRules.Finite(modifier.Duration) || modifier.Duration < 0 || modifier.Data.Values.Any(x => !EffectRules.Finite(x)))
            return Result.Fail("Modifier data and duration must be finite, with non-negative duration.", 400);
        string[] required = modifier.TypeId switch
        {
            "decrease" or "increase" => new[] { "value" }, "randomdecrease" or "randomincrease" => new[] { "min", "max" },
            "multiplier" => new[] { "multiplier" }, "critical" => new[] { "chance", "multiplier" },
            "splash" => new[] { "range", "percent" }, "preventflat" => new[] { "amount" },
            "preventpercent" or "steal" => new[] { "percent" }, _ => Array.Empty<string>()
        };
        foreach (var key in required)
            if (!modifier.Data.ContainsKey(key))
                return Result.Fail($"Modifier {modifier.TypeId} requires '{key}'.", 400);
        foreach (var key in new[] { "percent", "chance" })
            if (modifier.Data.TryGetValue(key, out var value) && (value < 0 || value > 100))
                return Result.Fail($"'{key}' must be between 0 and 100.", 400);
        foreach (var key in new[] { "value", "amount", "range", "multiplier", "min", "max" })
            if (modifier.Data.TryGetValue(key, out var value) && value < 0)
                return Result.Fail($"'{key}' cannot be negative.", 400);
        if (modifier.Data.TryGetValue("min", out var min) && modifier.Data.TryGetValue("max", out var max) && min > max)
            return Result.Fail("Random min cannot exceed max.", 400);
        return Result.Ok();
    }
}
