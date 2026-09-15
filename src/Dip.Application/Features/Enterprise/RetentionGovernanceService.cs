using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Policies;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Features.Enterprise;

public sealed record RetentionPreview(Guid OrganizationId,Guid? ProjectId,DateTimeOffset CompletedIngestionCutoff,int MetadataRetentionDays,int ArtifactRetentionDays,int VideoRetentionDays);
public sealed class RetentionGovernanceService(IEnterpriseEvidenceRepository evidence,IEnterpriseIdentityRepository identity,PolicyResolver policies,IUnitOfWork uow,ISystemClock clock)
{
    public async Task<RetentionPreview> PreviewAsync(Guid organizationId,Guid? projectId,string? agentId,CancellationToken ct)
    {var policy=await policies.ResolveAsync(projectId,agentId,ct);return new(organizationId,projectId,clock.UtcNow.AddDays(-policy.Retention.MetadataRetentionDays),policy.Retention.MetadataRetentionDays,policy.Retention.ArtifactRetentionDays,policy.Retention.VideoRetentionDays);}
    public async Task<int> ExecuteCompletedQueueRetentionAsync(Guid organizationId,Guid? projectId,string actor,CancellationToken ct)
    {
        var preview=await PreviewAsync(organizationId,projectId,null,ct);var count=await evidence.DeleteCompletedBeforeAsync(organizationId,preview.CompletedIngestionCutoff,ct);await identity.AddRetentionExecutionAsync(new RetentionExecutionRecord(Guid.NewGuid(),organizationId,projectId,"distributed-ingestion",RetentionAction.ExpireMetadata,count,JsonSerializer.Serialize(new{preview.MetadataRetentionDays,actor}),clock.UtcNow),ct);await uow.SaveChangesAsync(ct);return count;
    }
}
