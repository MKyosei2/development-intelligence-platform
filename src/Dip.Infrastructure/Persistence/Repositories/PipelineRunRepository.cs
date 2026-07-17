using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class PipelineRunRepository:IPipelineRunRepository
{
    private readonly DipDbContext _db;public PipelineRunRepository(DipDbContext db)=>_db=db;
    public Task<PipelineRun?> GetByIdAsync(Guid id,CancellationToken ct)=>_db.PipelineRuns.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<PipelineRun?> FindByExternalRunAsync(Guid projectId,string externalRunId,string collectorType,int attempt,CancellationToken ct)=>
        _db.PipelineRuns.SingleOrDefaultAsync(x=>x.ProjectId==projectId&&x.ExternalRunId==externalRunId&&x.CollectorType==collectorType&&x.AttemptNumber==attempt,ct);
    public Task AddAsync(PipelineRun run,CancellationToken ct)=>_db.PipelineRuns.AddAsync(run,ct).AsTask();
}
