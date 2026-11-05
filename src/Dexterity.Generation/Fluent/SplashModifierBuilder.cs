namespace Dexterity.Generation;

/// <summary>Configures runtime splash distance and percentage.</summary>
public sealed class SplashModifierBuilder : GenerationModifierBuilder<SplashModifierBuilder>
{
    /// <summary>References a live splash definition.</summary>
    internal SplashModifierBuilder(GenerationTemplate template, GenerationNode node, ModifierTemplate modifier, int ordinal) : base(template, node, modifier, ordinal) { }

    /// <summary>Maximum inclusive splash distance in the world's distance units.</summary>
    public GenerationParameterBuilder<SplashModifierBuilder> Range => new(this, "range");
    /// <summary>Percentage of the matching contribution sent to secondary targets.</summary>
    public GenerationParameterBuilder<SplashModifierBuilder> Percent => new(this, "percent");
}
