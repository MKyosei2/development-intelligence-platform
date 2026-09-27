// Purpose: Verifies Migration Metadata behavior, edge cases, and repository invariants.
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Dip.Infrastructure.Tests.Persistence;

public sealed class MigrationMetadataTests
{
    [Fact]
    public void FormalMigrationChain_IsDiscoverable_AndModelBuilds()
    {
        var options = new DbContextOptionsBuilder<DipDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=dip_metadata_test;Username=dip;Password=dip-test-password")
            .Options;
        using var db = new DipDbContext(options);

        var migrations = db.Database.GetMigrations().ToArray();
        var entities = db.Model.GetEntityTypes().Select(x => x.ClrType.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("20260812000100_InitialBaseline", migrations);
        Assert.Contains("20260812000200_AddDurablePlatformIntelligence", migrations);
        Assert.Contains("20260812000300_AddDevelopmentIntelligenceExpansion", migrations);
        Assert.Contains("20260812000400_AddProjectControlGraph", migrations);
        Assert.Contains("20260813000500_AddProjectForecasts", migrations);
        Assert.Contains("20260814000600_AddQaGovernance", migrations);
        Assert.Contains("20260814000700_QaGovernanceHardening", migrations);
        Assert.Contains("DurableJob", entities);
        Assert.Contains("ArtifactRecord", entities);
        Assert.Contains("Incident", entities);
        Assert.Contains("BenchmarkRun", entities);
        Assert.Contains("GameReplayRun", entities);
        Assert.Contains("PerformanceBudgetProfile", entities);
        Assert.Contains("HardwareBacktest", entities);
        Assert.Contains("ChangeRiskAssessment", entities);
        Assert.Contains("RegressionTriage", entities);
        Assert.Contains("ReleaseReadinessSnapshot", entities);
        Assert.Contains("ExternalWorkItemLink", entities);
        Assert.Contains("ReleaseEvidenceBundle", entities);
        Assert.Contains("RepositoryDependency", entities);
        Assert.Contains("DevelopmentGraphNode", entities);
        Assert.Contains("DevelopmentGraphEdge", entities);
        Assert.Contains("ProjectForecast", entities);
        Assert.Contains("QaFindingState", entities);
        Assert.Contains("QaWaiver", entities);
        Assert.Contains("QaTestReliability", entities);
        Assert.Contains("QaRegressionBaseline", entities);
        Assert.Contains("QaOwnershipRule", entities);
        Assert.Contains("QaPolicy", entities);
        Assert.Contains("QaPolicyEvaluation", entities);
        Assert.Contains("QaEvidenceProvenance", entities);
        Assert.Contains("QaSystemHealthSnapshot", entities);
        Assert.Contains("QaQualityTrendSnapshot", entities);
        Assert.Contains("QaTestImpactRule", entities);
        Assert.Contains("QaArtifactRetentionRule", entities);
        Assert.Contains("QaRunCostRecord", entities);
        Assert.Contains("QaBugMinimizationRecord", entities);
        Assert.Contains("ProjectMembership", entities);
        Assert.Contains("QaGovernanceProcessedRun", entities);
        Assert.Contains("QaReleaseGateOverride", entities);
        Assert.Contains("QaBackupVerificationRecord", entities);
    }
    [Fact]
    public void DifficultyMigration_DoesNotRecreateObjectsOwnedByEarlierMigrations()
    {
        var options = new DbContextOptionsBuilder<DipDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=dip_metadata_test;Username=dip;Password=dip-test-password")
            .Options;
        using var db = new DipDbContext(options);

        var assembly = db.GetService<IMigrationsAssembly>();
        var migrationType = assembly.Migrations["20260914092630_AddDifficulty1Foundation"];
        var migration = assembly.CreateMigration(migrationType, db.Database.ProviderName!);

        Assert.DoesNotContain(
            migration.UpOperations.OfType<CreateTableOperation>(),
            operation => operation.Name == "project_forecasts");

        var baselineOwnedColumns = new HashSet<string>(StringComparer.Ordinal)
        {
            "QualityIssues.CreatedAtUtc",
            "QualityIssues.FirstSeenPipelineRunId",
            "QualityIssues.LastSeenPipelineRunId",
            "QualityIssues.OccurrenceCount",
            "QualityIssues.ResolvedAtUtc",
            "QualityIssues.Severity",
            "QualityIssues.Status",
            "QualityGateResults.CriticalIssueCount",
            "QualityGateResults.WarningIssueCount",
            "Projects.CreatedAtUtc",
            "Projects.ReleaseDateUtc",
            "Projects.UpdatedAtUtc",
            "ProcessedWebhooks.AttemptCount",
            "ProcessedWebhooks.ProcessedAtUtc",
            "PipelineRuns.FinishedAtUtc",
            "PipelineRuns.StartedAtUtc",
            "PipelineRuns.Status",
            "OutboxMessages.AttemptCount",
            "OutboxMessages.CreatedAtUtc",
            "Metrics.BaselinePipelineRunId",
            "Metrics.ComparisonStatus",
            "Metrics.Severity"
        };

        Assert.DoesNotContain(
            migration.UpOperations.OfType<AddColumnOperation>(),
            operation => baselineOwnedColumns.Contains($"{operation.Table}.{operation.Name}"));
    }

}
