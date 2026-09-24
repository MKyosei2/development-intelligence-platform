// Purpose: Registers enterprise fleet, tenancy, identity, and evidence-integrity infrastructure services.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Enterprise;
using Dip.Application.Features.Fleet;
using Dip.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Infrastructure;

public static class EnterpriseFleetDependencyInjection
{
    public static IServiceCollection AddEnterpriseFleetInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IEnterpriseFleetRepository, EnterpriseFleetRepository>();
        services.AddScoped<IEnterpriseEvidenceRepository, EnterpriseEvidenceRepository>();
        services.AddScoped<IEnterpriseIdentityRepository, EnterpriseIdentityRepository>();
        services.AddScoped<AgentFleetService>();
        services.AddScoped<EnterpriseTenantService>();
        services.AddScoped<IdentityResolutionService>();
        services.AddScoped<EvidenceIntegrityService>();
        services.AddScoped<DistributedIngestionService>();
        services.AddScoped<RetentionGovernanceService>();
        services.AddScoped<AgentPolicyDistributionService>();
        services.AddSingleton<AdaptiveQaRoutingService>();
        return services;
    }
}
