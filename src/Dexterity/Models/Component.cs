namespace Dexterity;

public class Component
{
    public int Id { get; set; } = -1;
    public string Name { get; set; } = "";
    public ComponentState State { get; set; } = ComponentState.Detached;
    public int TypeId { get; set; }
    public int CategoryId { get; set; }
    public Dictionary<int, Stat> Stats { get; set; } = new();
    public Dictionary<string, string> MetaData { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<ModifierData> ModifierData { get; set; } = new();
    public List<EffectResult> Effects { get; set; } = new();
}
