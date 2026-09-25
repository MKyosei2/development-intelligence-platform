// Purpose: Implements QA Governance Repository infrastructure responsibilities for persistence.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class QaGovernanceRepository(DipDbContext db) : IQaGovernanceRepository
{
    public Task<QaFindingState?> FindFindingStateAsync(Guid p, string f, CancellationToken ct) => db.QaFindingStates.SingleOrDefaultAsync(x => x.ProjectId == p && x.Fingerprint == f, ct);
    public Task AddFindingStateAsync(QaFindingState x, CancellationToken ct) => db.QaFindingStates.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaFindingState>> ListFindingStatesAsync(Guid p, int take, CancellationToken ct) => await db.QaFindingStates.Where(x => x.ProjectId == p).OrderByDescending(x => x.LastSeenAtUtc).ThenByDescending(x => x.Id).Take(Math.Clamp(take, 1, 10000)).ToListAsync(ct);
    public async Task<IReadOnlyList<QaFindingState>> ListFindingStatesPageAsync(Guid p, DateTimeOffset? before, string? beforeFingerprint, int take, CancellationToken ct) { var q = db.QaFindingStates.AsNoTracking().Where(x => x.ProjectId == p);
    if (before.HasValue && !string.IsNullOrWhiteSpace(beforeFingerprint)) q = q.Where(x => x.LastSeenAtUtc < before.Value || (x.LastSeenAtUtc == before.Value && string.Compare(x.Fingerprint, beforeFingerprint) < 0));
     return await q.OrderByDescending(x => x.LastSeenAtUtc).ThenByDescending(x => x.Fingerprint).Take(Math.Clamp(take, 1, 501)).ToListAsync(ct);
     }
    public Task<QaWaiver?> GetWaiverAsync(Guid id, CancellationToken ct) => db.QaWaivers.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task AddWaiverAsync(QaWaiver x, CancellationToken ct) => db.QaWaivers.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaWaiver>> ListWaiversAsync(Guid p, CancellationToken ct) => await db.QaWaivers.Where(x => x.ProjectId == p).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<QaTestReliability?> FindReliabilityAsync(Guid p, string k, CancellationToken ct) => db.QaTestReliability.SingleOrDefaultAsync(x => x.ProjectId == p && x.TestKey == k, ct);
    public Task AddReliabilityAsync(QaTestReliability x, CancellationToken ct) => db.QaTestReliability.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaTestReliability>> ListReliabilityAsync(Guid p, int take, CancellationToken ct) => await db.QaTestReliability.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.LastObservedAtUtc).Take(Math.Clamp(take, 1, 5000)).ToListAsync(ct);
    public Task<QaRegressionBaseline?> FindBaselineAsync(Guid p, string s, string m, CancellationToken ct) => db.QaRegressionBaselines.SingleOrDefaultAsync(x => x.ProjectId == p && x.SubjectKey == s && x.MetricKey == m, ct);
    public Task AddBaselineAsync(QaRegressionBaseline x, CancellationToken ct) => db.QaRegressionBaselines.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaRegressionBaseline>> ListBaselinesAsync(Guid p, int take, CancellationToken ct) => await db.QaRegressionBaselines.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.UpdatedAtUtc).Take(Math.Clamp(take, 1, 5000)).ToListAsync(ct);
    public Task AddOwnershipRuleAsync(QaOwnershipRule x, CancellationToken ct) => db.QaOwnershipRules.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaOwnershipRule>> ListOwnershipRulesAsync(Guid p, CancellationToken ct) => await db.QaOwnershipRules.AsNoTracking().Where(x => x.ProjectId == p && x.Enabled).OrderByDescending(x => x.Priority).ToListAsync(ct);
    public Task AddPolicyAsync(QaPolicy x, CancellationToken ct) => db.QaPolicies.AddAsync(x, ct).AsTask();
    public Task<QaPolicy?> GetActivePolicyAsync(Guid p, CancellationToken ct) => db.QaPolicies.Where(x => x.ProjectId == p && x.LifecycleStatus == Dip.Domain.Enums.QaPolicyLifecycleStatus.Active && x.Enabled).OrderByDescending(x => x.Revision).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
    public Task AddPolicyEvaluationAsync(QaPolicyEvaluation x, CancellationToken ct) => db.QaPolicyEvaluations.AddAsync(x, ct).AsTask();
    public Task<QaPolicyEvaluation?> GetLatestPolicyEvaluationAsync(Guid p, CancellationToken ct) => db.QaPolicyEvaluations.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.EvaluatedAtUtc).FirstOrDefaultAsync(ct);
    public Task AddProvenanceAsync(QaEvidenceProvenance x, CancellationToken ct) => db.QaEvidenceProvenance.AddAsync(x, ct).AsTask();
    public Task<QaEvidenceProvenance?> GetLatestProvenanceAsync(Guid p, CancellationToken ct) => db.QaEvidenceProvenance.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.CapturedAtUtc).FirstOrDefaultAsync(ct);
    public Task AddSystemHealthAsync(QaSystemHealthSnapshot x, CancellationToken ct) => db.QaSystemHealthSnapshots.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaSystemHealthSnapshot>> ListSystemHealthAsync(Guid p, int take, CancellationToken ct) => await db.QaSystemHealthSnapshots.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.ObservedAtUtc).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct);
    public Task AddTrendAsync(QaQualityTrendSnapshot x, CancellationToken ct) => db.QaQualityTrendSnapshots.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaQualityTrendSnapshot>> ListTrendsAsync(Guid p, int take, CancellationToken ct) => await db.QaQualityTrendSnapshots.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.CapturedAtUtc).Take(Math.Clamp(take, 1, 5000)).ToListAsync(ct);
    public Task AddTestImpactRuleAsync(QaTestImpactRule x, CancellationToken ct) => db.QaTestImpactRules.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaTestImpactRule>> ListTestImpactRulesAsync(Guid p, CancellationToken ct) => await db.QaTestImpactRules.AsNoTracking().Where(x => x.ProjectId == p && x.Enabled).OrderByDescending(x => x.Priority).ToListAsync(ct);
    public Task AddRetentionRuleAsync(QaArtifactRetentionRule x, CancellationToken ct) => db.QaArtifactRetentionRules.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaArtifactRetentionRule>> ListRetentionRulesAsync(Guid p, CancellationToken ct) => await db.QaArtifactRetentionRules.AsNoTracking().Where(x => x.ProjectId == p && x.Enabled).OrderBy(x => x.RetentionDays).ToListAsync(ct);
    public Task AddCostAsync(QaRunCostRecord x, CancellationToken ct) => db.QaRunCosts.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaRunCostRecord>> ListCostsAsync(Guid p, int take, CancellationToken ct) => await db.QaRunCosts.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.CapturedAtUtc).Take(Math.Clamp(take, 1, 5000)).ToListAsync(ct);
    public Task AddMinimizationAsync(QaBugMinimizationRecord x, CancellationToken ct) => db.QaBugMinimizations.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaBugMinimizationRecord>> ListMinimizationsAsync(Guid p, int take, CancellationToken ct) => await db.QaBugMinimizations.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct);
    public Task<QaGovernanceProcessedRun?> GetProcessedRunAsync(Guid qaRunId, CancellationToken ct) => db.QaGovernanceProcessedRuns.SingleOrDefaultAsync(x => x.QaRunId == qaRunId, ct);
    public Task AddProcessedRunAsync(QaGovernanceProcessedRun x, CancellationToken ct) => db.QaGovernanceProcessedRuns.AddAsync(x, ct).AsTask();
    public Task<QaPolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => db.QaPolicies.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<QaPolicy>> ListPoliciesAsync(Guid p, CancellationToken ct) => await db.QaPolicies.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.Revision).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<QaReleaseGateOverride?> GetReleaseOverrideAsync(Guid id, CancellationToken ct) => db.QaReleaseGateOverrides.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task AddReleaseOverrideAsync(QaReleaseGateOverride x, CancellationToken ct) => db.QaReleaseGateOverrides.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaReleaseGateOverride>> ListReleaseOverridesAsync(Guid p, int take, CancellationToken ct) => await db.QaReleaseGateOverrides.Where(x => x.ProjectId == p).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct);
    public Task AddBackupVerificationAsync(QaBackupVerificationRecord x, CancellationToken ct) => db.QaBackupVerifications.AddAsync(x, ct).AsTask();
    public async Task<IReadOnlyList<QaBackupVerificationRecord>> ListBackupVerificationsAsync(Guid p, int take, CancellationToken ct) => await db.QaBackupVerifications.AsNoTracking().Where(x => x.ProjectId == p).OrderByDescending(x => x.VerifiedAtUtc).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct);
}
