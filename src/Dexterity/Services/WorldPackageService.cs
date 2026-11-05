namespace Dexterity;

public class WorldPackageService : IWorldPackageService
{
    public Result<WorldPackage> Save(World world)
    {
        if (world?.Repository == null)
            return EffectRules.Failure<WorldPackage>(Result.Fail("A configured world and repository are required.", 400));
        try
        {
            return world.Repository.Save();
        }
        catch (Exception exception)
        {
            return EffectRules.Failure<WorldPackage>(EffectRules.Exception(exception, "World save failed."));
        }
    }

    public Result Load(World world, WorldPackage package)
    {
        if (world?.Repository == null || package == null)
            return Result.Fail("A configured world, repository and package are required.", 400);
        try
        {
            return world.Repository.Load(package);
        }
        catch (Exception exception)
        {
            return EffectRules.Exception(exception, "World load failed.");
        }
    }
}
