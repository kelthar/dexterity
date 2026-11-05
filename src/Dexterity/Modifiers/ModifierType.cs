namespace Dexterity;

public abstract class ModifierType : IModifierType
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public virtual int Order => 100;
    protected virtual double Amount(ModifierData modifier) => 0;

    public virtual EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId)
    {
        if (modifier.EffectTypeId.HasValue)
            Add(result, modifier, context, holderId, Amount(modifier), modifier.Duration);
        return result;
    }

    protected static void Add(EffectResult result, ModifierData modifier, EffectContext context, int holderId, double amount, double duration = 0)
    {
        if (!modifier.EffectTypeId.HasValue || amount == 0)
            return;
        result.Items.Add(new EffectResultItem
        {
            EffectTypeId = modifier.EffectTypeId.Value, Amount = amount, Duration = duration,
            SourceId = context.OwnerId, InstrumentId = holderId
        });
    }
    protected static EffectResult Scale(EffectResult result, ModifierData modifier, EffectContext context, int holderId, double factor)
    {
        // Keep each timed contribution's duration; Incoming changes only immediate items.
        var groups = result.Items.Where(x => x.EffectTypeId == modifier.EffectTypeId && (context.Phase != Phase.Incoming || x.Duration == 0))
            .GroupBy(x => x.Duration).Select(x => (Duration: x.Key, Amount: x.Sum(i => i.Amount))).ToArray();
        foreach (var group in groups)
            Add(result, modifier, context, holderId, group.Amount * (factor - 1), group.Duration);
        return result;
    }
}
