namespace Dexterity.Generation;

/// <summary>Configures scaling of existing matching runtime contributions.</summary>
public sealed class MultiplierModifierBuilder : GenerationModifierBuilder<MultiplierModifierBuilder>
{
    /// <summary>References a live multiplier definition.</summary>
    internal MultiplierModifierBuilder(GenerationTemplate template, GenerationNode node, ModifierTemplate modifier, int ordinal) : base(template, node, modifier, ordinal) { }

    /// <summary>Scaling factor; 2 means two times, not two percent.</summary>
    public GenerationParameterBuilder<MultiplierModifierBuilder> Multiplier => new(this, "multiplier");
}
