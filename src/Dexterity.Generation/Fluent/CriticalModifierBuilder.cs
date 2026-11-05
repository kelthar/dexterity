namespace Dexterity.Generation;

/// <summary>Configures critical probability and scaling as independent generated inputs.</summary>
public sealed class CriticalModifierBuilder : GenerationModifierBuilder<CriticalModifierBuilder>
{
    /// <summary>References a live critical definition.</summary>
    internal CriticalModifierBuilder(GenerationTemplate template, GenerationNode node, ModifierTemplate modifier, int ordinal) : base(template, node, modifier, ordinal) { }

    /// <summary>Runtime critical chance in percent from 0 to 100.</summary>
    public GenerationParameterBuilder<CriticalModifierBuilder> Chance => new(this, "chance");
    /// <summary>Scaling factor applied when the runtime critical roll succeeds.</summary>
    public GenerationParameterBuilder<CriticalModifierBuilder> Multiplier => new(this, "multiplier");
}
