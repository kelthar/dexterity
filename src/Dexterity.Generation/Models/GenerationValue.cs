namespace Dexterity.Generation;

public class GenerationValue : IGenerationEntry
{
    /// <summary>Case-sensitive output key, unique across all values in the template.</summary>
    public string Id { get; set; } = "";
    /// <summary>Ascending sibling processing order; ties preserve authored list order.</summary>
    public int Order { get; set; }
    /// <summary>Positive relative share of the parent budget for numeric values; fixed values do not participate.</summary>
    public int Weight { get; set; } = 1;
    /// <summary>Maximum random addition to the weight, sampled inclusively from zero; zero disables variation.</summary>
    public int VaryWeightBy { get; set; }
    /// <summary>Inclusion percentage from 1 to 99; 0 and 100 always include an eligible value without a chance draw.</summary>
    public int ChanceToGet { get; set; }
    /// <summary>Request-context conditions that must all match for this value to be eligible.</summary>
    public List<GenerationCondition> Conditions { get; set; } = new();
    /// <summary>Output strategy: weighted budget, bounded random, or literal text; defaults to weighted.</summary>
    public GenerationValueKind Kind { get; set; }
    /// <summary>Required literal output for fixed values; ignored by numeric kinds.</summary>
    public string? Value { get; set; }
    /// <summary>Inclusive random minimum, honored even when its cost exceeds the allocated budget.</summary>
    public int MinValue { get; set; }
    /// <summary>Inclusive random maximum; the sampled amount is capped by affordability unless the minimum requires overspending.</summary>
    public int MaxValue { get; set; }
    /// <summary>Positive cost per numeric output unit; fixed values contribute no worth regardless of this setting.</summary>
    public int WorthPerValue { get; set; } = 1;
}
