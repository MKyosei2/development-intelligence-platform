// Purpose: Defines the I En te rp ri se Fl ee tR ep os it or ie s application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Abstractions.Persistence;

public interface IEnterpriseFleetRepository
{
    Task<OrganizationTenant?> GetOrganizationAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<OrganizationTenant>> ListOrganizationsAsync(CancellationToken ct);
    Task AddOrganizationAsync(OrganizationTenant item,CancellationToken ct);
    Task<ProjectTenantBinding?> GetProjectBindingAsync(Guid projectId,CancellationToken ct);
    Task AddProjectBindingAsync(ProjectTenantBinding item,CancellationToken ct);
    Task<AgentEnrollmentToken?> FindEnrollmentByHashAsync(string hash,CancellationToken ct);
    Task AddEnrollmentAsync(AgentEnrollmentToken item,CancellationToken ct);
    Task<AgentRegistration?> FindAgentAsync(string agentId,CancellationToken ct);
    Task<IReadOnlyList<AgentRegistration>> ListAgentsAsync(Guid organizationId,CancellationToken ct);
    Task AddAgentAsync(AgentRegistration item,CancellationToken ct);
    Task AddReleaseAsync(AgentRelease item,CancellationToken ct);
    Task<AgentRelease?> GetReleaseAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<AgentRelease>> ListReleasesAsync(Guid organizationId,CancellationToken ct);
}

public interface IEnterpriseEvidenceRepository
{
    Task<EvidenceIntegrityReceipt?> LatestReceiptAsync(Guid organizationId,Guid projectId,CancellationToken ct);
    Task AddReceiptAsync(EvidenceIntegrityReceipt item,CancellationToken ct);
    Task<IReadOnlyList<EvidenceIntegrityReceipt>> ListReceiptsAsync(Guid projectId,int take,CancellationToken ct);
    Task<DistributedIngestionEvent?> FindByIdempotencyKeyAsync(Guid organizationId,string key,CancellationToken ct);
    Task AddIngestionAsync(DistributedIngestionEvent item,CancellationToken ct);
    Task<IReadOnlyList<DistributedIngestionEvent>> ListPendingAsync(Guid organizationId,int take,CancellationToken ct);
    Task<int> DeleteCompletedBeforeAsync(Guid organizationId,DateTimeOffset cutoff,CancellationToken ct);
}

public interface IEnterpriseIdentityRepository
{
    Task AddProfileAsync(IdentityProfile profile,CancellationToken ct);
    Task<IdentityProfile?> GetProfileAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<IdentityProfile>> ListProfilesAsync(Guid organizationId,CancellationToken ct);
    Task AddLinkAsync(ExternalIdentityLink link,CancellationToken ct);
    Task<ExternalIdentityLink?> FindLinkAsync(Guid organizationId,string provider,string externalId,CancellationToken ct);
    Task<IReadOnlyList<ExternalIdentityLink>> ListLinksAsync(Guid identityProfileId,CancellationToken ct);
    Task AddRoleAsync(OrganizationRoleBinding role,CancellationToken ct);
    Task<IReadOnlyList<OrganizationRoleBinding>> ListRolesAsync(Guid organizationId,string subjectId,CancellationToken ct);
    Task AddRetentionExecutionAsync(RetentionExecutionRecord record,CancellationToken ct);
}
