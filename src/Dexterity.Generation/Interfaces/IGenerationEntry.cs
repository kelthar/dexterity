namespace Dexterity.Generation;

public interface IGenerationEntry
{
    /// <summary>Case-sensitive sibling ID; value entries also require globally unique output IDs within the template.</summary>
    string Id { get; set; }
    /// <summary>Ascending sibling processing order, with authored list order breaking ties.</summary>
    int Order { get; set; }
    /// <summary>Relative parent-budget share; must be positive when the entry participates in numeric generation.</summary>
    int Weight { get; set; }
    /// <summary>Maximum inclusive random addition to a participating entry's weight; zero disables variation.</summary>
    int VaryWeightBy { get; set; }
    /// <summary>Inclusion percentage; 0 and 100 both bypass the chance draw and include an eligible entry.</summary>
    int ChanceToGet { get; set; }
    /// <summary>Request-context conditions that must all match before the entry's chance gate is evaluated.</summary>
    List<GenerationCondition> Conditions { get; set; }
}
