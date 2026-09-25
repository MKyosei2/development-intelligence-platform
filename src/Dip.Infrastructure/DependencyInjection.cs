// Purpose: Implements Dependency Injection infrastructure behavior for this repository area.
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Hardware;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.Hardware;
using Dip.Infrastructure.Intelligence;
using Dip.Infrastructure.Messaging;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Persistence.Repositories;
using Dip.Infrastructure.P2Intelligence;
using Dip.Infrastructure.QualityAutomation;
using Dip.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnection = configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(postgresConnection))
            throw new InvalidOperationException("ConnectionStrings:PostgreSql is missing.");

        services.AddDbContext<DipDbContext>(options => options.UseNpgsql(postgresConnection));
        services.AddScoped<LegacyMigrationAdopter>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectMembershipRepository, ProjectMembershipRepository>();
        services.AddScoped<Dip.Application.Features.Authorization.IProjectResourceProjectResolver, Dip.Infrastructure.Persistence.ProjectResourceProjectResolver>();
        services.AddScoped<Dip.Application.Features.Authorization.IProjectMutationCoordinator, Dip.Infrastructure.Persistence.ProjectMutationCoordinator>();
        services.AddScoped<IPipelineRunRepository, PipelineRunRepository>();
        services.AddScoped<IMetricRepository, MetricRepository>();
        services.AddScoped<IQualityIssueRepository, QualityIssueRepository>();
        services.AddScoped<IEvidenceRepository, EvidenceRepository>();
        services.AddScoped<IProjectClaimRepository, ProjectClaimRepository>();
        services.AddScoped<IClaimEvidenceLinkRepository, ClaimEvidenceLinkRepository>();
        services.AddScoped<IProjectTruthRepository, ProjectTruthRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IQaRunRepository, QaRunRepository>();
        services.AddScoped<IQaFindingRepository, QaFindingRepository>();
        services.AddScoped<IQaGovernanceRepository, QaGovernanceRepository>();
        services.AddScoped<Dip.Application.Features.QualityGovernance.IQaGovernanceExecutionCoordinator, Dip.Infrastructure.Persistence.QaGovernanceExecutionCoordinator>();
        services.AddScoped<Dip.Application.Features.QualityGovernance.IRepositoryOwnershipResolver, CodeOwnersOwnershipResolver>();
        services.AddScoped<IReproductionAttemptRepository, ReproductionAttemptRepository>();
        services.AddScoped<IHardwareProfileRepository, HardwareProfileRepository>();
        services.AddScoped<IHardwareCalibrationRepository, HardwareCalibrationRepository>();
        services.AddScoped<IPerformanceSimulationRepository, PerformanceSimulationRepository>();
        services.AddScoped<ICommunityHardwareStore, CommunityHardwareStore>();
        services.AddScoped<IIntelligenceRunStore, IntelligenceRunStore>();
        services.AddScoped<IProjectForecastStore, ProjectForecastStore>();
        services.AddSingleton<ProcessExecutor>();
        services.AddSingleton<UniversalDiagnosticNormalizer>();
        services.AddSingleton<IUniversalSemanticAnalyzer, UniversalSemanticAnalyzer>();
        services.AddSingleton<IQaCapabilityPlanner, QaCapabilityPlanner>();
        services.AddSingleton<IIdeDiagnosticNormalizer, IdeDiagnosticNormalizer>();
        services.AddSingleton<ICrossCuttingQaPack, SystemIntegrationQaPack>();
        services.AddSingleton<ICrossCuttingQaPack, GameProjectQaPack>();
        services.AddSingleton<DeepLanguagePackQaAdapter>();
        services.AddSingleton<PythonQaEngine>();
        services.AddSingleton<CSharpQaEngine>();
        services.AddSingleton<CppQaEngine>();
        services.AddSingleton<GenericCommandQaAdapter>();
        services.AddSingleton<UnityProjectQaAdapter>();
        services.AddSingleton<ManifestQaAdapter>();
        services.AddSingleton<IQaAdapter>(sp => sp.GetRequiredService<ManifestQaAdapter>());
        services.AddSingleton<IQaAdapter>(sp => sp.GetRequiredService<UnityProjectQaAdapter>());
        services.AddSingleton<IQaAdapter>(sp => sp.GetRequiredService<PythonQaEngine>());
        services.AddSingleton<IQaAdapter>(sp => sp.GetRequiredService<CSharpQaEngine>());
        services.AddSingleton<IQaAdapter>(sp => sp.GetRequiredService<CppQaEngine>());
        services.AddSingleton<IQaAdapter>(sp => sp.GetRequiredService<DeepLanguagePackQaAdapter>());
        services.AddSingleton<IQaAdapter>(sp => sp.GetRequiredService<GenericCommandQaAdapter>());
        services.AddSingleton<IQaAdapterRegistry, QaAdapterRegistry>();
        services.AddSingleton<IRepositoryTechnologyDetector, RepositoryTechnologyDetector>();
        services.AddScoped<Dip.Application.Features.QualityGovernance.IQaBugMinimizer, QaBugMinimizer>();
        services.AddSingleton<ReproductionCoordinator>();
        services.AddSingleton<EnvironmentSnapshotService>();
        services.AddSingleton<HardwareCalibrationService>();
        services.AddSingleton<HardwareConstraintRunner>();
        services.AddSingleton<PerformanceSimulationService>();
        services.AddSingleton<IntelligenceExecutionSafety>();
        services.AddSingleton<IMinimumHardwareRunner, MinimumHardwareRunner>();
        services.AddSingleton<IRegressionRunner, RegressionRunner>();
        services.AddSingleton<IReliabilityRunner, ReliabilityRunner>();
        services.AddSingleton<IMediaRegressionRunner, MediaRegressionRunner>();
        services.AddSingleton<IFuzzRunner, FuzzRunner>();
        services.AddSingleton<IMutationRunner, MutationRunner>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();
        services.AddScoped<IP2IntelligenceRepository, P2IntelligenceRepository>();
        services.AddScoped<IHardwareCommunityRepository, HardwareCommunityRepository>();
        services.AddScoped<IDecisionTraceRepository, DecisionTraceRepository>();
        services.AddScoped<IAdaptiveQaPlanRepository, AdaptiveQaPlanRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IDurableJobRepository, DurableJobRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
        services.AddScoped<IGameReplayRepository, GameReplayRepository>();
        services.AddScoped<IPerformanceBudgetRepository, PerformanceBudgetRepository>();
        services.AddScoped<IHardwareBacktestRepository, HardwareBacktestRepository>();
        services.AddScoped<IChangeRiskRepository, ChangeRiskRepository>();
        services.AddScoped<IRegressionTriageRepository, RegressionTriageRepository>();
        services.AddScoped<IReleaseReadinessRepository, ReleaseReadinessRepository>();
        services.AddScoped<IExternalWorkItemRepository, ExternalWorkItemRepository>();
        services.AddScoped<IReleaseEvidenceRepository, ReleaseEvidenceRepository>();
        services.AddScoped<IRepositoryGraphRepository, RepositoryGraphRepository>();
        services.AddScoped<IDevelopmentGraphRepository, DevelopmentGraphRepository>();
        services.AddSingleton<Dip.Application.Abstractions.Spreadsheets.IGoogleSheetsConnector, Dip.Infrastructure.Spreadsheets.GoogleSheetsRestConnector>();
        services.AddSingleton<Dip.Application.Abstractions.Calendars.IGoogleCalendarConnector, Dip.Infrastructure.Calendars.GoogleCalendarRestConnector>();
        services.Configure<Dip.Infrastructure.Integrations.PerforceOptions>(configuration.GetSection(Dip.Infrastructure.Integrations.PerforceOptions.SectionName));
        services.Configure<Dip.Infrastructure.Integrations.JenkinsOptions>(configuration.GetSection(Dip.Infrastructure.Integrations.JenkinsOptions.SectionName));
        services.AddSingleton<Dip.Application.Abstractions.Integrations.IVersionControlConnector, Dip.Infrastructure.Integrations.PerforceVersionControlConnector>();
        services.AddSingleton<Dip.Application.Abstractions.Integrations.IExternalCiConnector, Dip.Infrastructure.Integrations.JenkinsCiConnector>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.IP2ExperimentRunner, Dip.Infrastructure.P2Intelligence.P2ExperimentRunner>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.IMediaRegressionRunner, Dip.Infrastructure.P2Intelligence.MediaRegressionRunner>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.ITestImpactAnalyzer, Dip.Infrastructure.P2Intelligence.TestImpactAnalyzer>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.INativeSymbolicator, Dip.Infrastructure.P2Intelligence.NativeSymbolicator>();

        // Generative QA is explicit opt-in. Disabled mode keeps the deterministic local baseline.
        // Enabled mode fails closed at resolution time if endpoint/model/API-key configuration is incomplete.
        services.Configure<GenerativeQaOptions>(configuration.GetSection(GenerativeQaOptions.SectionName));
        services.AddSingleton<GenerativeQaRedactor>();
        services.AddSingleton<GenerativeQaResponseParser>();
        services.AddSingleton<DeterministicLocalDebugAgent>();
        services.AddHttpClient<OpenAiCompatibleFixProposalAgent>(client =>
        {
            // Per-request timeout is enforced by the adapter so cancellation and retry remain observable.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddScoped<IFixProposalAgent>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GenerativeQaOptions>>().Value;
            if (!options.Enabled)
                return sp.GetRequiredService<DeterministicLocalDebugAgent>();
            options.ValidateForEnabled();
            return sp.GetRequiredService<OpenAiCompatibleFixProposalAgent>();
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DipDbContext>());
        services.AddSingleton<ISystemClock, SystemClock>();

        services.Configure<Dip.Infrastructure.ExternalEvents.ExternalEventIngestionOptions>(configuration.GetSection(Dip.Infrastructure.ExternalEvents.ExternalEventIngestionOptions.SectionName));
        services.AddSingleton<Dip.Application.Features.ExternalEvents.IExternalIncidentIngestionAdapter, Dip.Infrastructure.ExternalEvents.SentryIncidentIngestionAdapter>();
        services.AddSingleton<Dip.Application.Features.ExternalEvents.IExternalIncidentIngestionAdapter, Dip.Infrastructure.ExternalEvents.OpenTelemetryIncidentIngestionAdapter>();

        services.Configure<Dip.Infrastructure.Artifacts.ArtifactStoreOptions>(configuration.GetSection(Dip.Infrastructure.Artifacts.ArtifactStoreOptions.SectionName));
        services.AddSingleton<Dip.Application.Abstractions.Artifacts.IArtifactStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<Dip.Infrastructure.Artifacts.ArtifactStoreOptions>>().Value;
            return options.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase)
                ? new Dip.Infrastructure.Artifacts.MinioArtifactStore(sp.GetRequiredService<IOptions<Dip.Infrastructure.Artifacts.ArtifactStoreOptions>>())
                : new Dip.Infrastructure.Artifacts.LocalArtifactStore(sp.GetRequiredService<IOptions<Dip.Infrastructure.Artifacts.ArtifactStoreOptions>>());
        });
        services.AddSingleton<Dip.Application.Features.Benchmarks.IBenchmarkExecutor, Dip.Infrastructure.Benchmarks.BenchmarkExecutor>();
        services.Configure<Dip.Infrastructure.Replay.GameReplayOptions>(configuration.GetSection(Dip.Infrastructure.Replay.GameReplayOptions.SectionName));
        services.Configure<Dip.Infrastructure.Provenance.ReleaseProvenanceOptions>(configuration.GetSection(Dip.Infrastructure.Provenance.ReleaseProvenanceOptions.SectionName));
        services.AddSingleton<Dip.Application.Abstractions.Replay.IGameReplayExecutor, Dip.Infrastructure.Replay.GameReplayExecutor>();
        services.Configure<Dip.Infrastructure.WorkItems.WorkItemIntegrationOptions>(configuration.GetSection(Dip.Infrastructure.WorkItems.WorkItemIntegrationOptions.SectionName));
        services.Configure<Dip.Infrastructure.Management.ManagementNotificationOptions>(configuration.GetSection(Dip.Infrastructure.Management.ManagementNotificationOptions.SectionName));
        services.Configure<Dip.Infrastructure.ProjectIntelligence.ProjectIntelligenceSourceOptions>(configuration.GetSection(Dip.Infrastructure.ProjectIntelligence.ProjectIntelligenceSourceOptions.SectionName));
        services.AddSingleton<Dip.Application.Features.ProjectIntelligence.IProjectSourceAdapter, Dip.Infrastructure.ProjectIntelligence.RedmineProjectSourceAdapter>();
        services.AddSingleton<Dip.Application.Features.ProjectIntelligence.IProjectSourceAdapter, Dip.Infrastructure.ProjectIntelligence.SlackProjectSourceAdapter>();
        services.AddSingleton<Dip.Infrastructure.ProjectIntelligence.CollaborationWebhookVerifier>();
        services.AddSingleton<Dip.Application.Features.Management.IManagementNotificationChannel, Dip.Infrastructure.Management.SlackManagementNotificationChannel>();
        services.AddSingleton<Dip.Application.Features.Management.IManagementNotificationChannel, Dip.Infrastructure.Management.TeamsManagementNotificationChannel>();
        services.AddSingleton<Dip.Application.Features.Management.IManagementNotificationChannel, Dip.Infrastructure.Management.EmailManagementNotificationChannel>();
        services.AddScoped<Dip.Application.Features.Management.IManagementSyncAdapter, Dip.Infrastructure.Management.GitHubManagementSyncAdapter>();
        services.AddScoped<Dip.Application.Features.Management.IManagementSyncAdapter, Dip.Infrastructure.Management.JiraManagementSyncAdapter>();
        services.AddScoped<Dip.Application.Features.Management.IManagementSyncAdapter, Dip.Infrastructure.Management.LinearManagementSyncAdapter>();
        services.AddSingleton<Dip.Application.Abstractions.WorkItems.IExternalWorkItemConnector, Dip.Infrastructure.WorkItems.GitHubWorkItemConnector>();
        services.AddSingleton<Dip.Application.Abstractions.WorkItems.IExternalWorkItemConnector, Dip.Infrastructure.WorkItems.JiraWorkItemConnector>();
        services.AddSingleton<Dip.Application.Abstractions.WorkItems.IExternalWorkItemConnector, Dip.Infrastructure.WorkItems.LinearWorkItemConnector>();
        services.AddSingleton<Dip.Application.Abstractions.Provenance.IReleaseProvenanceCollector, Dip.Infrastructure.Provenance.ReleaseProvenanceCollector>();
        services.AddSingleton<Dip.Infrastructure.Observability.DipOperationalMetrics>();
        services.AddSingleton<Dip.Application.Abstractions.Observability.IDipOperationalMetrics>(sp => sp.GetRequiredService<Dip.Infrastructure.Observability.DipOperationalMetrics>());
        services.Configure<Dip.Application.Features.Jobs.DurableJobQueueOptions>(configuration.GetSection(Dip.Application.Features.Jobs.DurableJobQueueOptions.SectionName));
        services.Configure<Dip.Infrastructure.Messaging.QueueOptions>(configuration.GetSection(Dip.Infrastructure.Messaging.QueueOptions.SectionName));

        services.Configure<QaExecutionOptions>(configuration.GetSection(QaExecutionOptions.SectionName));
        services.AddSingleton<IQaWorkspaceResolver, QaWorkspaceResolver>();
        services.AddSingleton<IVerifiedFixService, VerifiedFixService>();
        services.AddSingleton<IReproductionCoordinator>(sp => sp.GetRequiredService<ReproductionCoordinator>());
        services.AddSingleton<IHardwareCalibrationRunner>(sp => sp.GetRequiredService<HardwareCalibrationService>());
        services.AddSingleton<IPerformanceSimulationRunner>(sp => sp.GetRequiredService<PerformanceSimulationService>());

        // Native-first runtime: PostgreSQL is the durable coordination boundary.
        // No external coordination daemon, container, or Docker Desktop is required.
        services.AddSingleton<IQueueClient, PostgresQueueClient>();
        return services;
    }
}
