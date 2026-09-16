// Purpose: Defines the IPolicyProfileRepository application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Abstractions.Persistence;

public interface IPolicyProfileRepository
{
    Task<PolicyProfile?> GetAsync(Guid id, CancellationToken ct);
    Task<PolicyProfile?> GetGlobalAsync(CancellationToken ct);
    Task<PolicyProfile?> GetProjectAsync(Guid projectId, CancellationToken ct);
    Task<PolicyProfile?> GetAgentAsync(string agentId, CancellationToken ct);
    Task AddAsync(PolicyProfile profile, CancellationToken ct);
}
