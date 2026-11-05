namespace Dexterity;

// Immutable contributions; countdown replaces stored items rather than changing reports.
public sealed record EffectResultItem
{
    public int EffectTypeId { get; init; }
    public double Amount { get; init; }
    public double Duration { get; init; }
    public int SourceId { get; init; }
    public int InstrumentId { get; init; }
}
