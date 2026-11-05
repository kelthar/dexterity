namespace Dexterity;

public sealed class RandomDecrease : ModifierType
{
    private readonly IRandomSource _random;
    public RandomDecrease(IRandomSource random) => _random = random;
    public override string Id => "randomdecrease";
    public override string Name => nameof(RandomDecrease);
    protected override double Amount(ModifierData modifier) => -_random.Range(modifier.Data["min"], modifier.Data["max"]);
}
