namespace Dexterity.Generation;

public class DefaultGenerationRandom(int? seed = null) : IGenerationRandom
{
    private readonly Random _random = seed.HasValue ? new Random(seed.Value) : new Random();

    /// <inheritdoc />
    public int Next(int min, int max) => (int)_random.NextInt64(min, (long)max + 1);
}
