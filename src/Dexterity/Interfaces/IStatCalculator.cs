namespace Dexterity;

public interface IStatCalculator
{
    int StatTypeId { get; }
    int ComponentTypeId { get; }
    int Order { get; }
    Result<double> Calculate(int componentId, World world);
}
