using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dexterity.Generation;

public static class ServiceExtensions
{
    /// <summary>Registers the default singleton generation random source and transient generation service unless registrations already exist.</summary>
    public static IServiceCollection Register(this IServiceCollection services)
    {
        services.TryAddSingleton<IGenerationRandom>(_ => new DefaultGenerationRandom());
        services.TryAddTransient<IGenerationService, GenerationService>();
        return services;
    }
}
