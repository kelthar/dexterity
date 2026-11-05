namespace Dexterity.Generation;

public class GeneratedComponent
{
    /// <summary>The live, attached component with recalculated stats and final initial currents.</summary>
    public Component Component { get; set; } = null!;
    /// <summary>Generated source values and worth accounting.</summary>
    public GenerationResult Generation { get; set; } = new();
}
