namespace Dexterity;

public class EffectContext
{
    public Phase Phase { get; set; }
    public Trigger Trigger { get; set; }
    public int OwnerId { get; set; }
    public int? SourceId { get; set; }
    public int? TargetId { get; set; }
    public int? InstrumentId { get; set; }
    public RelationKind Relation { get; set; }
    public int? EffectTypeId { get; set; }
    public double? Distance { get; set; }
}
