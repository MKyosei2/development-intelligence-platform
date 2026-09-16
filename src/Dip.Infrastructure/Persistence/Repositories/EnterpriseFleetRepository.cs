// Purpose: Implements persistence operations for EnterpriseFleet.
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
