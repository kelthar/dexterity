namespace Dexterity;

public sealed class PreventPercent : ModifierType
{
    public override string Id => "preventpercent";
    public override string Name => nameof(PreventPercent);
    public override int Order => 700;
    public override EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId)
    {
        if (modifier.EffectTypeId.HasValue)
            Add(result, modifier, context, holderId, -result.Sum(modifier.EffectTypeId.Value) * modifier.Data["percent"] / 100);
        return result;
    }
}
