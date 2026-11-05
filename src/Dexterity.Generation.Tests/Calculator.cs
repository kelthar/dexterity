namespace Dexterity.Generation.Tests;

internal sealed class Calculator : IStatCalculator
{
    public int StatTypeId { get; set; } = Game.HP;
    public int ComponentTypeId { get; set; } = Game.Character;
    public int Order { get; set; }
    public Func<int, World, Result<double>> Run { get; set; } = (_, _) => Result.Ok(50d);
    public List<int> Calls { get; } = new();
    public Result<double> Calculate(int componentId, World world)
    {
        Calls.Add(componentId);
        return Run(componentId, world);
    }
}
