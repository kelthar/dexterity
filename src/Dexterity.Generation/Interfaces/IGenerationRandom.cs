namespace Dexterity.Generation;

public interface IGenerationRandom
{
    /// <summary>Returns an integer between the inclusive minimum and maximum bounds.</summary>
    int Next(int min, int max);
}
