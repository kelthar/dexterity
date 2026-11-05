namespace Dexterity.Generation;

/// <summary>Configures the Value input of an increase or decrease modifier.</summary>
public sealed class ValueModifierBuilder : GenerationModifierBuilder<ValueModifierBuilder>
{
    /// <summary>References a live increase/decrease definition.</summary>
    internal ValueModifierBuilder(GenerationTemplate template, GenerationNode node, ModifierTemplate modifier, int ordinal) : base(template, node, modifier, ordinal) { }

    /// <summary>Numeric amount supplied to the runtime modifier.</summary>
    public GenerationParameterBuilder<ValueModifierBuilder> Value => new(this, "value");
}
