namespace Dexterity;

public interface IWorldBuilder
{
    Result<World> Build(World world);
    Result Validate(ModifierData modifier);
}
