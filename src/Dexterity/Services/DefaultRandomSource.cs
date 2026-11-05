namespace Dexterity;

public class DefaultRandomSource : IRandomSource
{
    private readonly Random _random;
    private readonly object _gate = new();
    public DefaultRandomSource(int? seed = null) => _random = seed.HasValue ? new Random(seed.Value) : new Random();
    public double Roll()
    {
        lock (_gate)
            return _random.NextDouble() * 100;
    }
    public double Range(double min, double max)
    {
        // Discrete sampling includes both endpoints, including non-integer endpoints.
        lock (_gate)
            return min + (max - min) * (_random.Next(0, int.MaxValue) / (double)(int.MaxValue - 1));
    }
}
