namespace Dexterity;

public sealed class PreventFlat : ModifierType
{
    public override string Id => "preventflat";
    public override string Name => nameof(PreventFlat);
    public override int Order => 600;
    public override EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId)
    {
        if (!modifier.EffectTypeId.HasValue)
            return result;
        var total = result.Sum(modifier.EffectTypeId.Value);
        Add(result, modifier, context, holderId, -Math.Sign(total) * Math.Min(Math.Abs(total), modifier.Data["amount"]));
        return result;
    }
}
