// Purpose: Implements D if fi cu lt yF iv eD ep en de nc yI nj ec ti on infrastructure integration for d ep en de nc yi nj ec ti on.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Enterprise;
using Dip.Application.Features.Fleet;
using Dip.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Dip.Infrastructure;
public static class DifficultyFiveDependencyInjection
{
    public static IServiceCollection AddDifficultyFiveFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(configuration);
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
