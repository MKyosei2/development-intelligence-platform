using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Security;
using Dip.Application.Features.Evidence;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Policies;
using Dip.Infrastructure.Integrations.GitHub;
using Dip.Infrastructure.Persistence.Repositories;
using Dip.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Infrastructure;

public static class DifficultyOneDependencyInjection
{
    public static IServiceCollection AddDifficultyOneFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IEvidenceV2Repository, EvidenceV2Repository>();
        services.AddScoped<IIntegrationConnectionRepository, IntegrationConnectionRepository>();
        services.AddScoped<IProjectSourceBindingRepository, ProjectSourceBindingRepository>();
        services.AddScoped<IPolicyProfileRepository, PolicyProfileRepository>();

        services.AddSingleton<IIntegrationCredentialStore, LocalIntegrationCredentialStore>();

        services.AddScoped<IIntegrationConnector, GitHubConnector>();
        services.AddScoped<IntegrationConnectorRegistry>();

        services.AddScoped<PolicyResolver>();
        services.AddScoped<PolicyProfileService>();
        services.AddScoped<EvidenceIngestionService>();
        services.AddScoped<IntegrationConnectionService>();

        return services;
    }
}
