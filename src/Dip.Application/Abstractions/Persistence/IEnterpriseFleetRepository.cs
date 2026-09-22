// Purpose: Defines the IEnterpriseFleetRepository application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Abstractions.Persistence;

public interface IEnterpriseFleetRepository
{
    Task<OrganizationTenant?> GetOrganizationAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<OrganizationTenant>> ListOrganizationsAsync(CancellationToken ct);
    Task AddOrganizationAsync(OrganizationTenant item, CancellationToken ct);
    Task<ProjectTenantBinding?> GetProjectBindingAsync(Guid projectId, CancellationToken ct);
    Task AddProjectBindingAsync(ProjectTenantBinding item, CancellationToken ct);
    Task<AgentEnrollmentToken?> FindEnrollmentByHashAsync(string hash, CancellationToken ct);
    Task AddEnrollmentAsync(AgentEnrollmentToken item, CancellationToken ct);
    Task<AgentRegistration?> FindAgentAsync(string agentId, CancellationToken ct);
    Task<IReadOnlyList<AgentRegistration>> ListAgentsAsync(Guid organizationId, CancellationToken ct);
    Task AddAgentAsync(AgentRegistration item, CancellationToken ct);
    Task AddReleaseAsync(AgentRelease item, CancellationToken ct);
    Task<AgentRelease?> GetReleaseAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AgentRelease>> ListReleasesAsync(Guid organizationId, CancellationToken ct);
}
