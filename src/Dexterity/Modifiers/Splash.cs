namespace Dexterity;

public sealed class Splash : ModifierType
{
    public Splash(World world) { }
    public override string Id => "splash";
    public override string Name => nameof(Splash);
    public override int Order => 400;
    public override EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId)
    {
        var splash = new EffectResult { TargetId = context.TargetId, SourceId = result.SourceId, InstrumentId = result.InstrumentId };
        if (!context.Distance.HasValue || context.Distance > modifier.Data["range"])
            return splash;
        foreach (var group in result.Items.Where(x => x.EffectTypeId == modifier.EffectTypeId).GroupBy(x => x.Duration))
            Add(splash, modifier, context, holderId, group.Sum(x => x.Amount) * modifier.Data["percent"] / 100, group.Key);
        return splash;
    }
}
