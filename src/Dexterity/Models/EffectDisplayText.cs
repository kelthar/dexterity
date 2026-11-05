namespace Dexterity;

/// <summary>Optional English display wording, keyed by effect type ID in DisplayService.</summary>
public sealed class EffectDisplayText
{
    public string Quantity { get; init; } = "";
    /// <summary>Phrase containing {amount}, for example "restore {amount} HP".</summary>
    public string Increase { get; init; } = "";
    /// <summary>Phrase containing {amount}, for example "{amount} dmg".</summary>
    public string Decrease { get; init; } = "";
}
