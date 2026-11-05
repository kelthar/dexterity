namespace Dexterity.Generation;

public class GenerationCatalog
{
    /// <summary>Available templates, each with a non-empty, ordinally unique ID.</summary>
    public List<GenerationTemplate> Templates { get; set; } = new();
}
