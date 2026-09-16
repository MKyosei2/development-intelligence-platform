// Purpose: Defines the I Ev id en ce In te gr at io nR ep os it or ie s application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Abstractions.Persistence;

public interface IEvidenceV2Repository
{
    Task<EvidenceRecord?> FindBySourceEventAsync(
        Guid projectId,
        EvidenceSourceType sourceType,
        string sourceInstanceId,
        string sourceEventId,
        CancellationToken ct);

    Task<EvidenceRecord?> GetAsync(
        Guid projectId,
        Guid evidenceId,
        CancellationToken ct);

    Task AddAsync(EvidenceRecord evidence, CancellationToken ct);
}

public interface IIntegrationConnectionRepository
{
    Task<IntegrationConnection?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<IntegrationConnection>> ListAsync(CancellationToken ct);
    Task<IntegrationConnection?> GetDefaultForProviderAsync(
        string provider,
        CancellationToken ct);
    Task AddAsync(IntegrationConnection connection, CancellationToken ct);
    void Remove(IntegrationConnection connection);
}

public interface IProjectSourceBindingRepository
{
    Task<ProjectSourceBinding?> GetAsync(Guid id, CancellationToken ct);
    Task<ProjectSourceBinding?> FindAsync(
        Guid projectId,
        Guid connectionId,
        string externalResourceId,
        CancellationToken ct);
    Task<IReadOnlyList<ProjectSourceBinding>> ListByProjectAsync(
        Guid projectId,
        CancellationToken ct);
    Task AddAsync(ProjectSourceBinding binding, CancellationToken ct);
}

public interface IPolicyProfileRepository
{
    Task<PolicyProfile?> GetAsync(Guid id, CancellationToken ct);
    Task<PolicyProfile?> GetGlobalAsync(CancellationToken ct);
    Task<PolicyProfile?> GetProjectAsync(Guid projectId, CancellationToken ct);
    Task<PolicyProfile?> GetAgentAsync(string agentId, CancellationToken ct);
    Task AddAsync(PolicyProfile profile, CancellationToken ct);
}
