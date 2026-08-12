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
        services.AddScoped<Dip.Application.Features.Replay.GameReplayService>();
        services.AddScoped<Dip.Application.Features.Performance.PerformanceBudgetService>();
        services.AddScoped<Dip.Application.Features.Hardware.HardwareBacktestingService>();
        services.AddScoped<Dip.Application.Features.Repositories.MultiRepositoryGraphService>();
        services.AddScoped<Dip.Application.Features.ChangeRisk.ChangeRiskIntelligenceService>();
        services.AddScoped<Dip.Application.Features.Triage.RegressionTriageService>();
        services.AddScoped<Dip.Application.Features.Release.ReleaseReadinessService>();
        services.AddScoped<Dip.Application.Features.WorkItems.WorkItemService>();
        services.AddScoped<Dip.Application.Features.Release.ReleaseEvidenceService>();
        services.AddScoped<Dip.Application.Features.Dashboard.IntelligenceDashboardService>();
        services.AddScoped<Dip.Application.Features.DevelopmentGraph.DevelopmentGraphService>();
        services.AddScoped<Dip.Application.Features.DevelopmentGraph.ProjectControlTestImpactService>();
        services.AddScoped<Dip.Application.Features.ChangeImpact.ChangeImpactAnalysisService>();
        services.AddScoped<Dip.Application.Features.Milestones.MilestoneIntelligenceService>();
        services.AddScoped<Dip.Application.Features.FeatureControl.FeatureReadinessService>();
        services.AddScoped<Dip.Application.Features.Review.ReviewIntelligenceService>();
        services.AddScoped<Dip.Application.Features.Assets.AssetIntelligenceService>();
        services.AddScoped<Dip.Application.Features.Decisions.DecisionLogService>();
        services.AddScoped<Dip.Application.Features.Scenarios.WhatIfSimulationService>();
        services.AddScoped<Dip.Application.Features.Integrations.VersionControlSyncService>();
        services.AddScoped<Dip.Application.Features.Integrations.ExternalCiIntegrationService>();
        services.AddScoped<Dip.Application.Features.Dashboard.ProjectControlDashboardService>();
        services.AddScoped<Dip.Application.Features.Management.WorkManagementService>();
        services.AddScoped<Dip.Application.Features.Management.ManagementAnalyticsService>();
        services.AddScoped<Dip.Application.Features.Management.ManagementSpreadsheetService>();
        services.AddScoped<Dip.Application.Features.Dashboard.ManagementDashboardService>();
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
