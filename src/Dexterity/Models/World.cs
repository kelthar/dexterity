namespace Dexterity;

public class World
{
    public List<StatType> Stats { get; set; } = new();
    public List<EffectType> EffectTypes { get; set; } = new();
    public List<ComponentType> ComponentTypes { get; set; } = new();
    public List<ComponentCategory> Categories { get; set; } = new();
    public List<EquipmentSlot> Slots { get; set; } = new();
    public List<IStatCalculator> Calculators { get; set; } = new();
    public List<IModifierType> Modifiers { get; set; } = new();
    public int? ChargesStatId { get; set; }
    public WorldSettings Settings { get; set; } = new();
    public IComponentRepository Repository { get; set; } = null!;
    public IWorldLogic Logic { get; set; } = null!;
    public IRandomSource Random { get; set; } = null!;
}
