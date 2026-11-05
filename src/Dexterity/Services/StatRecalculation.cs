namespace Dexterity;

// Shared domain calculation for explicit service calls and repository deletion invariants.
internal static class StatRecalculation
{
    internal static Result Apply(World world, int componentId)
    {
        if (world.Repository.Get(componentId).Failed(out var lookup))
            return EffectRules.Failure(lookup);
        var component = lookup.Data!;
        if (component.State != ComponentState.Active)
            return Result.Fail("Deleted components cannot be recalculated for gameplay.", 409);

        // Collect only direct, active passives and validate before mutating stats.
        var modifiers = EffectRules.Passives(world, componentId)
            .SelectMany(holder => holder.ModifierData.Select((data, index) => (Holder: holder, Data: data, Index: index)))
            .Where(x => x.Data.Scope == Scope.Passive && x.Data.Phase == Phase.StatsRecalc).ToArray();
        var validator = new WorldBuilder(world);
        foreach (var modifier in modifiers)
            if (validator.Validate(modifier.Data).Failed(out var validation))
                return validation;
        var ordered = modifiers.OrderBy(x => world.Modifiers.First(t => t.Id == x.Data.TypeId).Order)
            .ThenBy(x => x.Holder.Id).ThenBy(x => x.Index);

        try
        {
            // Sum every flat bonus before updating any calculator input.
            var bonuses = new EffectResult();
            foreach (var modifier in ordered)
            {
                if (!modifier.Data.EffectTypeId.HasValue)
                    continue;
                bonuses = world.Modifiers.First(x => x.Id == modifier.Data.TypeId).Calculate(bonuses, modifier.Data, new EffectContext
                {
                    Phase = Phase.StatsRecalc, Trigger = Trigger.Recalc, OwnerId = componentId, EffectTypeId = modifier.Data.EffectTypeId
                }, modifier.Holder.Id);
            }
            foreach (var pair in component.Stats)
                pair.Value.Bonus = bonuses.SumStat(pair.Key, world);
            var calculators = world.Calculators.Where(x => x.ComponentTypeId == component.TypeId).OrderBy(x => x.Order).ToArray();
            foreach (var pair in component.Stats.Where(x => calculators.All(c => c.StatTypeId != x.Key)))
                if (Update(pair.Value, pair.Value.Max).Failed(out var updated))
                    return updated;
            foreach (var calculator in calculators)
            {
                if (!component.Stats.TryGetValue(calculator.StatTypeId, out var stat))
                    return Result.Fail("Calculator stat is absent from component.", 400);
                if (calculator.Calculate(componentId, world).Failed(out var calculated))
                    return EffectRules.Failure(calculated);
                if (Update(stat, calculated.Data).Failed(out var updated))
                    return updated;
            }
            return Result.Ok();
        }
        catch (Exception exception)
        {
            return EffectRules.Exception(exception, "Stat recalculation failed.");
        }
    }

    private static Result Update(Stat stat, double max)
    {
        if (!EffectRules.Finite(max) || !EffectRules.Finite(stat.Bonus) || !EffectRules.Finite(max + stat.Bonus))
            return Result.Fail("Calculated stat values must be finite.", 400);
        var modified = Math.Max(0, max + stat.Bonus);
        stat.Current = EffectRules.Clamp(stat.Current + modified - stat.ModifiedMax, modified);
        stat.Max = max;
        stat.ModifiedMax = modified;
        return Result.Ok();
    }
}
