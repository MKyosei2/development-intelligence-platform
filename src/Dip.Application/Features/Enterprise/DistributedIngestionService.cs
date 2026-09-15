using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Evidence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Features.Enterprise;

public sealed record QueueEvidenceCommand(Guid OrganizationId,Guid ProjectId,string IdempotencyKey,long SourceSequence,IngestEvidenceCommand Evidence);
public sealed record QueueEvidenceResult(Guid QueueId,bool Created,DistributedIngestionState State);
public sealed class DistributedIngestionService(IEnterpriseEvidenceRepository repo,EvidenceIngestionService evidence,IUnitOfWork uow,ISystemClock clock)
{
    public async Task<QueueEvidenceResult> EnqueueAsync(QueueEvidenceCommand c,CancellationToken ct)
    {
        var existing=await repo.FindByIdempotencyKeyAsync(c.OrganizationId,c.IdempotencyKey,ct);if(existing is not null)return new(existing.Id,false,existing.State);
        var json=JsonSerializer.Serialize(c.Evidence);var row=new DistributedIngestionEvent(Guid.NewGuid(),c.OrganizationId,c.ProjectId,c.IdempotencyKey,c.SourceSequence,json,c.Evidence.ObservedAtUtc,clock.UtcNow);await repo.AddIngestionAsync(row,ct);await uow.SaveChangesAsync(ct);return new(row.Id,true,row.State);
    }
    public async Task<int> DrainAsync(Guid organizationId,int take,CancellationToken ct)
    {
        var rows=await repo.ListPendingAsync(organizationId,Math.Clamp(take,1,500),ct);var completed=0;
        foreach(var row in rows.OrderBy(x=>x.SourceSequence).ThenBy(x=>x.ObservedAtUtc))
        {
            row.Start();await uow.SaveChangesAsync(ct);
            try{var command=JsonSerializer.Deserialize<IngestEvidenceCommand>(row.EnvelopeJson)??throw new InvalidOperationException("Invalid queued evidence envelope.");await evidence.IngestAsync(command,ct);row.Complete();completed++;}
            catch(Exception ex) when(ex is not OperationCanceledException){row.Fail(ex.Message);}
            await uow.SaveChangesAsync(ct);
        }
        return completed;
    }
}
