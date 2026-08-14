using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Tests.Persistence;

public sealed class MigrationMetadataTests
{
    [Fact]
    public void FormalMigrationChain_IsDiscoverable_AndModelBuilds()
    {
        var options=new DbContextOptionsBuilder<DipDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=dip_metadata_test;Username=dip;Password=dip")
            .Options;
        using var db=new DipDbContext(options);

        var migrations=db.Database.GetMigrations().ToArray();
        var entities=db.Model.GetEntityTypes().Select(x=>x.ClrType.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("202608120001_InitialBaseline",migrations);
        Assert.Contains("202608120002_AddDurablePlatformIntelligence",migrations);
        Assert.Contains("202608120003_AddDevelopmentIntelligenceExpansion",migrations);
        Assert.Contains("202608120004_AddProjectControlGraph",migrations);
        Assert.Contains("202608130005_AddProjectForecasts",migrations);
        Assert.Contains("202608140006_AddQaGovernance",migrations);
        Assert.Contains("202608140007_QaGovernanceHardening",migrations);
        Assert.Contains("DurableJob",entities);
        Assert.Contains("ArtifactRecord",entities);
        Assert.Contains("Incident",entities);
        Assert.Contains("BenchmarkRun",entities);
        Assert.Contains("GameReplayRun",entities);
        Assert.Contains("PerformanceBudgetProfile",entities);
        Assert.Contains("HardwareBacktest",entities);
        Assert.Contains("ChangeRiskAssessment",entities);
        Assert.Contains("RegressionTriage",entities);
        Assert.Contains("ReleaseReadinessSnapshot",entities);
        Assert.Contains("ExternalWorkItemLink",entities);
        Assert.Contains("ReleaseEvidenceBundle",entities);
        Assert.Contains("RepositoryDependency",entities);
        Assert.Contains("DevelopmentGraphNode",entities);
        Assert.Contains("DevelopmentGraphEdge",entities);
        Assert.Contains("ProjectForecast",entities);
        Assert.Contains("QaFindingState",entities);
        Assert.Contains("QaWaiver",entities);
        Assert.Contains("QaTestReliability",entities);
        Assert.Contains("QaRegressionBaseline",entities);
        Assert.Contains("QaOwnershipRule",entities);
        Assert.Contains("QaPolicy",entities);
        Assert.Contains("QaPolicyEvaluation",entities);
        Assert.Contains("QaEvidenceProvenance",entities);
        Assert.Contains("QaSystemHealthSnapshot",entities);
        Assert.Contains("QaQualityTrendSnapshot",entities);
        Assert.Contains("QaTestImpactRule",entities);
        Assert.Contains("QaArtifactRetentionRule",entities);
        Assert.Contains("QaRunCostRecord",entities);
        Assert.Contains("QaBugMinimizationRecord",entities);
        Assert.Contains("ProjectMembership",entities);
        Assert.Contains("QaGovernanceProcessedRun",entities);
        Assert.Contains("QaReleaseGateOverride",entities);
        Assert.Contains("QaBackupVerificationRecord",entities);
    }
}
