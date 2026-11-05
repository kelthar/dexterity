namespace Dexterity.Generation;

/// <summary>Configures bounds sampled by a random increase/decrease at runtime.</summary>
public sealed class RangeModifierBuilder : GenerationModifierBuilder<RangeModifierBuilder>
{
    /// <summary>References a live random modifier definition.</summary>
    internal RangeModifierBuilder(GenerationTemplate template, GenerationNode node, ModifierTemplate modifier, int ordinal) : base(template, node, modifier, ordinal) { }

    /// <summary>Inclusive minimum used by the later runtime sample.</summary>
    public GenerationParameterBuilder<RangeModifierBuilder> Min => new(this, "min");
    /// <summary>Inclusive maximum used by the later runtime sample.</summary>
    public GenerationParameterBuilder<RangeModifierBuilder> Max => new(this, "max");
}
