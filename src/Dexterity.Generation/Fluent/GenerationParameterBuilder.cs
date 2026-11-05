namespace Dexterity.Generation;

/// <summary>Selects one generation strategy for a typed numeric parameter.</summary>
public sealed class GenerationParameterBuilder<TModifier> where TModifier : GenerationModifierBuilder<TModifier>
{
    private readonly TModifier _owner = null!;
    private readonly string _name = null!;

    /// <summary>References an owning modifier and runtime parameter name.</summary>
    internal GenerationParameterBuilder(TModifier owner, string name)
    {
        _owner = owner;
        _name = name;
    }

    /// <summary>Adds a free finite numeric literal, supporting fractions without exponent notation.</summary>
    public TModifier Fixed(double value, Action<GenerationEntryBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(this);
        var text = FluentRules.Number(value);
        return _owner.Configure(_name, new GenerationValue { Kind = GenerationValueKind.Fixed, WorthPerValue = 0, Value = text }, configure);
    }

    /// <summary>Generates whole units from a relative budget share divided by the positive unit cost.</summary>
    public TModifier Weight(int worthPerValue, int weight = 1, Action<GenerationEntryBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Numeric(worthPerValue, weight);
        return _owner.Configure(_name, new GenerationValue { WorthPerValue = worthPerValue, Weight = weight }, configure);
    }

    /// <summary>Samples inclusive integer bounds, caps by affordability and honors the minimum even when it overspends.</summary>
    public TModifier Random(int worthPerValue, int min, int max, int weight = 1, Action<GenerationEntryBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Numeric(worthPerValue, weight, min, max);
        return _owner.Configure(_name, new GenerationValue { Kind = GenerationValueKind.Random, WorthPerValue = worthPerValue, Weight = weight, MinValue = min, MaxValue = max }, configure);
    }
}
