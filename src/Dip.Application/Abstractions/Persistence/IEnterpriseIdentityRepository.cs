// Purpose: Defines the IEnterpriseIdentityRepository application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Abstractions.Persistence;

public interface IEnterpriseIdentityRepository
{
    Task AddProfileAsync(IdentityProfile profile, CancellationToken ct);
    Task<IdentityProfile?> GetProfileAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<IdentityProfile>> ListProfilesAsync(Guid organizationId, CancellationToken ct);
    Task AddLinkAsync(ExternalIdentityLink link, CancellationToken ct);
    Task<ExternalIdentityLink?> FindLinkAsync(Guid organizationId, string provider, string externalId, CancellationToken ct);
    Task<IReadOnlyList<ExternalIdentityLink>> ListLinksAsync(Guid identityProfileId, CancellationToken ct);
    Task AddRoleAsync(OrganizationRoleBinding role, CancellationToken ct);
    Task<IReadOnlyList<OrganizationRoleBinding>> ListRolesAsync(Guid organizationId, string subjectId, CancellationToken ct);
    Task AddRetentionExecutionAsync(RetentionExecutionRecord record, CancellationToken ct);
}
