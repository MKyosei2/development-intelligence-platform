using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Artifacts;
using Dip.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Infrastructure;

public static class DifficultyTwoDependencyInjection
{
    public static IServiceCollection AddDifficultyTwoFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<ITrackedArtifactRepository, TrackedArtifactRepository>();
        services.AddScoped<TrackedArtifactService>();
        services.AddDifficultyThreeFoundation(configuration);
        return services;
    }
}
