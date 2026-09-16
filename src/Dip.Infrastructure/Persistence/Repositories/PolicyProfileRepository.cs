// Purpose: Implements persistence operations for PolicyProfile.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class PolicyProfileRepository(DipDbContext db):IPolicyProfileRepository
{
    public Task<PolicyProfile?> GetAsync(Guid id,CancellationToken ct)=>db.PolicyProfiles.FirstOrDefaultAsync(item=>item.Id==id,ct);
    public Task<PolicyProfile?> GetGlobalAsync(CancellationToken ct)=>db.PolicyProfiles.Where(item=>item.ScopeType==PolicyScopeType.Global).OrderByDescending(item=>item.UpdatedAtUtc).FirstOrDefaultAsync(ct);
    public Task<PolicyProfile?> GetProjectAsync(Guid projectId,CancellationToken ct)=>db.PolicyProfiles.Where(item=>item.ScopeType==PolicyScopeType.Project&&item.ProjectId==projectId).OrderByDescending(item=>item.UpdatedAtUtc).FirstOrDefaultAsync(ct);
    public Task<PolicyProfile?> GetAgentAsync(string agentId,CancellationToken ct)=>db.PolicyProfiles.Where(item=>item.ScopeType==PolicyScopeType.Agent&&item.AgentId==agentId).OrderByDescending(item=>item.UpdatedAtUtc).FirstOrDefaultAsync(ct);
    public Task AddAsync(PolicyProfile profile,CancellationToken ct)=>db.PolicyProfiles.AddAsync(profile,ct).AsTask();
}
