namespace Dexterity;

public sealed class Steal : ModifierType
{
    public override string Id => "steal";
    public override string Name => nameof(Steal);
    public override int Order => 800;
    public override EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId)
    {
        // The service supplies the post-mitigation basis, capped by the target's Current.
        var steal = new EffectResult { TargetId = context.SourceId, SourceId = result.SourceId, InstrumentId = result.InstrumentId };
        if (modifier.EffectTypeId.HasValue)
            Add(steal, modifier, context, holderId, Math.Max(0, -result.Sum(modifier.EffectTypeId.Value)) * modifier.Data["percent"] / 100);
        return steal;
    }
}
