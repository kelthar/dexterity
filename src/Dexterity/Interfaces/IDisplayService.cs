namespace Dexterity;

public interface IDisplayService
{
    Result<ModifierDisplay> Describe(IModifierType modifier, ModifierData data);
    Result<ModifierDisplay> Describe(IEnumerable<ModifierData> modifiers);
}
