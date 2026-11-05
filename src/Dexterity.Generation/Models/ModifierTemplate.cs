namespace Dexterity.Generation;

public class ModifierTemplate
{
    /// <summary>Base modifier definition, deep-cloned before generated parameters are applied.</summary>
    public ModifierData Base { get; set; } = new();
    /// <summary>Maps modifier parameter names to generated numeric value keys; any missing output omits the whole modifier.</summary>
    public Dictionary<string, string> DataKeys { get; set; } = new();
    /// <summary>Optional generated numeric key overriding duration; missing output omits the modifier, while null preserves the base duration.</summary>
    public string? DurationKey { get; set; }
}
