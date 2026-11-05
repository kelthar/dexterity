namespace Dexterity;

public interface IWorldPackageService
{
    Result<WorldPackage> Save(World world);
    Result Load(World world, WorldPackage package);
}
