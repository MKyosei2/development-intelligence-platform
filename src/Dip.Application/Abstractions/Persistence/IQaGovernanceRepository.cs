// Purpose: Defines the IQaGovernanceRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IQaGovernanceRepository
{
    Task<QaFindingState?> FindFindingStateAsync(Guid projectId,string fingerprint,CancellationToken ct);
    Task AddFindingStateAsync(QaFindingState state,CancellationToken ct);
    Task<IReadOnlyList<QaFindingState>> ListFindingStatesAsync(Guid projectId,int take,CancellationToken ct);
    Task<IReadOnlyList<QaFindingState>> ListFindingStatesPageAsync(Guid projectId,DateTimeOffset? before,string? beforeFingerprint,int take,CancellationToken ct);
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
    Task<QaGovernanceProcessedRun?> GetProcessedRunAsync(Guid qaRunId,CancellationToken ct);
    Task AddProcessedRunAsync(QaGovernanceProcessedRun record,CancellationToken ct);
    Task<QaPolicy?> GetPolicyAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<QaPolicy>> ListPoliciesAsync(Guid projectId,CancellationToken ct);
    Task<QaReleaseGateOverride?> GetReleaseOverrideAsync(Guid id,CancellationToken ct);
    Task AddReleaseOverrideAsync(QaReleaseGateOverride record,CancellationToken ct);
    Task<IReadOnlyList<QaReleaseGateOverride>> ListReleaseOverridesAsync(Guid projectId,int take,CancellationToken ct);
    Task AddBackupVerificationAsync(QaBackupVerificationRecord record,CancellationToken ct);
    Task<IReadOnlyList<QaBackupVerificationRecord>> ListBackupVerificationsAsync(Guid projectId,int take,CancellationToken ct);
}
