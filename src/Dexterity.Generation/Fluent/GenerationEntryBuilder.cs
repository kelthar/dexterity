namespace Dexterity.Generation;

/// <summary>Configures the inclusion, ordering and weight variation of a live generation entry.</summary>
public sealed class GenerationEntryBuilder
{
    private readonly IGenerationEntry _entry = null!;

    /// <summary>References an existing value or group.</summary>
    internal GenerationEntryBuilder(IGenerationEntry entry) => _entry = entry;

    /// <summary>Sets ascending execution order; list order breaks ties.</summary>
    public GenerationEntryBuilder Order(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        _entry.Order = value;
        return this;
    }

    /// <summary>Sets the maximum inclusive random addition to the base weight.</summary>
    public GenerationEntryBuilder VaryWeightBy(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Weight(_entry.Weight, value);
        _entry.VaryWeightBy = value;
        return this;
    }

    /// <summary>Sets generation-time inclusion percent; both 0 and 100 mean always.</summary>
    public GenerationEntryBuilder IncludeChance(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        if (value is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(value), "Inclusion must be between 0 and 100 percent.");
        _entry.ChanceToGet = value;
        return this;
    }

    /// <summary>Appends an AND condition against the request context, not generated values.</summary>
    public GenerationEntryBuilder When(string key, ConditionOperator @operator, string value)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Condition(_entry.Conditions, key, @operator, value);
        return this;
    }
}
