namespace Dexterity;

public interface IRandomSource
{
    double Roll();
    double Range(double min, double max);
}
