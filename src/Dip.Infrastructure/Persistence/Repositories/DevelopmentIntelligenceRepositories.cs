// Purpose: Implements D ev el op me nt In te ll ig en ce Re po si to ri es infrastructure integration for p er si st en ce.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class GameReplayRepository(DipDbContext db) : IGameReplayRepository
{
    public Task AddSessionAsync(GameReplaySession x, CancellationToken ct) => db.GameReplaySessions.AddAsync(x, ct).AsTask();
    public Task<GameReplaySession?> GetSessionAsync(Guid id, CancellationToken ct) => db.GameReplaySessions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<GameReplaySession?> GetSessionByKeyAsync(Guid projectId, string key, CancellationToken ct) => db.GameReplaySessions.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Key == key, ct);
    public async Task<IReadOnlyList<GameReplaySession>> ListSessionsAsync(Guid projectId, CancellationToken ct) => await db.GameReplaySessions.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.Key).ToListAsync(ct);
    public Task AddRunAsync(GameReplayRun x, CancellationToken ct) => db.GameReplayRuns.AddAsync(x, ct).AsTask();
    public Task<GameReplayRun?> GetRunAsync(Guid id, CancellationToken ct) => db.GameReplayRuns.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<GameReplayRun?> FindBaselineRunAsync(Guid projectId, Guid sessionId, string hardwareFingerprint, string excludingCommit, CancellationToken ct) => db.GameReplayRuns.AsNoTracking().Where(x => x.ProjectId == projectId && x.SessionId == sessionId && x.HardwareFingerprint == hardwareFingerprint && x.CommitSha != excludingCommit && x.Status == GameReplayRunStatus.Succeeded).OrderByDescending(x => x.FinishedAtUtc).FirstOrDefaultAsync(ct);
    public Task AddMetricsAsync(IEnumerable<GameReplayMetric> metrics, CancellationToken ct) => db.GameReplayMetrics.AddRangeAsync(metrics, ct);
    public async Task<IReadOnlyList<GameReplayMetric>> ListMetricsAsync(Guid runId, CancellationToken ct) => await db.GameReplayMetrics.AsNoTracking().Where(x => x.ReplayRunId == runId).OrderBy(x => x.Name).ThenBy(x => x.Aggregation).ToListAsync(ct);
}
public sealed class PerformanceBudgetRepository(DipDbContext db) : IPerformanceBudgetRepository
{
    public Task AddProfileAsync(PerformanceBudgetProfile x, CancellationToken ct) => db.PerformanceBudgetProfiles.AddAsync(x, ct).AsTask(); public Task<PerformanceBudgetProfile?> GetProfileAsync(Guid id, CancellationToken ct) => db.PerformanceBudgetProfiles.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PerformanceBudgetProfile?> GetProfileByKeyAsync(Guid? projectId, string key, CancellationToken ct) => db.PerformanceBudgetProfiles.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Key == key, ct);
    public async Task<IReadOnlyList<PerformanceBudgetProfile>> ListProfilesAsync(Guid? projectId, CancellationToken ct) => await db.PerformanceBudgetProfiles.AsNoTracking().Where(x => x.ProjectId == null || x.ProjectId == projectId).OrderBy(x => x.Platform).ThenBy(x => x.Key).ToListAsync(ct);
    public Task AddEvaluationAsync(PerformanceBudgetEvaluation x, CancellationToken ct) => db.PerformanceBudgetEvaluations.AddAsync(x, ct).AsTask(); public async Task<IReadOnlyList<PerformanceBudgetEvaluation>> ListEvaluationsAsync(Guid projectId, int take, CancellationToken ct) => await db.PerformanceBudgetEvaluations.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.EvaluatedAtUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
}
public sealed class HardwareBacktestRepository(DipDbContext db) : IHardwareBacktestRepository
{
    public Task AddAsync(HardwareBacktest x, CancellationToken ct) => db.HardwareBacktests.AddAsync(x, ct).AsTask(); public async Task<IReadOnlyList<HardwareBacktest>> ListAsync(Guid projectId, Guid? profileId, int take, CancellationToken ct) => await db.HardwareBacktests.AsNoTracking().Where(x => x.ProjectId == projectId && (!profileId.HasValue || x.HardwareProfileId == profileId.Value)).OrderByDescending(x => x.MeasuredAtUtc).Take(Math.Clamp(take, 1, 5000)).ToListAsync(ct);
}
public sealed class ChangeRiskRepository(DipDbContext db) : IChangeRiskRepository
{
    public Task AddAsync(ChangeRiskAssessment x, CancellationToken ct) => db.ChangeRiskAssessments.AddAsync(x, ct).AsTask();
     public Task<ChangeRiskAssessment?> GetLatestAsync(Guid projectId, string? commitSha, CancellationToken ct) => db.ChangeRiskAssessments.AsNoTracking().Where(x => x.ProjectId == projectId && (commitSha == null || x.CommitSha == commitSha)).OrderByDescending(x => x.AssessedAtUtc).FirstOrDefaultAsync(ct);
     public async Task<IReadOnlyList<ChangeRiskAssessment>> ListAsync(Guid projectId, int take, CancellationToken ct) => await db.ChangeRiskAssessments.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.AssessedAtUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
}
public sealed class RegressionTriageRepository(DipDbContext db) : IRegressionTriageRepository
{
    public Task AddAsync(RegressionTriage x, CancellationToken ct) => db.RegressionTriages.AddAsync(x, ct).AsTask();
     public Task<RegressionTriage?> GetLatestForIncidentAsync(Guid incidentId, CancellationToken ct) => db.RegressionTriages.AsNoTracking().Where(x => x.IncidentId == incidentId).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
     public async Task<IReadOnlyList<RegressionTriage>> ListAsync(Guid projectId, int take, CancellationToken ct) => await db.RegressionTriages.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
}
public sealed class ReleaseReadinessRepository(DipDbContext db) : IReleaseReadinessRepository
{
    public Task AddAsync(ReleaseReadinessSnapshot x, CancellationToken ct) => db.ReleaseReadinessSnapshots.AddAsync(x, ct).AsTask();
     public Task<ReleaseReadinessSnapshot?> GetLatestAsync(Guid projectId, CancellationToken ct) => db.ReleaseReadinessSnapshots.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.EvaluatedAtUtc).FirstOrDefaultAsync(ct);
     public async Task<IReadOnlyList<ReleaseReadinessSnapshot>> ListAsync(Guid projectId, int take, CancellationToken ct) => await db.ReleaseReadinessSnapshots.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.EvaluatedAtUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
}
public sealed class ExternalWorkItemRepository(DipDbContext db) : IExternalWorkItemRepository
{
    public Task AddAsync(ExternalWorkItemLink x, CancellationToken ct) => db.ExternalWorkItemLinks.AddAsync(x, ct).AsTask();
     public Task<ExternalWorkItemLink?> FindByIncidentAsync(Guid incidentId, ExternalWorkItemProvider provider, CancellationToken ct) => db.ExternalWorkItemLinks.AsNoTracking().FirstOrDefaultAsync(x => x.IncidentId == incidentId && x.Provider == provider, ct);
     public async Task<IReadOnlyList<ExternalWorkItemLink>> ListAsync(Guid projectId, CancellationToken ct) => await db.ExternalWorkItemLinks.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
}
public sealed class ReleaseEvidenceRepository(DipDbContext db) : IReleaseEvidenceRepository
{
    public Task AddAsync(ReleaseEvidenceBundle x, CancellationToken ct) => db.ReleaseEvidenceBundles.AddAsync(x, ct).AsTask();
     public Task<ReleaseEvidenceBundle?> GetLatestAsync(Guid projectId, CancellationToken ct) => db.ReleaseEvidenceBundles.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
     public async Task<IReadOnlyList<ReleaseEvidenceBundle>> ListAsync(Guid projectId, int take, CancellationToken ct) => await db.ReleaseEvidenceBundles.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
}
public sealed class RepositoryGraphRepository(DipDbContext db) : IRepositoryGraphRepository
{
    public Task AddNodeAsync(RepositoryNode x, CancellationToken ct) => db.RepositoryNodes.AddAsync(x, ct).AsTask();
     public Task<RepositoryNode?> GetNodeAsync(Guid id, CancellationToken ct) => db.RepositoryNodes.SingleOrDefaultAsync(x => x.Id == id, ct);
     public Task<RepositoryNode?> GetNodeByKeyAsync(Guid projectId, string key, CancellationToken ct) => db.RepositoryNodes.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Key == key, ct);
     public async Task<IReadOnlyList<RepositoryNode>> ListNodesAsync(Guid projectId, CancellationToken ct) => await db.RepositoryNodes.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.Key).ToListAsync(ct);
    public Task AddDependencyAsync(RepositoryDependency x, CancellationToken ct) => db.RepositoryDependencies.AddAsync(x, ct).AsTask();
     public async Task<IReadOnlyList<RepositoryDependency>> ListDependenciesAsync(Guid projectId, CancellationToken ct) => await db.RepositoryDependencies.AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(ct);
     public Task AddImpactAsync(CrossRepositoryImpact x, CancellationToken ct) => db.CrossRepositoryImpacts.AddAsync(x, ct).AsTask();
     public Task<CrossRepositoryImpact?> GetLatestImpactAsync(Guid projectId, string commitSha, CancellationToken ct) => db.CrossRepositoryImpacts.AsNoTracking().Where(x => x.ProjectId == projectId && x.CommitSha == commitSha).OrderByDescending(x => x.AnalyzedAtUtc).FirstOrDefaultAsync(ct);
}
