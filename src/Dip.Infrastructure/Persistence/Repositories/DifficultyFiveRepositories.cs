using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class EnterpriseFleetRepository(DipDbContext db):IEnterpriseFleetRepository
{
    public Task<OrganizationTenant?> GetOrganizationAsync(Guid id,CancellationToken ct)=>db.OrganizationTenants.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<OrganizationTenant>> ListOrganizationsAsync(CancellationToken ct)=>await db.OrganizationTenants.AsNoTracking().OrderBy(x=>x.Name).ToArrayAsync(ct);
    public Task AddOrganizationAsync(OrganizationTenant item,CancellationToken ct)=>db.OrganizationTenants.AddAsync(item,ct).AsTask();
    public Task<ProjectTenantBinding?> GetProjectBindingAsync(Guid projectId,CancellationToken ct)=>db.ProjectTenantBindings.AsNoTracking().SingleOrDefaultAsync(x=>x.ProjectId==projectId,ct);
    public Task AddProjectBindingAsync(ProjectTenantBinding item,CancellationToken ct)=>db.ProjectTenantBindings.AddAsync(item,ct).AsTask();
    public Task<AgentEnrollmentToken?> FindEnrollmentByHashAsync(string hash,CancellationToken ct)=>db.AgentEnrollmentTokens.SingleOrDefaultAsync(x=>x.TokenHash==hash,ct);
    public Task AddEnrollmentAsync(AgentEnrollmentToken item,CancellationToken ct)=>db.AgentEnrollmentTokens.AddAsync(item,ct).AsTask();
    public Task<AgentRegistration?> FindAgentAsync(string agentId,CancellationToken ct)=>db.AgentRegistrations.SingleOrDefaultAsync(x=>x.AgentId==agentId,ct);
    public async Task<IReadOnlyList<AgentRegistration>> ListAgentsAsync(Guid organizationId,CancellationToken ct)=>await db.AgentRegistrations.AsNoTracking().Where(x=>x.OrganizationId==organizationId).OrderBy(x=>x.AgentId).ToArrayAsync(ct);
    public Task AddAgentAsync(AgentRegistration item,CancellationToken ct)=>db.AgentRegistrations.AddAsync(item,ct).AsTask();
    public Task AddReleaseAsync(AgentRelease item,CancellationToken ct)=>db.AgentReleases.AddAsync(item,ct).AsTask();
    public Task<AgentRelease?> GetReleaseAsync(Guid id,CancellationToken ct)=>db.AgentReleases.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<AgentRelease>> ListReleasesAsync(Guid organizationId,CancellationToken ct)=>await db.AgentReleases.AsNoTracking().Where(x=>x.OrganizationId==organizationId).OrderByDescending(x=>x.CreatedAtUtc).ToArrayAsync(ct);
}

public sealed class EnterpriseEvidenceRepository(DipDbContext db):IEnterpriseEvidenceRepository
{
    public Task<EvidenceIntegrityReceipt?> LatestReceiptAsync(Guid organizationId,Guid projectId,CancellationToken ct)=>db.EvidenceIntegrityReceipts.AsNoTracking().Where(x=>x.OrganizationId==organizationId&&x.ProjectId==projectId).OrderByDescending(x=>x.IngestedAtUtc).FirstOrDefaultAsync(ct);
    public Task AddReceiptAsync(EvidenceIntegrityReceipt item,CancellationToken ct)=>db.EvidenceIntegrityReceipts.AddAsync(item,ct).AsTask();
    public async Task<IReadOnlyList<EvidenceIntegrityReceipt>> ListReceiptsAsync(Guid projectId,int take,CancellationToken ct)=>await db.EvidenceIntegrityReceipts.AsNoTracking().Where(x=>x.ProjectId==projectId).OrderByDescending(x=>x.IngestedAtUtc).Take(take).ToArrayAsync(ct);
    public Task<DistributedIngestionEvent?> FindByIdempotencyKeyAsync(Guid organizationId,string key,CancellationToken ct)=>db.DistributedIngestionEvents.SingleOrDefaultAsync(x=>x.OrganizationId==organizationId&&x.IdempotencyKey==key,ct);
    public Task AddIngestionAsync(DistributedIngestionEvent item,CancellationToken ct)=>db.DistributedIngestionEvents.AddAsync(item,ct).AsTask();
    public async Task<IReadOnlyList<DistributedIngestionEvent>> ListPendingAsync(Guid organizationId,int take,CancellationToken ct)=>await db.DistributedIngestionEvents.Where(x=>x.OrganizationId==organizationId&&(x.State==DistributedIngestionState.Pending||x.State==DistributedIngestionState.Failed)).OrderBy(x=>x.SourceSequence).Take(take).ToArrayAsync(ct);
    public Task<int> DeleteCompletedBeforeAsync(Guid organizationId,DateTimeOffset cutoff,CancellationToken ct)=>db.DistributedIngestionEvents.Where(x=>x.OrganizationId==organizationId&&x.State==DistributedIngestionState.Completed&&x.ReceivedAtUtc<cutoff).ExecuteDeleteAsync(ct);
}

public sealed class EnterpriseIdentityRepository(DipDbContext db):IEnterpriseIdentityRepository
{
    public Task AddProfileAsync(IdentityProfile profile,CancellationToken ct)=>db.IdentityProfiles.AddAsync(profile,ct).AsTask();
    public Task<IdentityProfile?> GetProfileAsync(Guid id,CancellationToken ct)=>db.IdentityProfiles.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<IdentityProfile>> ListProfilesAsync(Guid organizationId,CancellationToken ct)=>await db.IdentityProfiles.AsNoTracking().Where(x=>x.OrganizationId==organizationId).OrderBy(x=>x.DisplayName).ToArrayAsync(ct);
    public Task AddLinkAsync(ExternalIdentityLink link,CancellationToken ct)=>db.ExternalIdentityLinks.AddAsync(link,ct).AsTask();
    public async Task<ExternalIdentityLink?> FindLinkAsync(Guid organizationId,string provider,string externalId,CancellationToken ct)=>await (from l in db.ExternalIdentityLinks.AsNoTracking() join p in db.IdentityProfiles.AsNoTracking() on l.IdentityProfileId equals p.Id where p.OrganizationId==organizationId&&l.Provider==provider&&l.ExternalId==externalId select l).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<ExternalIdentityLink>> ListLinksAsync(Guid identityProfileId,CancellationToken ct)=>await db.ExternalIdentityLinks.AsNoTracking().Where(x=>x.IdentityProfileId==identityProfileId).ToArrayAsync(ct);
    public Task AddRoleAsync(OrganizationRoleBinding role,CancellationToken ct)=>db.OrganizationRoleBindings.AddAsync(role,ct).AsTask();
    public async Task<IReadOnlyList<OrganizationRoleBinding>> ListRolesAsync(Guid organizationId,string subjectId,CancellationToken ct)=>await db.OrganizationRoleBindings.AsNoTracking().Where(x=>x.OrganizationId==organizationId&&x.SubjectId==subjectId).ToArrayAsync(ct);
    public Task AddRetentionExecutionAsync(RetentionExecutionRecord record,CancellationToken ct)=>db.RetentionExecutionRecords.AddAsync(record,ct).AsTask();
}
