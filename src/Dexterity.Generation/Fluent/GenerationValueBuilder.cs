namespace Dexterity.Generation;

/// <summary>Configures a named output directly on a budget node.</summary>
public sealed class GenerationValueBuilder
{
    private readonly GenerationTemplate _template = null!;
    private readonly GenerationNode _node = null!;
    private readonly string _id = null!;
    private bool _configured;

    /// <summary>References the destination node and explicit output ID.</summary>
    internal GenerationValueBuilder(GenerationTemplate template, GenerationNode node, string id)
    {
        _template = template;
        _node = node;
        _id = id;
    }

    /// <summary>Adds free literal text, preserved verbatim by ordinary data bindings.</summary>
    public GenerationValueBuilder Fixed(string value, Action<GenerationEntryBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(this);
        ArgumentNullException.ThrowIfNull(value);
        return Configure(new GenerationValue { Kind = GenerationValueKind.Fixed, WorthPerValue = 0, Value = value }, configure);
    }

    /// <summary>Generates whole numeric units from this output's budget share.</summary>
    public GenerationValueBuilder Weight(int worthPerValue, int weight = 1, Action<GenerationEntryBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Numeric(worthPerValue, weight);
        return Configure(new GenerationValue { WorthPerValue = worthPerValue, Weight = weight }, configure);
    }

    /// <summary>Generates a bounded budget-dependent integer, allowing minimum overspend.</summary>
    public GenerationValueBuilder Random(int worthPerValue, int min, int max, int weight = 1, Action<GenerationEntryBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Numeric(worthPerValue, weight, min, max);
        return Configure(new GenerationValue { Kind = GenerationValueKind.Random, WorthPerValue = worthPerValue, Weight = weight, MinValue = min, MaxValue = max }, configure);
    }

    /// <summary>Appends a single strategy without replacing a prior configured output.</summary>
    private GenerationValueBuilder Configure(GenerationValue value, Action<GenerationEntryBuilder>? configure)
    {
        if (_configured)
            throw new InvalidOperationException($"Value '{_id}' already has a strategy.");
        FluentRules.Identity(_template, _node, _id, true);
        value.Id = _id;
        _node.Items.Add(value);
        _configured = true;
        configure?.Invoke(new GenerationEntryBuilder(value));
        return this;
    }

    /// <summary>Rejects a named value callback that did not choose a strategy.</summary>
    internal void Complete()
    {
        if (!_configured)
            throw new InvalidOperationException($"Value '{_id}' requires a strategy.");
    }
}
