using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.Hardware;
using Dip.Application.Features.Intelligence;
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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnection = configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(postgresConnection))
            throw new InvalidOperationException("ConnectionStrings:PostgreSql is missing.");

        services.AddDbContext<DipDbContext>(options => options.UseNpgsql(postgresConnection));
        services.AddScoped<IProjectRepository, ProjectRepository>();
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
        services.AddScoped<IReproductionAttemptRepository, ReproductionAttemptRepository>();
        services.AddScoped<IHardwareProfileRepository, HardwareProfileRepository>();
        services.AddScoped<IHardwareCalibrationRepository, HardwareCalibrationRepository>();
        services.AddScoped<IPerformanceSimulationRepository, PerformanceSimulationRepository>();
        services.AddScoped<ICommunityHardwareStore, CommunityHardwareStore>();
        services.AddScoped<IIntelligenceRunStore, IntelligenceRunStore>();
        services.AddScoped<IProjectForecastStore, ProjectForecastStore>();
        services.AddSingleton<ProcessExecutor>();
        services.AddSingleton<PythonQaEngine>();
        services.AddSingleton<CSharpQaEngine>();
        services.AddSingleton<CppQaEngine>();
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
        services.Configure<Dip.Infrastructure.P2Intelligence.P2IntelligenceOptions>(configuration.GetSection(Dip.Infrastructure.P2Intelligence.P2IntelligenceOptions.SectionName));
        services.AddSingleton<Dip.Application.Features.P2Intelligence.IP2ExperimentRunner, Dip.Infrastructure.P2Intelligence.P2ExperimentRunner>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.IMediaRegressionRunner, Dip.Infrastructure.P2Intelligence.MediaRegressionRunner>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.ITestImpactAnalyzer, Dip.Infrastructure.P2Intelligence.TestImpactAnalyzer>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.INativeSymbolicator, Dip.Infrastructure.P2Intelligence.NativeSymbolicator>();
        services.AddSingleton<Dip.Infrastructure.P2Intelligence.OpenAiCompatibleRootCauseClient>();
        services.AddSingleton<Dip.Application.Features.P2Intelligence.IAiRootCauseClient>(sp => sp.GetRequiredService<Dip.Infrastructure.P2Intelligence.OpenAiCompatibleRootCauseClient>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DipDbContext>());
        services.AddSingleton<ISystemClock, SystemClock>();

        services.Configure<QaExecutionOptions>(configuration.GetSection(QaExecutionOptions.SectionName));
        services.AddSingleton<IQaWorkspaceResolver, QaWorkspaceResolver>();
        services.AddSingleton<IPythonHardcodedQaRunner>(sp => sp.GetRequiredService<PythonQaEngine>());
        services.AddSingleton<ICSharpHardcodedQaRunner>(sp => sp.GetRequiredService<CSharpQaEngine>());
        services.AddSingleton<ICppHardcodedQaRunner>(sp => sp.GetRequiredService<CppQaEngine>());
        services.AddSingleton<IReproductionCoordinator>(sp => sp.GetRequiredService<ReproductionCoordinator>());
        services.AddSingleton<IHardwareCalibrationRunner>(sp => sp.GetRequiredService<HardwareCalibrationService>());
        services.AddSingleton<IPerformanceSimulationRunner>(sp => sp.GetRequiredService<PerformanceSimulationService>());

        services.Configure<RedisQueueOptions>(configuration.GetSection(RedisQueueOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConnection = configuration["Redis:ConnectionString"]
                ?? configuration.GetConnectionString("Redis")
                ?? "localhost:6379";
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = Math.Max(options.ConnectRetry, 3);
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IQueueClient, RedisQueueClient>();
        return services;
    }
}
