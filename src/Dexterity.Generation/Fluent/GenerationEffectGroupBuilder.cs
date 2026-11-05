namespace Dexterity.Generation;

/// <summary>Configures a budget group with inherited, isolated effect settings.</summary>
public sealed class GenerationEffectGroupBuilder : GenerationEffectBuilder
{
    /// <summary>References a group and captures its parent's effect settings.</summary>
    internal GenerationEffectGroupBuilder(GenerationTemplate template, GenerationGroup group, ModifierData settings) : base(template, group, settings) { }

    /// <summary>Sets the group's ascending execution order.</summary>
    public GenerationEffectGroupBuilder Order(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        new GenerationEntryBuilder((GenerationGroup)Node).Order(value);
        return this;
    }

    /// <summary>Sets inclusive weight variation for numeric groups.</summary>
    public GenerationEffectGroupBuilder VaryWeightBy(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        new GenerationEntryBuilder((GenerationGroup)Node).VaryWeightBy(value);
        return this;
    }

    /// <summary>Sets generation-time branch inclusion; 0 and 100 mean always.</summary>
    public GenerationEffectGroupBuilder IncludeChance(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        new GenerationEntryBuilder((GenerationGroup)Node).IncludeChance(value);
        return this;
    }

    /// <summary>Appends an AND condition governing this group.</summary>
    public GenerationEffectGroupBuilder When(string key, ConditionOperator @operator, string value)
    {
        ArgumentNullException.ThrowIfNull(this);
        new GenerationEntryBuilder((GenerationGroup)Node).When(key, @operator, value);
        return this;
    }
}
