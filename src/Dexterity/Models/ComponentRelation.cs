namespace Dexterity;

public class ComponentRelation
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public int TargetId { get; set; }
    public List<int> SlotIds { get; set; } = new();
    public RelationType Type { get; set; }
    public Dictionary<string, string> MetaData { get; set; } = new();
}
