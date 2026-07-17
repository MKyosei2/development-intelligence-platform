using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class PipelineRunRepository : IPipelineRunRepository
{
    private readonly DipDbContext _db;

    public PipelineRunRepository(DipDbContext db) => _db = db;

    public Task<PipelineRun?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        _db.PipelineRuns.SingleOrDefaultAsync(
            x => x.Id == id,
            cancellationToken);

    public Task<PipelineRun?> FindByExternalRunAsync(
        Guid projectId,
        string externalRunId,
        string collectorType,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        var normalizedExternalRunId = externalRunId.Trim();
        var normalizedCollectorType = collectorType.Trim();

        return _db.PipelineRuns.SingleOrDefaultAsync(
            x => x.ProjectId == projectId
                 && x.ExternalRunId == normalizedExternalRunId
                 && x.CollectorType == normalizedCollectorType
                 && x.AttemptNumber == attemptNumber,
            cancellationToken);
    }

    public Task AddAsync(
        PipelineRun run,
        CancellationToken cancellationToken) =>
        _db.PipelineRuns.AddAsync(run, cancellationToken).AsTask();
}
