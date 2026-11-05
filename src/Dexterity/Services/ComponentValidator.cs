namespace Dexterity;

// Shared component data rules for registration and package validation.
internal sealed class ComponentValidator(World world)
{
    internal Result Validate(Component component)
    {
        var type = world.ComponentTypes.FirstOrDefault(x => x.Id == component.TypeId);
        var category = world.Categories.FirstOrDefault(x => x.Id == component.CategoryId);
        if (type == null || category == null || component.Name == null ||
            component.Stats == null || component.MetaData == null || component.Tags == null || component.ModifierData == null || component.Effects == null ||
            component.Tags.Any(x => x == null) || component.MetaData.Any(x => x.Value == null))
            return Result.Fail($"Component {component.Id} has an unknown type/category or incomplete state.", 400);
        if (type.StatIds.Any(id => world.Stats.All(x => x.Id != id)))
            return Result.Fail("Component type references an unknown stat.", 400);
        if (!component.Stats.Keys.ToHashSet().SetEquals(type.StatIds))
            return Result.Fail($"Component {component.Id} must contain exactly its type's declared stats.", 400);
        foreach (var pair in component.Stats)
        {
            var stat = pair.Value;
            if (stat == null || stat.TypeId != pair.Key || !EffectRules.Finite(stat.Max) || !EffectRules.Finite(stat.ModifiedMax) ||
                !EffectRules.Finite(stat.Current) || !EffectRules.Finite(stat.Bonus) || stat.Max < 0 || stat.ModifiedMax < 0 ||
                stat.Current < 0 || stat.Current > stat.ModifiedMax)
                return Result.Fail($"Component {component.Id} has invalid stat values.", 400);
        }
        if (category.Usable == Usable.Charges && (!world.ChargesStatId.HasValue || !component.Stats.ContainsKey(world.ChargesStatId.Value) ||
            !world.Stats.Any(x => x.Id == world.ChargesStatId.Value && x.Type == StatKind.Pool)))
            return Result.Fail("Charges components require the configured Pool charges stat.", 400);
        var validator = new WorldBuilder(world);
        foreach (var modifier in component.ModifierData)
        {
            if (modifier == null)
                return Result.Fail("Component modifiers cannot be null.", 400);
            if (validator.Validate(modifier).Failed(out var validation))
                return validation;
        }
        return Result.Ok();
    }
}
