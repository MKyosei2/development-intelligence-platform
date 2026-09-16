// Purpose: Implements persistence operations for EnterpriseIdentity.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;

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
