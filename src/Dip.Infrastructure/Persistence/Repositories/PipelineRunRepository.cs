// Purpose: Persists and queries P ip el in eR un Re po si to ry data.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class PipelineRunRepository(DipDbContext db) : IPipelineRunRepository
{
    public Task<PipelineRun?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.PipelineRuns.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<PipelineRun?> FindByExternalRunAsync(
        Guid projectId,
        string externalRunId,
        string collectorType,
        int attemptNumber,
        CancellationToken ct)
        => db.PipelineRuns.SingleOrDefaultAsync(
            x => x.ProjectId == projectId
                 && x.ExternalRunId == externalRunId
                 && x.CollectorType == collectorType
                 && x.AttemptNumber == attemptNumber,
            ct);

    public async Task<IReadOnlyList<PipelineRun>> ListByProjectAsync(
        Guid projectId,
        int take,
        CancellationToken ct)
        => await db.PipelineRuns
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.FinishedAtUtc ?? x.StartedAtUtc ?? x.QueuedAtUtc)
            .ThenByDescending(x => x.QueuedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PipelineRun>> ListByCommitShasAsync(
        Guid projectId,
        IReadOnlyCollection<string> commitShas,
        CancellationToken ct)
    {
        if (commitShas.Count == 0)
            return Array.Empty<PipelineRun>();

        var revisions = commitShas
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => new CommitSha(value.Trim()))
            .Distinct()
            .ToArray();
        if (revisions.Length == 0)
            return Array.Empty<PipelineRun>();

        return await db.PipelineRuns
            .AsNoTracking()
            .Where(run => run.ProjectId == projectId && revisions.Contains(run.CommitSha))
            .OrderByDescending(run => run.FinishedAtUtc ?? run.StartedAtUtc ?? run.QueuedAtUtc)
            .ThenByDescending(run => run.QueuedAtUtc)
            .ToListAsync(ct);
    }

    public Task AddAsync(PipelineRun run, CancellationToken ct)
        => db.PipelineRuns.AddAsync(run, ct).AsTask();
}
