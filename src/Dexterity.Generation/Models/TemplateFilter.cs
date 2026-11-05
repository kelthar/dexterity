namespace Dexterity.Generation;

public class TemplateFilter
{
    /// <summary>Optional world component type to match; null allows any type.</summary>
    public int? TypeId { get; set; }
    /// <summary>Optional exact world category to match; null allows any category.</summary>
    public int? CategoryId { get; set; }
    /// <summary>Discovery tags that must all be present, compared case-insensitively; an empty list imposes no tag filter.</summary>
    public List<string> Tags { get; set; } = new();
}
