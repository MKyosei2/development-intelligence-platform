// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Artifacts;
using Dip.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Infrastructure;

public static class ArtifactTrackingDependencyInjection
{
    public static IServiceCollection AddArtifactTrackingFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<ITrackedArtifactRepository, TrackedArtifactRepository>();
        services.AddScoped<TrackedArtifactService>();
        services.AddProductionToolsFoundation(configuration);
        return services;
    }
}
