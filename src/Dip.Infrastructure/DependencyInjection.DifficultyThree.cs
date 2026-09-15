using Dip.Application.Features.ProductionTools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Infrastructure;

public static class DifficultyThreeDependencyInjection
{
    public static IServiceCollection AddDifficultyThreeFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<ProductionToolEventService>();
        services.AddScoped<ProductionEventQueryService>();
        services.AddScoped<ProductionMediaService>();
        services.AddDifficultyFourFoundation(configuration);
        return services;
    }
}
