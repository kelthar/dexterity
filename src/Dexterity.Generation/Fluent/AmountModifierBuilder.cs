namespace Dexterity.Generation;

/// <summary>Configures flat incoming prevention.</summary>
public sealed class AmountModifierBuilder : GenerationModifierBuilder<AmountModifierBuilder>
{
    /// <summary>References a live prevention definition.</summary>
    internal AmountModifierBuilder(GenerationTemplate template, GenerationNode node, ModifierTemplate modifier, int ordinal) : base(template, node, modifier, ordinal) { }

    /// <summary>Maximum absolute amount of incoming contribution to prevent.</summary>
    public GenerationParameterBuilder<AmountModifierBuilder> Amount => new(this, "amount");
}
