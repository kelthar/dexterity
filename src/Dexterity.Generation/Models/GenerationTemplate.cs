namespace Dexterity.Generation;

public class GenerationTemplate : GenerationNode
{
    /// <summary>Unique, case-sensitive catalog ID used to select this template.</summary>
    public string Id { get; set; } = "";
    /// <summary>Name assigned to the generated component.</summary>
    public string Name { get; set; } = "";
    /// <summary>World component type to create; determines which stats are initialized.</summary>
    public int TypeId { get; set; }
    /// <summary>World category assigned to the generated component.</summary>
    public int CategoryId { get; set; }
    /// <summary>Discovery tags matched case-insensitively by template filters.</summary>
    public List<string> Tags { get; set; } = new();
    /// <summary>Instance tags copied to the generated component.</summary>
    public List<string> ComponentTags { get; set; } = new();
    /// <summary>Request-context conditions that must all match before generation can proceed.</summary>
    public List<GenerationCondition> Conditions { get; set; } = new();
    /// <summary>Mappings from generated values to initial stat maxima and optional current values.</summary>
    public List<StatBinding> StatBindings { get; set; } = new();
    /// <summary>Mappings from generated text values to component data entries.</summary>
    public List<DataBinding> DataBindings { get; set; } = new();
    /// <summary>Modifier definitions copied and populated from generated values.</summary>
    public List<ModifierTemplate> Modifiers { get; set; } = new();
}
