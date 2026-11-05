namespace Dexterity;

public interface IModifierType
{
    string Id { get; }
    string Name { get; }
    int Order { get; }
    // Context.InstrumentId identifies the action; holderId identifies this modifier's component.
    EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId);
}
