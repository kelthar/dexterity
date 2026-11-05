namespace Dexterity.Generation;

/// <summary>Direct fluent authoring extensions for existing generation templates.</summary>
public static class FluentGenerationExtensions
{
    /// <summary>Opens an effect scope with fresh outgoing-use enemy settings, without adding a group.</summary>
    public static GenerationEffectBuilder EffectType(this GenerationTemplate template, int id)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new GenerationEffectBuilder(template, template, new ModifierData { EffectTypeId = id, Scope = Scope.Use, Phase = Phase.Outgoing, RelationKind = RelationKind.Enemies });
    }

    /// <summary>Configures an effect synchronously and returns the original template.</summary>
    public static GenerationTemplate EffectType(this GenerationTemplate template, int id, Action<GenerationEffectBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(configure);
        configure(template.EffectType(id));
        return template;
    }

    /// <summary>Appends and configures an explicit root budget group.</summary>
    public static GenerationTemplate Group(this GenerationTemplate template, string id, int weight, Action<GenerationGroupBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(configure);
        var group = FluentRules.Group(template, template, id, weight);
        configure(new GenerationGroupBuilder(template, group));
        return template;
    }

    /// <summary>Appends a named root output and returns the original template.</summary>
    public static GenerationTemplate Value(this GenerationTemplate template, string id, Action<GenerationValueBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(template);
        FluentRules.Value(template, template, id, configure);
        return template;
    }

    /// <summary>Appends a root eligibility condition against request context.</summary>
    public static GenerationTemplate When(this GenerationTemplate template, string key, ConditionOperator @operator, string value)
    {
        ArgumentNullException.ThrowIfNull(template);
        FluentRules.Condition(template.Conditions, key, @operator, value);
        return template;
    }

    /// <summary>Appends discovery tags without turning them into component instance tags.</summary>
    public static GenerationTemplate Tags(this GenerationTemplate template, params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(template);
        FluentRules.Tags(template.Tags, tags);
        return template;
    }

    /// <summary>Appends tags copied to generated component instances.</summary>
    public static GenerationTemplate ComponentTags(this GenerationTemplate template, params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(template);
        FluentRules.Tags(template.ComponentTags, tags);
        return template;
    }

    /// <summary>Binds an existing output key to a declared stat maximum and optional initial current.</summary>
    public static GenerationTemplate BindStat(this GenerationTemplate template, int statTypeId, string maxKey, string? currentKey = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        FluentRules.Key(maxKey);
        if (currentKey != null)
            FluentRules.Key(currentKey);
        if (template.StatBindings.Any(x => x.StatTypeId == statTypeId))
            throw new InvalidOperationException($"Stat '{statTypeId}' already has a binding.");
        template.StatBindings.Add(new StatBinding { StatTypeId = statTypeId, MaxKey = maxKey, CurrentKey = currentKey });
        return template;
    }

    /// <summary>Binds generated text to an ordinary component data key.</summary>
    public static GenerationTemplate BindData(this GenerationTemplate template, string key, string valueKey)
    {
        ArgumentNullException.ThrowIfNull(template);
        FluentRules.Key(key);
        FluentRules.Key(valueKey);
        if (template.DataBindings.Any(x => x.Key == key))
            throw new InvalidOperationException($"Data key '{key}' already has a binding.");
        template.DataBindings.Add(new DataBinding { Key = key, ValueKey = valueKey });
        return template;
    }
}
