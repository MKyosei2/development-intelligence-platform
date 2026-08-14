using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

public sealed class DipDbContext : DbContext, IUnitOfWork
{
    public DipDbContext(DbContextOptions<DipDbContext> options) : base(options) { }
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMembership> ProjectMemberships => Set<ProjectMembership>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<Metric> Metrics => Set<Metric>();
    public DbSet<QualityIssue> QualityIssues => Set<QualityIssue>();
    public DbSet<QualityGateResult> QualityGateResults => Set<QualityGateResult>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedWebhook> ProcessedWebhooks => Set<ProcessedWebhook>();
    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();
    public DbSet<ProjectClaim> ProjectClaims => Set<ProjectClaim>();
    public DbSet<ClaimEvidenceLink> ClaimEvidenceLinks => Set<ClaimEvidenceLink>();
    public DbSet<ProjectTruthSnapshot> ProjectTruthSnapshots => Set<ProjectTruthSnapshot>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<QaRun> QaRuns => Set<QaRun>();
    public DbSet<QaFinding> QaFindings => Set<QaFinding>();
    public DbSet<QaFindingState> QaFindingStates => Set<QaFindingState>();
    public DbSet<QaWaiver> QaWaivers => Set<QaWaiver>();
    public DbSet<QaTestReliability> QaTestReliability => Set<QaTestReliability>();
    public DbSet<QaRegressionBaseline> QaRegressionBaselines => Set<QaRegressionBaseline>();
    public DbSet<QaOwnershipRule> QaOwnershipRules => Set<QaOwnershipRule>();
    public DbSet<QaPolicy> QaPolicies => Set<QaPolicy>();
    public DbSet<QaPolicyEvaluation> QaPolicyEvaluations => Set<QaPolicyEvaluation>();
    public DbSet<QaEvidenceProvenance> QaEvidenceProvenance => Set<QaEvidenceProvenance>();
    public DbSet<QaSystemHealthSnapshot> QaSystemHealthSnapshots => Set<QaSystemHealthSnapshot>();
    public DbSet<QaQualityTrendSnapshot> QaQualityTrendSnapshots => Set<QaQualityTrendSnapshot>();
    public DbSet<QaTestImpactRule> QaTestImpactRules => Set<QaTestImpactRule>();
    public DbSet<QaArtifactRetentionRule> QaArtifactRetentionRules => Set<QaArtifactRetentionRule>();
    public DbSet<QaRunCostRecord> QaRunCosts => Set<QaRunCostRecord>();
    public DbSet<QaBugMinimizationRecord> QaBugMinimizations => Set<QaBugMinimizationRecord>();
    public DbSet<QaGovernanceProcessedRun> QaGovernanceProcessedRuns => Set<QaGovernanceProcessedRun>();
    public DbSet<QaReleaseGateOverride> QaReleaseGateOverrides => Set<QaReleaseGateOverride>();
    public DbSet<QaBackupVerificationRecord> QaBackupVerifications => Set<QaBackupVerificationRecord>();
    public DbSet<ReproductionAttempt> ReproductionAttempts => Set<ReproductionAttempt>();
    public DbSet<HardwareProfile> HardwareProfiles => Set<HardwareProfile>();
    public DbSet<HardwareCalibration> HardwareCalibrations => Set<HardwareCalibration>();
    public DbSet<PerformanceSimulationRun> PerformanceSimulationRuns => Set<PerformanceSimulationRun>();
    public DbSet<P2IntelligenceRecord> P2IntelligenceRecords => Set<P2IntelligenceRecord>();
    public DbSet<HardwareCommunitySample> HardwareCommunitySamples => Set<HardwareCommunitySample>();
    public DbSet<DecisionTrace> DecisionTraces => Set<DecisionTrace>();
    public DbSet<AdaptiveQaPlanSnapshot> AdaptiveQaPlanSnapshots => Set<AdaptiveQaPlanSnapshot>();
    public DbSet<ArtifactBlob> ArtifactBlobs => Set<ArtifactBlob>();
    public DbSet<ArtifactRecord> ArtifactRecords => Set<ArtifactRecord>();
    public DbSet<DurableJob> DurableJobs => Set<DurableJob>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentOccurrence> IncidentOccurrences => Set<IncidentOccurrence>();
    public DbSet<IncidentEvent> IncidentEvents => Set<IncidentEvent>();
    public DbSet<BenchmarkDefinition> BenchmarkDefinitions => Set<BenchmarkDefinition>();
    public DbSet<BenchmarkRun> BenchmarkRuns => Set<BenchmarkRun>();
    public DbSet<BenchmarkMetric> BenchmarkMetrics => Set<BenchmarkMetric>();
    public DbSet<BenchmarkComparison> BenchmarkComparisons => Set<BenchmarkComparison>();
    public DbSet<GameReplaySession> GameReplaySessions => Set<GameReplaySession>();
    public DbSet<GameReplayRun> GameReplayRuns => Set<GameReplayRun>();
    public DbSet<GameReplayMetric> GameReplayMetrics => Set<GameReplayMetric>();
    public DbSet<PerformanceBudgetProfile> PerformanceBudgetProfiles => Set<PerformanceBudgetProfile>();
    public DbSet<PerformanceBudgetEvaluation> PerformanceBudgetEvaluations => Set<PerformanceBudgetEvaluation>();
    public DbSet<HardwareBacktest> HardwareBacktests => Set<HardwareBacktest>();
    public DbSet<ChangeRiskAssessment> ChangeRiskAssessments => Set<ChangeRiskAssessment>();
    public DbSet<RegressionTriage> RegressionTriages => Set<RegressionTriage>();
    public DbSet<ReleaseReadinessSnapshot> ReleaseReadinessSnapshots => Set<ReleaseReadinessSnapshot>();
    public DbSet<ExternalWorkItemLink> ExternalWorkItemLinks => Set<ExternalWorkItemLink>();
    public DbSet<ReleaseEvidenceBundle> ReleaseEvidenceBundles => Set<ReleaseEvidenceBundle>();
    public DbSet<RepositoryNode> RepositoryNodes => Set<RepositoryNode>();
    public DbSet<RepositoryDependency> RepositoryDependencies => Set<RepositoryDependency>();
    public DbSet<CrossRepositoryImpact> CrossRepositoryImpacts => Set<CrossRepositoryImpact>();
    public DbSet<DevelopmentGraphNode> DevelopmentGraphNodes => Set<DevelopmentGraphNode>();
    public DbSet<DevelopmentGraphEdge> DevelopmentGraphEdges => Set<DevelopmentGraphEdge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DipDbContext).Assembly);
    }
}
