namespace Dexterity;

public sealed class Multiplier : ModifierType
{
    public override string Id => "multiplier";
    public override string Name => nameof(Multiplier);
    public override int Order => 200;
    public override EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId) =>
        Scale(result, modifier, context, holderId, modifier.Data["multiplier"]);
}
