// Purpose: Implements Quality Issue Repository infrastructure responsibilities for persistence.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class QualityIssueRepository(DipDbContext db) : IQualityIssueRepository
{
    public Task<QualityIssue?> GetAsync(Guid id, CancellationToken ct) =>
        db.QualityIssues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<QualityIssue?> FindOpenByFingerprintAsync(Guid projectId, string fingerprint, CancellationToken ct) =>
        db.QualityIssues.FirstOrDefaultAsync(
            x => x.ProjectId == projectId
                 && x.Fingerprint == fingerprint
                 && (x.Status == IssueStatus.Open || x.Status == IssueStatus.Reopened),
            ct);

    public async Task<IReadOnlyList<QualityIssue>> ListByProjectAsync(Guid projectId, int take, CancellationToken ct) =>
        await db.QualityIssues
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ThenByDescending(x => x.Severity)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);


    public async Task<IReadOnlyList<QualityIssue>> ListByPipelineRunsAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> pipelineRunIds,
        CancellationToken ct)
    {
        if (pipelineRunIds.Count == 0)
            return Array.Empty<QualityIssue>();
        var ids = pipelineRunIds.Distinct().ToArray();
        return await db.QualityIssues
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId
                        && (ids.Contains(x.PipelineRunId)
                            || ids.Contains(x.FirstSeenPipelineRunId)
                            || ids.Contains(x.LastSeenPipelineRunId)))
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(ct);
    }

    public Task AddAsync(QualityIssue issue, CancellationToken ct) =>
        db.QualityIssues.AddAsync(issue, ct).AsTask();
}
