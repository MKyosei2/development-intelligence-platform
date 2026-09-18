// Purpose: Provides the infrastructure capability represented by E vi de nc eI nt eg ra ti on De pe nd en cy In je ct io n.
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

public static class EvidenceIntegrationDependencyInjection
{
    public static IServiceCollection AddEvidenceIntegrationFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IEvidenceV2Repository, EvidenceV2Repository>();
        services.AddScoped<IIntegrationConnectionRepository, IntegrationConnectionRepository>();
        services.AddScoped<IProjectSourceBindingRepository, ProjectSourceBindingRepository>();
        services.AddScoped<IPolicyProfileRepository, PolicyProfileRepository>();

        services.AddSingleton<LocalIntegrationCredentialStore>();
        services.AddSingleton<IIntegrationCredentialStore, EnterpriseIntegrationCredentialStore>();

        services.AddScoped<IIntegrationConnector, GitHubConnector>();
        services.AddScoped<IntegrationConnectorRegistry>();

        services.AddScoped<PolicyResolver>();
        services.AddScoped<PolicyProfileService>();
        services.AddScoped<EvidenceIngestionService>();
        services.AddScoped<IntegrationConnectionService>();

        // Phase 2 builds on the Phase 1 contracts without replacing them.
        services.AddArtifactTrackingFoundation(configuration);
        return services;
    }
}
