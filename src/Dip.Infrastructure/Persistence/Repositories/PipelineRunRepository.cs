using Dip.Application.Abstractions.Persistence;using Dip.Domain.Entities;using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class PipelineRunRepository(DipDbContext db):IPipelineRunRepository
{
 public Task<PipelineRun?> GetByIdAsync(Guid id,CancellationToken ct)=>db.PipelineRuns.SingleOrDefaultAsync(x=>x.Id==id,ct);
 public Task<PipelineRun?> FindByExternalRunAsync(Guid projectId,string externalRunId,string collectorType,int attemptNumber,CancellationToken ct)=>db.PipelineRuns.SingleOrDefaultAsync(x=>x.ProjectId==projectId&&x.ExternalRunId==externalRunId&&x.CollectorType==collectorType&&x.AttemptNumber==attemptNumber,ct);
 public Task AddAsync(PipelineRun run,CancellationToken ct)=>db.PipelineRuns.AddAsync(run,ct).AsTask();
}
