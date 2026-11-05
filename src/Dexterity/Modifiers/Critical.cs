namespace Dexterity;

public sealed class Critical : ModifierType
{
    private readonly IRandomSource _random;
    public Critical(IRandomSource random) => _random = random;
    public override string Id => "critical";
    public override string Name => nameof(Critical);
    public override int Order => 300;
    public override EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId) =>
        _random.Roll() < modifier.Data["chance"] ? Scale(result, modifier, context, holderId, modifier.Data["multiplier"]) : result;
}
