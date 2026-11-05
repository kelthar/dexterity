namespace Dexterity.Generation;

public class GenerationGroup : GenerationNode, IGenerationEntry
{
    /// <summary>Case-sensitive group ID, unique among siblings; groups do not produce output values themselves.</summary>
    public string Id { get; set; } = "";
    /// <summary>Ascending sibling processing order; ties preserve authored list order.</summary>
    public int Order { get; set; }
    /// <summary>Relative share of the parent budget for descendants; must be positive when any descendant is numeric.</summary>
    public int Weight { get; set; } = 1;
    /// <summary>Maximum random addition to the weight, sampled inclusively from zero for budget-participating groups.</summary>
    public int VaryWeightBy { get; set; }
    /// <summary>Inclusion percentage for the entire branch; 0 and 100 always include an eligible group without a chance draw.</summary>
    public int ChanceToGet { get; set; }
    /// <summary>Request-context conditions that must all match; a mismatch skips the entire branch.</summary>
    public List<GenerationCondition> Conditions { get; set; } = new();
}
