namespace Dexterity;

public class EquipmentSlot
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<int>? Categories { get; set; } = new();
}
