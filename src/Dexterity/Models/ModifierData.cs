namespace Dexterity;

public class ModifierData
{
    public string TypeId { get; set; } = "";
    public int? EffectTypeId { get; set; }
    public List<string> IncludedTags { get; set; } = new();
    public List<string> ExcludedTags { get; set; } = new();
    public Phase Phase { get; set; }
    public Scope Scope { get; set; }
    public RelationKind RelationKind { get; set; }
    public double Duration { get; set; }
    public Dictionary<string, double> Data { get; set; } = new();
}
