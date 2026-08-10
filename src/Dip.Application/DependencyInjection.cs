using Dip.Application.Features.Collectors;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.Projects;
using Dip.Application.Features.Quality;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.Hardware;
using Dip.Application.Features.Intelligence;
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
        services.AddScoped<ProjectClaimService>();
        services.AddScoped<ProjectTruthService>();
        services.AddScoped<QaOrchestrationService>();
        services.AddScoped<HardwareOrchestrationService>();
        services.AddScoped<P2OrchestrationService>();
        services.AddScoped<CommunityHardwareService>();
        services.AddScoped<AdaptiveQaPlanner>();
        services.AddScoped<RootCauseIntelligenceService>();
        services.AddScoped<ForecastAndDecisionService>();
        services.AddScoped<P2AnalysisService>();
        services.AddScoped<OutboxWriter>();
        services.AddSingleton<CollectorReportValidator>();
        services.AddSingleton<QualityRuleEvaluator>();
        services.AddSingleton<QualityGateEvaluator>();
        services.AddSingleton<ClaimVerificationEvaluator>();
        services.AddSingleton<EvidenceConfidenceEvaluator>();
        services.AddSingleton<ProgressEvaluator>();
        services.AddSingleton<ProjectBlockerEvaluator>();
        services.AddSingleton<ProjectRiskEvaluator>();
        services.AddSingleton<ProjectTruthEvaluator>();
        services.AddSingleton<PmEvidenceTranslator>();
        return services;
    }
}
