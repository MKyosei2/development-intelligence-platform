using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct);
    Task AddAsync(Project project,CancellationToken ct);
}
public interface IPipelineRunRepository
{
    Task<PipelineRun?> GetByIdAsync(Guid id,CancellationToken ct);
    Task<PipelineRun?> FindByExternalRunAsync(Guid projectId,string externalRunId,string collectorType,int attemptNumber,CancellationToken ct);
    Task AddAsync(PipelineRun run,CancellationToken ct);
}
public interface IMetricRepository{Task AddRangeAsync(IEnumerable<Metric> metrics,CancellationToken ct);}
public interface IQualityIssueRepository
{
    Task<QualityIssue?> FindOpenByFingerprintAsync(Guid projectId,string fingerprint,CancellationToken ct);
    Task AddAsync(QualityIssue issue,CancellationToken ct);
}
public interface IEvidenceRepository
{
    Task AddAsync(EvidenceRecord evidence,CancellationToken ct);
    Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence,CancellationToken ct);
    Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(Guid projectId,DateTimeOffset? observedSince,CancellationToken ct);
}
public interface IProjectClaimRepository
{
    Task<ProjectClaim?> GetByIdAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<ProjectClaim>> ListByProjectAsync(Guid projectId,CancellationToken ct);
    Task AddAsync(ProjectClaim claim,CancellationToken ct);
}
public interface IClaimEvidenceLinkRepository
{
    Task ReplaceForClaimAsync(Guid claimId,IReadOnlyCollection<ClaimEvidenceLink> links,CancellationToken ct);
}
public interface IProjectTruthRepository
{
    Task<ProjectTruthSnapshot?> GetLatestAsync(Guid projectId,CancellationToken ct);
    Task<IReadOnlyList<ProjectTruthSnapshot>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
    Task AddAsync(ProjectTruthSnapshot snapshot,CancellationToken ct);
}
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry,CancellationToken ct);
}
public interface IQaRunRepository
{
    Task AddAsync(QaRun run,CancellationToken ct);
    Task<QaRun?> GetAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<QaRun>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IQaFindingRepository
{
    Task AddRangeAsync(IEnumerable<QaFinding> findings,CancellationToken ct);
    Task<QaFinding?> GetAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<QaFinding>> ListByRunAsync(Guid runId,CancellationToken ct);
    Task<IReadOnlyList<QaFinding>> ListRecentByProjectAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IReproductionAttemptRepository
{
    Task AddAsync(ReproductionAttempt attempt,CancellationToken ct);
    Task<IReadOnlyList<ReproductionAttempt>> ListByFindingAsync(Guid findingId,CancellationToken ct);
}
public interface IHardwareProfileRepository
{
    Task AddAsync(HardwareProfile profile,CancellationToken ct);
    Task<HardwareProfile?> GetAsync(Guid id,CancellationToken ct);
    Task<HardwareProfile?> FindAsync(string name,string platform,CancellationToken ct);
    Task<IReadOnlyList<HardwareProfile>> ListAsync(CancellationToken ct);
}
public interface IHardwareCalibrationRepository
{
    Task AddAsync(HardwareCalibration calibration,CancellationToken ct);
    Task<HardwareCalibration?> GetLatestAsync(Guid projectId,CancellationToken ct);
}
public interface IPerformanceSimulationRepository
{
    Task AddAsync(PerformanceSimulationRun run,CancellationToken ct);
    Task<PerformanceSimulationRun?> GetAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<PerformanceSimulationRun>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
}

public interface IP2IntelligenceRepository
{
    Task AddAsync(P2IntelligenceRecord record,CancellationToken ct);
    Task<IReadOnlyList<P2IntelligenceRecord>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
    Task<IReadOnlyList<P2IntelligenceRecord>> ListByKindAsync(Guid projectId,Dip.Domain.Enums.P2IntelligenceKind kind,int take,CancellationToken ct);
}
public interface IHardwareCommunityRepository
{
    Task AddAsync(HardwareCommunitySample sample,CancellationToken ct);
    Task<IReadOnlyList<HardwareCommunitySample>> ListAsync(string profileKey,string platform,int take,CancellationToken ct);
}
public interface IDecisionTraceRepository
{
    Task AddAsync(DecisionTrace trace,CancellationToken ct);
    Task<IReadOnlyList<DecisionTrace>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IAdaptiveQaPlanRepository
{
    Task AddAsync(AdaptiveQaPlanSnapshot plan,CancellationToken ct);
    Task<AdaptiveQaPlanSnapshot?> GetLatestAsync(Guid projectId,CancellationToken ct);
}

public interface IArtifactRepository
{
    Task<ArtifactBlob?> FindBlobAsync(string provider,string sha256,CancellationToken ct);
    Task<ArtifactBlob> GetOrCreateBlobAsync(ArtifactBlob candidate,CancellationToken ct);
    Task<ArtifactRecord?> GetRecordAsync(Guid id,CancellationToken ct);
    Task<ArtifactRecord?> GetRecordForUpdateAsync(Guid id,CancellationToken ct);
    Task<ArtifactBlob?> GetBlobAsync(Guid id,CancellationToken ct);
    Task<ArtifactBlob?> GetBlobForUpdateAsync(Guid id,CancellationToken ct);
    Task AddBlobAsync(ArtifactBlob blob,CancellationToken ct);
    Task AddRecordAsync(ArtifactRecord record,CancellationToken ct);
    Task<IReadOnlyList<ArtifactRecord>> ListForJobAsync(Guid jobId,CancellationToken ct);
    Task<IReadOnlyList<ArtifactRecord>> ListExpiredAsync(DateTimeOffset now,int take,CancellationToken ct);
    Task<bool> HasRecordsForBlobAsync(Guid blobId,CancellationToken ct);
    void RemoveRecord(ArtifactRecord record);
    void RemoveBlob(ArtifactBlob blob);
}
public interface IDurableJobRepository
{
    Task AddAsync(DurableJob job,CancellationToken ct);
    Task<DurableJob?> GetAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<DurableJob>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
    Task<DurableJob?> TryClaimAsync(Guid id,string owner,DateTimeOffset now,TimeSpan lease,CancellationToken ct);
    Task<IReadOnlyList<DurableJob>> ListExpiredLeasesAsync(DateTimeOffset now,int take,CancellationToken ct);
    Task<long> CountQueuedAsync(CancellationToken ct);
}
public interface IIncidentRepository
{
    Task<Incident?> FindByFingerprintAsync(Guid projectId,string fingerprint,CancellationToken ct);
    Task<Incident?> GetAsync(Guid id,CancellationToken ct);
    Task<Incident?> FindLatestOpenForSubjectAsync(Guid projectId,string subjectKey,CancellationToken ct);
    Task AddAsync(Incident incident,CancellationToken ct);
    Task AddOccurrenceAsync(IncidentOccurrence occurrence,CancellationToken ct);
    Task AddEventAsync(IncidentEvent incidentEvent,CancellationToken ct);
    Task<IReadOnlyList<Incident>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
    Task<IReadOnlyList<IncidentEvent>> ListEventsAsync(Guid incidentId,CancellationToken ct);
    Task<IReadOnlyList<IncidentOccurrence>> ListOccurrencesAsync(Guid incidentId,int take,CancellationToken ct);
}
public interface IBenchmarkRepository
{
    Task AddDefinitionAsync(BenchmarkDefinition definition,CancellationToken ct);
    Task<BenchmarkDefinition?> GetDefinitionAsync(Guid id,CancellationToken ct);
    Task<BenchmarkDefinition?> GetDefinitionByKeyAsync(string key,CancellationToken ct);
    Task<IReadOnlyList<BenchmarkDefinition>> ListDefinitionsAsync(CancellationToken ct);
    Task AddRunAsync(BenchmarkRun run,CancellationToken ct);
    Task<BenchmarkRun?> GetRunAsync(Guid id,CancellationToken ct);
    Task<BenchmarkRun?> FindBaselineAsync(Guid projectId,Guid definitionId,string environmentFingerprint,string configurationHash,string excludingCommit,CancellationToken ct);
    Task<IReadOnlyList<BenchmarkRun>> ListSucceededRunsAsync(Guid definitionId,int take,CancellationToken ct);
    Task AddMetricsAsync(IEnumerable<BenchmarkMetric> metrics,CancellationToken ct);
    Task<IReadOnlyList<BenchmarkMetric>> ListMetricsAsync(Guid runId,CancellationToken ct);
    Task AddComparisonsAsync(IEnumerable<BenchmarkComparison> comparisons,CancellationToken ct);
    Task<IReadOnlyList<BenchmarkComparison>> ListComparisonsAsync(Guid runId,CancellationToken ct);
}

public interface IUnitOfWork{Task<int> SaveChangesAsync(CancellationToken ct);}

public interface IGameReplayRepository
{
    Task AddSessionAsync(GameReplaySession session,CancellationToken ct);
    Task<GameReplaySession?> GetSessionAsync(Guid id,CancellationToken ct);
    Task<GameReplaySession?> GetSessionByKeyAsync(Guid projectId,string key,CancellationToken ct);
    Task<IReadOnlyList<GameReplaySession>> ListSessionsAsync(Guid projectId,CancellationToken ct);
    Task AddRunAsync(GameReplayRun run,CancellationToken ct);
    Task<GameReplayRun?> GetRunAsync(Guid id,CancellationToken ct);
    Task<GameReplayRun?> FindBaselineRunAsync(Guid projectId,Guid sessionId,string hardwareFingerprint,string excludingCommit,CancellationToken ct);
    Task AddMetricsAsync(IEnumerable<GameReplayMetric> metrics,CancellationToken ct);
    Task<IReadOnlyList<GameReplayMetric>> ListMetricsAsync(Guid runId,CancellationToken ct);
}
public interface IPerformanceBudgetRepository
{
    Task AddProfileAsync(PerformanceBudgetProfile profile,CancellationToken ct);
    Task<PerformanceBudgetProfile?> GetProfileAsync(Guid id,CancellationToken ct);
    Task<PerformanceBudgetProfile?> GetProfileByKeyAsync(Guid? projectId,string key,CancellationToken ct);
    Task<IReadOnlyList<PerformanceBudgetProfile>> ListProfilesAsync(Guid? projectId,CancellationToken ct);
    Task AddEvaluationAsync(PerformanceBudgetEvaluation evaluation,CancellationToken ct);
    Task<IReadOnlyList<PerformanceBudgetEvaluation>> ListEvaluationsAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IHardwareBacktestRepository
{
    Task AddAsync(HardwareBacktest backtest,CancellationToken ct);
    Task<IReadOnlyList<HardwareBacktest>> ListAsync(Guid projectId,Guid? hardwareProfileId,int take,CancellationToken ct);
}
public interface IChangeRiskRepository
{
    Task AddAsync(ChangeRiskAssessment assessment,CancellationToken ct);
    Task<ChangeRiskAssessment?> GetLatestAsync(Guid projectId,string? commitSha,CancellationToken ct);
    Task<IReadOnlyList<ChangeRiskAssessment>> ListAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IRegressionTriageRepository
{
    Task AddAsync(RegressionTriage triage,CancellationToken ct);
    Task<RegressionTriage?> GetLatestForIncidentAsync(Guid incidentId,CancellationToken ct);
    Task<IReadOnlyList<RegressionTriage>> ListAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IReleaseReadinessRepository
{
    Task AddAsync(ReleaseReadinessSnapshot snapshot,CancellationToken ct);
    Task<ReleaseReadinessSnapshot?> GetLatestAsync(Guid projectId,CancellationToken ct);
    Task<IReadOnlyList<ReleaseReadinessSnapshot>> ListAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IExternalWorkItemRepository
{
    Task AddAsync(ExternalWorkItemLink link,CancellationToken ct);
    Task<ExternalWorkItemLink?> FindByIncidentAsync(Guid incidentId,Dip.Domain.Enums.ExternalWorkItemProvider provider,CancellationToken ct);
    Task<IReadOnlyList<ExternalWorkItemLink>> ListAsync(Guid projectId,CancellationToken ct);
}
public interface IReleaseEvidenceRepository
{
    Task AddAsync(ReleaseEvidenceBundle bundle,CancellationToken ct);
    Task<ReleaseEvidenceBundle?> GetLatestAsync(Guid projectId,CancellationToken ct);
    Task<IReadOnlyList<ReleaseEvidenceBundle>> ListAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IRepositoryGraphRepository
{
    Task AddNodeAsync(RepositoryNode node,CancellationToken ct);
    Task<RepositoryNode?> GetNodeAsync(Guid id,CancellationToken ct);
    Task<RepositoryNode?> GetNodeByKeyAsync(Guid projectId,string key,CancellationToken ct);
    Task<IReadOnlyList<RepositoryNode>> ListNodesAsync(Guid projectId,CancellationToken ct);
    Task AddDependencyAsync(RepositoryDependency dependency,CancellationToken ct);
    Task<IReadOnlyList<RepositoryDependency>> ListDependenciesAsync(Guid projectId,CancellationToken ct);
    Task AddImpactAsync(CrossRepositoryImpact impact,CancellationToken ct);
    Task<CrossRepositoryImpact?> GetLatestImpactAsync(Guid projectId,string commitSha,CancellationToken ct);
}
public interface IDevelopmentGraphRepository
{
    Task<DevelopmentGraphNode?> GetNodeAsync(Guid id,CancellationToken ct);
    Task<DevelopmentGraphNode?> FindNodeAsync(Guid projectId,Dip.Domain.Enums.DevelopmentGraphNodeType type,string key,CancellationToken ct);
    Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(Guid projectId,Dip.Domain.Enums.DevelopmentGraphNodeType? type,CancellationToken ct);
    Task AddNodeAsync(DevelopmentGraphNode node,CancellationToken ct);
    Task<DevelopmentGraphEdge?> FindEdgeAsync(Guid projectId,Guid sourceNodeId,Guid targetNodeId,Dip.Domain.Enums.DevelopmentGraphEdgeType type,CancellationToken ct);
    Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(Guid projectId,CancellationToken ct);
    Task AddEdgeAsync(DevelopmentGraphEdge edge,CancellationToken ct);
}


public interface IQaGovernanceRepository
{
    Task<QaFindingState?> FindFindingStateAsync(Guid projectId,string fingerprint,CancellationToken ct);
    Task AddFindingStateAsync(QaFindingState state,CancellationToken ct);
    Task<IReadOnlyList<QaFindingState>> ListFindingStatesAsync(Guid projectId,int take,CancellationToken ct);
    Task<QaWaiver?> GetWaiverAsync(Guid id,CancellationToken ct);
    Task AddWaiverAsync(QaWaiver waiver,CancellationToken ct);
    Task<IReadOnlyList<QaWaiver>> ListWaiversAsync(Guid projectId,CancellationToken ct);
    Task<QaTestReliability?> FindReliabilityAsync(Guid projectId,string testKey,CancellationToken ct);
    Task AddReliabilityAsync(QaTestReliability reliability,CancellationToken ct);
    Task<IReadOnlyList<QaTestReliability>> ListReliabilityAsync(Guid projectId,int take,CancellationToken ct);
    Task<QaRegressionBaseline?> FindBaselineAsync(Guid projectId,string subjectKey,string metricKey,CancellationToken ct);
    Task AddBaselineAsync(QaRegressionBaseline baseline,CancellationToken ct);
    Task<IReadOnlyList<QaRegressionBaseline>> ListBaselinesAsync(Guid projectId,int take,CancellationToken ct);
    Task AddOwnershipRuleAsync(QaOwnershipRule rule,CancellationToken ct);
    Task<IReadOnlyList<QaOwnershipRule>> ListOwnershipRulesAsync(Guid projectId,CancellationToken ct);
    Task AddPolicyAsync(QaPolicy policy,CancellationToken ct);
    Task<QaPolicy?> GetActivePolicyAsync(Guid projectId,CancellationToken ct);
    Task AddPolicyEvaluationAsync(QaPolicyEvaluation evaluation,CancellationToken ct);
    Task<QaPolicyEvaluation?> GetLatestPolicyEvaluationAsync(Guid projectId,CancellationToken ct);
    Task AddProvenanceAsync(QaEvidenceProvenance provenance,CancellationToken ct);
    Task<QaEvidenceProvenance?> GetLatestProvenanceAsync(Guid projectId,CancellationToken ct);
    Task AddSystemHealthAsync(QaSystemHealthSnapshot snapshot,CancellationToken ct);
    Task<IReadOnlyList<QaSystemHealthSnapshot>> ListSystemHealthAsync(Guid projectId,int take,CancellationToken ct);
    Task AddTrendAsync(QaQualityTrendSnapshot snapshot,CancellationToken ct);
    Task<IReadOnlyList<QaQualityTrendSnapshot>> ListTrendsAsync(Guid projectId,int take,CancellationToken ct);
    Task AddTestImpactRuleAsync(QaTestImpactRule rule,CancellationToken ct);
    Task<IReadOnlyList<QaTestImpactRule>> ListTestImpactRulesAsync(Guid projectId,CancellationToken ct);
    Task AddRetentionRuleAsync(QaArtifactRetentionRule rule,CancellationToken ct);
    Task<IReadOnlyList<QaArtifactRetentionRule>> ListRetentionRulesAsync(Guid projectId,CancellationToken ct);
    Task AddCostAsync(QaRunCostRecord cost,CancellationToken ct);
    Task<IReadOnlyList<QaRunCostRecord>> ListCostsAsync(Guid projectId,int take,CancellationToken ct);
    Task AddMinimizationAsync(QaBugMinimizationRecord record,CancellationToken ct);
    Task<IReadOnlyList<QaBugMinimizationRecord>> ListMinimizationsAsync(Guid projectId,int take,CancellationToken ct);
}
