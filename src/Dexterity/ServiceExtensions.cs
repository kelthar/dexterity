using Microsoft.Extensions.DependencyInjection;

namespace Dexterity;

public static class ServiceExtensions
{
    public static IServiceCollection Register(this IServiceCollection services)
    {
        services.AddSingleton<IComponentRepository>(provider => provider.GetRequiredService<World>().Repository);
        services.AddTransient<IEffectService, DefaultEffectService>();
        services.AddTransient<IRelationService, RelationService>();
        services.AddTransient<IWorldBuilder, WorldBuilder>();
        services.AddTransient<ITagService, TagService>();
        services.AddTransient<IWorldPackageService, WorldPackageService>();
        services.AddTransient<IDisplayService, DisplayService>();
        services.AddSingleton<IWorldLogic>(provider => provider.GetRequiredService<World>().Logic);
        services.AddSingleton<IRandomSource>(provider => provider.GetRequiredService<World>().Random);
        return services;
    }
}
