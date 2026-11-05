namespace Dexterity.Generation;

public class DataBinding
{
    /// <summary>Destination component data key, unique among this template's data bindings.</summary>
    public string Key { get; set; } = "";
    /// <summary>Generated value key whose text is copied unchanged; missing output skips this binding.</summary>
    public string ValueKey { get; set; } = "";
}
