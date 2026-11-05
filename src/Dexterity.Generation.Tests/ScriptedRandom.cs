namespace Dexterity.Generation.Tests;

internal sealed class ScriptedRandom : IGenerationRandom
{
    public Queue<int> Values { get; } = new();
    public List<(int Min, int Max)> Calls { get; } = new();
    public int Next(int min, int max)
    {
        Calls.Add((min, max));
        return Values.Count > 0 ? Values.Dequeue() : min;
    }
}
