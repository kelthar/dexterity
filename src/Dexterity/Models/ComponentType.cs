namespace Dexterity;

public class ComponentType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public bool MultipleParentComponents { get; set; }
    public List<int> StatIds { get; set; } = new();
}
