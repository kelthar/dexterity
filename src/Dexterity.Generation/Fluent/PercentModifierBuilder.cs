namespace Dexterity.Generation;

/// <summary>Configures percentage prevention or steal.</summary>
public sealed class PercentModifierBuilder : GenerationModifierBuilder<PercentModifierBuilder>
{
    /// <summary>References a live percentage modifier definition.</summary>
    internal PercentModifierBuilder(GenerationTemplate template, GenerationNode node, ModifierTemplate modifier, int ordinal) : base(template, node, modifier, ordinal) { }

    /// <summary>Runtime percentage from 0 to 100.</summary>
    public GenerationParameterBuilder<PercentModifierBuilder> Percent => new(this, "percent");
}
