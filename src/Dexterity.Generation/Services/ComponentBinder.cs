namespace Dexterity.Generation;

internal static class ComponentBinder
{
    /// <summary>Populates a detached component and returns explicitly supplied currents for initialization after recalculation.</summary>
    internal static Result<Dictionary<int, double>> Bind(World world, GenerationTemplate template, Dictionary<string, string> values, Component component)
    {
        // populate the component directly, preserving its detached identity and declared stats
        GenerationMapper.Apply(template, component);
        var currents = new Dictionary<int, double>();
        foreach (var binding in template.StatBindings)
        {
            if (!values.TryGetValue(binding.MaxKey, out var maximum))
                continue;
            if (!GenerationRules.Numeric(maximum, out var max) || max < 0)
                return GenerationRules.Fail<Dictionary<int, double>>($"Stat {binding.StatTypeId} requires a finite non-negative maximum.");
            var stat = component.Stats[binding.StatTypeId];
            stat.Max = max;
            stat.ModifiedMax = max;
            if (binding.CurrentKey != null)
            {
                if (!values.TryGetValue(binding.CurrentKey, out var text) || !GenerationRules.Numeric(text, out var number) || number < 0 || number > max)
                    return GenerationRules.Fail<Dictionary<int, double>>($"Stat {binding.StatTypeId} requires a current value within its initial maximum.");
                currents.Add(binding.StatTypeId, number);
                stat.Current = number;
            }
        }
        foreach (var binding in template.DataBindings)
        {
            if (values.TryGetValue(binding.ValueKey, out var text))
                component.MetaData.Add(binding.Key, text);
        }

        // omit incomplete optional modifiers; validate complete concrete copies
        var validator = new WorldBuilder(world);
        foreach (var definition in template.Modifiers)
        {
            if (definition.DataKeys.Values.Any(key => !values.ContainsKey(key)) || (definition.DurationKey != null && !values.ContainsKey(definition.DurationKey)))
                continue;
            var modifier = GenerationMapper.Copy(definition.Base);
            foreach (var pair in definition.DataKeys)
            {
                if (!GenerationRules.Numeric(values[pair.Value], out var number))
                    return GenerationRules.Fail<Dictionary<int, double>>($"Modifier parameter '{pair.Key}' requires finite dot-decimal text without separators.");
                modifier.Data[pair.Key] = number;
            }
            if (definition.DurationKey != null)
            {
                if (!GenerationRules.Numeric(values[definition.DurationKey], out var duration))
                    return GenerationRules.Fail<Dictionary<int, double>>("Modifier duration requires finite dot-decimal text without separators.");
                modifier.Duration = duration;
            }
            if (validator.Validate(modifier).Failed(out var validation))
                return GenerationRules.Failure<Dictionary<int, double>>(validation);
            component.ModifierData.Add(modifier);
        }
        return Result.Ok(currents);
    }
}
