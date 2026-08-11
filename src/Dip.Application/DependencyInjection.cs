using Dip.Application.Features.Collectors;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.Projects;
using Dip.Application.Features.Quality;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.Hardware;
using Dip.Application.Features.P2Intelligence;
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
        services.AddScoped<Dip.Application.Features.Intelligence.P2OrchestrationService>();
        services.AddScoped<Dip.Application.Features.Intelligence.AdaptiveQaPlanner>();
        services.AddScoped<Dip.Application.Features.Intelligence.CommunityHardwareService>();
        services.AddScoped<Dip.Application.Features.Intelligence.RootCauseIntelligenceService>();
        services.AddScoped<Dip.Application.Features.Intelligence.ForecastAndDecisionService>();
        services.AddScoped<Dip.Application.Features.Intelligence.P2AnalysisService>();
        services.AddScoped<HardwareCommunityIntelligenceService>();
        services.AddScoped<Dip.Application.Features.P2Intelligence.AdaptiveQaPlanner>();
        services.AddScoped<P2TechnicalIntelligenceService>();
        services.AddSingleton<GameRegressionEvaluator>();
        services.AddScoped<EvidenceGroundedRootCauseService>();
        services.AddScoped<ProjectDecisionIntelligenceService>();
        services.AddScoped<OutboxWriter>();
        services.AddScoped<Dip.Application.Features.Artifacts.ArtifactService>();
        services.AddScoped<Dip.Application.Features.Jobs.DurableJobService>();
        services.AddScoped<Dip.Application.Features.Jobs.P2JobEnqueueService>();
        services.AddScoped<Dip.Application.Features.Incidents.IncidentAggregationService>();
        services.AddScoped<Dip.Application.Features.Benchmarks.BenchmarkOrchestrationService>();
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
