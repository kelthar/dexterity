namespace Dexterity;

public class StatCalculator : IStatCalculator
{
    public int StatTypeId { get; set; }
    public int ComponentTypeId { get; set; }
    public int Order { get; set; }
    public virtual Result<double> Calculate(int componentId, World world)
    {
        if (world == null || world.Repository == null)
            return Result.Fail("World and repository are required.", 400);
        if (world.Repository.Get(componentId).Failed(out var lookup))
            return lookup.As<double>();
        var component = lookup.Data!;
        if (component.TypeId != ComponentTypeId || !component.Stats.TryGetValue(StatTypeId, out var stat))
            return Result.Fail("Calculator does not support this component or stat.", 400);
        return Result.Ok(stat.Max);
    }
}
