namespace Dexterity;

public class ComponentCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Equipable { get; set; }
    public Usable Usable { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> MetaData { get; set; } = new();
    public int? ParentCategoryId { get; set; }
}
