namespace Dexterity;

public sealed class Increase : ModifierType
{
    public override string Id => "increase";
    public override string Name => nameof(Increase);
    protected override double Amount(ModifierData modifier) => modifier.Data["value"];
}
