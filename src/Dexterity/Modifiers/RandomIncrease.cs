namespace Dexterity;

public sealed class RandomIncrease : ModifierType
{
    private readonly IRandomSource _random;
    public RandomIncrease(IRandomSource random) => _random = random;
    public override string Id => "randomincrease";
    public override string Name => nameof(RandomIncrease);
    protected override double Amount(ModifierData modifier) => _random.Range(modifier.Data["min"], modifier.Data["max"]);
}
