namespace Dexterity.Generation;

public class GenerationRequest
{
    /// <summary>Case-sensitive catalog ID of the template to generate.</summary>
    public string TemplateId { get; set; } = "";
    /// <summary>Non-negative budget distributed through the template; random minima may cause overspending.</summary>
    public int Worth { get; set; }
    /// <summary>Optional seed for a per-request random source; null uses the service's injected source.</summary>
    public int? Seed { get; set; }
    /// <summary>Condition inputs, matched by ordinal, case-sensitive keys independently of this dictionary's comparer.</summary>
    public Dictionary<string, string> Context { get; set; } = new();
}
