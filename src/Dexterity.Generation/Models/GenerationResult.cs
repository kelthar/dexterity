namespace Dexterity.Generation;

public class GenerationResult
{
    /// <summary>ID of the template used for this generation.</summary>
    public string TemplateId { get; set; } = "";
    /// <summary>Original budget supplied by the request.</summary>
    public int RequestedWorth { get; set; }
    /// <summary>Sum of emitted numeric amounts multiplied by their unit costs; fixed values contribute zero.</summary>
    public int TotalWorth { get; set; }
    /// <summary>Positive difference between requested and total worth, or zero when the budget was met or exceeded.</summary>
    public int UnusedWorth { get; set; }
    /// <summary>Positive difference between total and requested worth, or zero when generation stayed within budget.</summary>
    public int Overspend { get; set; }
    /// <summary>Emitted text by case-sensitive value ID; skipped values are absent and numeric output uses invariant formatting.</summary>
    public Dictionary<string, string> Values { get; set; } = new();
}
