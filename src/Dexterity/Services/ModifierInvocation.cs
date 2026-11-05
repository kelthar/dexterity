namespace Dexterity;

internal sealed class ModifierInvocation
{
    internal Component Holder { get; }
    internal ModifierData Data { get; }
    internal IModifierType Type { get; }
    internal bool Use { get; }
    internal int Index { get; }
    internal ModifierInvocation(Component holder, ModifierData data, IModifierType type, bool use, int index)
    { Holder = holder; Data = data; Type = type; Use = use; Index = index; }
}
