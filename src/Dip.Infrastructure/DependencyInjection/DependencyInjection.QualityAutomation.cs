// Purpose: Registers quality automation, visual QA, asset QA, production verification, and enterprise fleet infrastructure services.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Management;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.QualityAutomation.AssetQa;
using Dip.Application.Features.QualityAutomation.VisualQa;
using Dip.Infrastructure.Persistence.Repositories;
using Dip.Infrastructure.QualityAutomation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Infrastructure;

public static class QualityAutomationDependencyInjection
{
    public static IServiceCollection AddQualityAutomationFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IQualityAutomationRepository, QualityAutomationRepository>();
        services.AddScoped<IClaimEvidenceAssessmentRepository, ClaimEvidenceAssessmentRepository>();
        services.AddSingleton<IVisualComparator, PortablePngVisualComparator>();
        services.AddSingleton<AssetQaRuleEngine>();
        services.AddScoped<ProductionGraphProjectionService>();
        services.AddScoped<VisualQaService>();
        services.AddScoped<AssetQaService>();
        services.AddScoped<ClaimEvidenceAssessmentService>();
        services.AddScoped<ProducerIntelligenceService>();
        services.AddSingleton<ICrossCuttingQaPack, ProductionVerificationQaPack>();
        services.AddEnterpriseFleetInfrastructure(configuration);
        return services;
    }
}
