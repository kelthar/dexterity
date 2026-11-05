namespace Dexterity.Generation;

/// <summary>Shares live parameter binding and optional duration configuration across typed modifiers.</summary>
public abstract class GenerationModifierBuilder<TSelf> where TSelf : GenerationModifierBuilder<TSelf>
{
    private readonly GenerationTemplate _template = null!;
    private readonly GenerationNode _node = null!;
    private readonly ModifierTemplate _modifier = null!;
    private readonly int _ordinal;

    /// <summary>References the appended modifier and its owning budget node.</summary>
    protected GenerationModifierBuilder(GenerationTemplate template, GenerationNode node, ModifierTemplate modifier, int ordinal)
    {
        _template = template;
        _node = node;
        _modifier = modifier;
        _ordinal = ordinal;
    }

    /// <summary>Optional generated duration; omission keeps the base duration at zero.</summary>
    public GenerationParameterBuilder<TSelf> Duration => new((TSelf)this, "duration");

    /// <summary>Appends and binds one parameter, rejecting repeated strategies and output collisions.</summary>
    internal TSelf Configure(string name, GenerationValue value, Action<GenerationEntryBuilder>? configure)
    {
        // Reject duplicates before editing either the budget tree or existing bindings.
        if (name == "duration" ? _modifier.DurationKey != null : _modifier.DataKeys.ContainsKey(name))
            throw new InvalidOperationException($"Parameter '{name}' already has a strategy.");
        value.Id = $"fluent/{_ordinal}/{name}";
        FluentRules.Identity(_template, _node, value.Id, true);

        // Make the live leaf/binding visible before its optional configuration callback.
        _node.Items.Add(value);
        if (name == "duration")
            _modifier.DurationKey = value.Id;
        else
            _modifier.DataKeys.Add(name, value.Id);
        configure?.Invoke(new GenerationEntryBuilder(value));
        return (TSelf)this;
    }
}
