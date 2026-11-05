namespace Dexterity;

public sealed class Decrease : ModifierType
{
    public override string Id => "decrease";
    public override string Name => nameof(Decrease);
    protected override double Amount(ModifierData modifier) => -modifier.Data["value"];
}
