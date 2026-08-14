using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.Hardware;
using Dip.Application.Features.Intelligence;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.Messaging;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Persistence.Repositories;
using Dip.Infrastructure.QualityAutomation;
using Dip.Infrastructure.Hardware;
using Dip.Infrastructure.Intelligence;
using Dip.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Dip.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        var postgresConnection=configuration.GetConnectionString("PostgreSql");
        if(string.IsNullOrWhiteSpace(postgresConnection))throw new InvalidOperationException("ConnectionStrings:PostgreSql is missing.");

        services.AddDbContext<DipDbContext>(options=>options.UseNpgsql(postgresConnection));
        services.AddScoped<LegacyMigrationAdopter>();
        services.AddScoped<IProjectRepository,ProjectRepository>();
        services.AddScoped<IPipelineRunRepository,PipelineRunRepository>();
        services.AddScoped<IMetricRepository,MetricRepository>();
        services.AddScoped<IQualityIssueRepository,QualityIssueRepository>();
        services.AddScoped<IEvidenceRepository,EvidenceRepository>();
        services.AddScoped<IProjectClaimRepository,ProjectClaimRepository>();
        services.AddScoped<IClaimEvidenceLinkRepository,ClaimEvidenceLinkRepository>();
        services.AddScoped<IProjectTruthRepository,ProjectTruthRepository>();
        services.AddScoped<IAuditLogRepository,AuditLogRepository>();
        services.AddScoped<IQaRunRepository,QaRunRepository>();
        services.AddScoped<IQaFindingRepository,QaFindingRepository>();
        services.AddScoped<IReproductionAttemptRepository,ReproductionAttemptRepository>();
        services.AddScoped<IHardwareProfileRepository,HardwareProfileRepository>();
        services.AddScoped<IHardwareCalibrationRepository,HardwareCalibrationRepository>();
        services.AddScoped<IPerformanceSimulationRepository,PerformanceSimulationRepository>();
        services.AddScoped<ICommunityHardwareStore,CommunityHardwareStore>();
        services.AddScoped<IIntelligenceRunStore,IntelligenceRunStore>();
        services.AddScoped<IProjectForecastStore,ProjectForecastStore>();

        services.AddSingleton<ProcessExecutor>();
        services.AddSingleton<PythonQaEngine>();
        services.AddSingleton<CSharpQaEngine>();
        services.AddSingleton<CppQaEngine>();
        services.AddSingleton<IDiagnosticNormalizer,DiagnosticNormalizer>();
        services.AddSingleton<IWorkspaceTechnologyDetector,WorkspaceTechnologyDetector>();
        services.AddSingleton<IQaAdapter>(sp=>sp.GetRequiredService<PythonQaEngine>());
        services.AddSingleton<IQaAdapter>(sp=>sp.GetRequiredService<CSharpQaEngine>());
        services.AddSingleton<IQaAdapter>(sp=>sp.GetRequiredService<CppQaEngine>());
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.JavaScript));
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.TypeScript));
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.Go));
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.Rust));
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.Java));
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.Kotlin));
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.Lua));
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.Hlsl));
        services.AddSingleton<IQaAdapter>(sp=>new ConventionalQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>(),LanguageId.Glsl));
        services.AddSingleton<IQaAdapter>(sp=>new UniversalFallbackQaAdapter(sp.GetRequiredService<ProcessExecutor>(),sp.GetRequiredService<IDiagnosticNormalizer>()));
        services.AddSingleton<IQaAdapterRegistry,QaAdapterRegistry>();

        services.AddSingleton<ReproductionCoordinator>();
        services.AddSingleton<EnvironmentSnapshotService>();
        services.AddSingleton<HardwareCalibrationService>();
        services.AddSingleton<HardwareConstraintRunner>();
        services.AddSingleton<PerformanceSimulationService>();
        services.AddSingleton<IntelligenceExecutionSafety>();
        services.AddSingleton<IMinimumHardwareRunner,MinimumHardwareRunner>();
        services.AddSingleton<IRegressionRunner,RegressionRunner>();
        services.AddSingleton<IReliabilityRunner,ReliabilityRunner>();
        services.AddSingleton<IMediaRegressionRunner,MediaRegressionRunner>();
        services.AddSingleton<IFuzzRunner,FuzzRunner>();
        services.AddSingleton<IMutationRunner,MutationRunner>();
        services.AddScoped<IOutboxRepository,OutboxRepository>();
        services.AddScoped<IDashboardReadRepository,DashboardReadRepository>();
        services.AddScoped<IP2IntelligenceRepository,P2IntelligenceRepository>();
        services.AddScoped<IHardwareCommunityRepository,HardwareCommunityRepository>();
        services.AddScoped<IDecisionTraceRepository,DecisionTraceRepository>();
        services.AddScoped<IAdaptiveQaPlanRepository,AdaptiveQaPlanRepository>();
        services.AddScoped<IArtifactRepository,ArtifactRepository>();
        services.AddScoped<IDurableJobRepository,DurableJobRepository>();
        services.AddScoped<IIncidentRepository,IncidentRepository>();
        services.AddScoped<IBenchmarkRepository,BenchmarkRepository>();
        services.AddScoped<IGameReplayRepository,GameReplayRepository>();
        services.AddScoped<IPerformanceBudgetRepository,PerformanceBudgetRepository>();
        services.AddScoped<IHardwareBacktestRepository,HardwareBacktestRepository>();
        services.AddScoped<IChangeRiskRepository,ChangeRiskRepository>();
        services.AddScoped<IRegressionTriageRepository,RegressionTriageRepository>();
        services.AddScoped<IReleaseReadinessRepository,ReleaseReadinessRepository>();
        services.AddScoped<IExternalWorkItemRepository,ExternalWorkItemRepository>();
        services.AddScoped<IReleaseEvidenceRepository,ReleaseEvidenceRepository>();
        services.AddScoped<IRepositoryGraphRepository,RepositoryGraphRepository>();
        services.AddScoped<IDevelopmentGraphRepository,DevelopmentGraphRepository>();
        services.AddSingleton<Dip.Application.Abstractions.Spreadsheets.IGoogleSheetsConnector,Dip.Infrastructure.Spreadsheets.GoogleSheetsRestConnector>();
        services.Configure<Dip.Infrastructure.Integrations.PerforceOptions>(configuration.GetSection(Dip.Infrastructure.Integrations.PerforceOptions.SectionName));
        services.Configure<Dip.Infrastructure.Integrations.JenkinsOptions>(configuration.GetSection(Dip.Infrastructure.Integrations.JenkinsOptions.SectionName));
        services.AddSingleton<Dip.Application.Abstractions.Integrations.IVersionControlConnector,Dip.Infrastructure.Integrations.PerforceVersionControlConnector>();
        services.AddSingleton<Dip.Application.Abstractions.Integrations.IExternalCiConnector,Dip.Infrastructure.Integrations.JenkinsCiConnector>();
        services.Configure<Dip.Infrastructure.P2Intelligence.P2IntelligenceOptions>(configuration.GetSection(Dip.Infrastructure.P2Intelligence.P2IntelligenceOptions.SectionName));
        services.AddSingleton<Dip.Application.Features.P2Intelligence.IP2ExperimentRunner,Dip.Infrastructure.P2Intelligence.P2ExperimentRunner>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.IMediaRegressionRunner,Dip.Infrastructure.P2Intelligence.MediaRegressionRunner>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.ITestImpactAnalyzer,Dip.Infrastructure.P2Intelligence.TestImpactAnalyzer>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.INativeSymbolicator,Dip.Infrastructure.P2Intelligence.NativeSymbolicator>();
        services.AddSingleton<Dip.Infrastructure.P2Intelligence.OpenAiCompatibleRootCauseClient>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.IAiRootCauseClient>(sp=>sp.GetRequiredService<Dip.Infrastructure.P2Intelligence.OpenAiCompatibleRootCauseClient>());
        services.AddScoped<IUnitOfWork>(provider=>provider.GetRequiredService<DipDbContext>());
        services.AddSingleton<ISystemClock,SystemClock>();

        services.Configure<Dip.Infrastructure.Artifacts.ArtifactStoreOptions>(configuration.GetSection(Dip.Infrastructure.Artifacts.ArtifactStoreOptions.SectionName));
        services.AddSingleton<Dip.Application.Abstractions.Artifacts.IArtifactStore>(sp=>
        {
            var options=sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Dip.Infrastructure.Artifacts.ArtifactStoreOptions>>().Value;
            return options.Provider.Equals("Minio",StringComparison.OrdinalIgnoreCase)?new Dip.Infrastructure.Artifacts.MinioArtifactStore(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Dip.Infrastructure.Artifacts.ArtifactStoreOptions>>()):new Dip.Infrastructure.Artifacts.LocalArtifactStore(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Dip.Infrastructure.Artifacts.ArtifactStoreOptions>>());
        });
        services.AddSingleton<Dip.Application.Features.Benchmarks.IBenchmarkExecutor,Dip.Infrastructure.Benchmarks.BenchmarkExecutor>();
        services.Configure<Dip.Infrastructure.Replay.GameReplayOptions>(configuration.GetSection(Dip.Infrastructure.Replay.GameReplayOptions.SectionName));
        services.Configure<Dip.Infrastructure.Provenance.ReleaseProvenanceOptions>(configuration.GetSection(Dip.Infrastructure.Provenance.ReleaseProvenanceOptions.SectionName));
        services.AddSingleton<Dip.Application.Abstractions.Replay.IGameReplayExecutor,Dip.Infrastructure.Replay.GameReplayExecutor>();
        services.AddSingleton<Dip.Application.Abstractions.WorkItems.IExternalWorkItemConnector,Dip.Infrastructure.WorkItems.GitHubWorkItemConnector>();
        services.AddSingleton<Dip.Application.Abstractions.Provenance.IReleaseProvenanceCollector,Dip.Infrastructure.Provenance.ReleaseProvenanceCollector>();
        services.AddSingleton<Dip.Infrastructure.Observability.DipOperationalMetrics>();
        services.AddSingleton<Dip.Application.Abstractions.Observability.IDipOperationalMetrics>(sp=>sp.GetRequiredService<Dip.Infrastructure.Observability.DipOperationalMetrics>());
        services.Configure<Dip.Application.Features.Jobs.DurableJobQueueOptions>(configuration.GetSection(Dip.Application.Features.Jobs.DurableJobQueueOptions.SectionName));

        services.Configure<QaExecutionOptions>(configuration.GetSection(QaExecutionOptions.SectionName));
        services.AddSingleton<IQaWorkspaceResolver,QaWorkspaceResolver>();
        services.AddSingleton<IPythonHardcodedQaRunner>(sp=>sp.GetRequiredService<PythonQaEngine>());
        services.AddSingleton<ICSharpHardcodedQaRunner>(sp=>sp.GetRequiredService<CSharpQaEngine>());
        services.AddSingleton<ICppHardcodedQaRunner>(sp=>sp.GetRequiredService<CppQaEngine>());
        services.AddSingleton<IReproductionCoordinator>(sp=>sp.GetRequiredService<ReproductionCoordinator>());
        services.AddSingleton<IHardwareCalibrationRunner>(sp=>sp.GetRequiredService<HardwareCalibrationService>());
        services.AddSingleton<IPerformanceSimulationRunner>(sp=>sp.GetRequiredService<PerformanceSimulationService>());

        services.Configure<RedisQueueOptions>(configuration.GetSection(RedisQueueOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(_=>
        {
            var redisConnection=configuration["Redis:ConnectionString"]??configuration.GetConnectionString("Redis")??"localhost:6379";
            var options=ConfigurationOptions.Parse(redisConnection);options.AbortOnConnectFail=false;options.ConnectRetry=Math.Max(options.ConnectRetry,3);return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IQueueClient,RedisQueueClient>();
        return services;
    }
}
