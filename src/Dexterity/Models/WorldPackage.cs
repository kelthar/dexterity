namespace Dexterity;

public class WorldPackage
{
    public int Version { get; set; } = 1;
    public List<Component> Components { get; set; } = new();
    public List<ComponentRelation> Relations { get; set; } = new();
    public long NextComponentId { get; set; } = 1;
}
