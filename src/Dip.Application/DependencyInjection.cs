using Dip.Application.Features.Collectors;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Projects;
using Dip.Application.Features.Quality;
using Dip.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProjectService>();
        services.AddScoped<CollectorReportService>();
        services.AddScoped<DashboardQueryService>();
        services.AddScoped<OutboxWriter>();
        services.AddSingleton<CollectorReportValidator>();
        services.AddSingleton<QualityGateEvaluator>();
        return services;
    }
}
