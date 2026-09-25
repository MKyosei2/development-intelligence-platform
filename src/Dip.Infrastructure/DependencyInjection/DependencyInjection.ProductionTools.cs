// Purpose: Implements P ro du ct io nT oo ls De pe nd en cy In je ct io n infrastructure behavior for d ep en de nc yi nj ec ti on.
using Dip.Application.Features.ProductionTools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Infrastructure;

public static class ProductionToolsDependencyInjection
{
    public static IServiceCollection AddProductionToolsFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<ProductionToolEventService>();
        services.AddScoped<ProductionEventQueryService>();
        services.AddScoped<ProductionMediaService>();
        services.AddQualityAutomationFoundation(configuration);
        return services;
    }
}
