using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class EvidenceV2Repository(DipDbContext db) : IEvidenceV2Repository
{
    public Task<EvidenceRecord?> FindBySourceEventAsync(
        Guid projectId,
        EvidenceSourceType sourceType,
        string sourceInstanceId,
        string sourceEventId,
        CancellationToken ct)
        => db.EvidenceRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.ProjectId == projectId
                        && item.SourceType == sourceType
                        && item.SourceInstanceId == sourceInstanceId
                        && item.SourceEventId == sourceEventId,
                ct);

    public Task<EvidenceRecord?> GetAsync(
        Guid projectId,
        Guid evidenceId,
        CancellationToken ct)
        => db.EvidenceRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.ProjectId == projectId && item.Id == evidenceId,
                ct);

    public Task AddAsync(EvidenceRecord evidence, CancellationToken ct)
        => db.EvidenceRecords.AddAsync(evidence, ct).AsTask();
}

public sealed class IntegrationConnectionRepository(DipDbContext db)
    : IIntegrationConnectionRepository
{
    public Task<IntegrationConnection?> GetAsync(Guid id, CancellationToken ct)
        => db.IntegrationConnections.FirstOrDefaultAsync(item => item.Id == id, ct);

    public async Task<IReadOnlyList<IntegrationConnection>> ListAsync(CancellationToken ct)
        => await db.IntegrationConnections
            .AsNoTracking()
            .OrderBy(item => item.Provider)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(ct);

    public Task<IntegrationConnection?> GetDefaultForProviderAsync(
        string provider,
        CancellationToken ct)
        => db.IntegrationConnections
            .Where(item => item.Enabled && item.Provider == provider)
            .OrderByDescending(item => item.LastTestedAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task AddAsync(IntegrationConnection connection, CancellationToken ct)
        => db.IntegrationConnections.AddAsync(connection, ct).AsTask();

    public void Remove(IntegrationConnection connection)
        => db.IntegrationConnections.Remove(connection);
}

public sealed class ProjectSourceBindingRepository(DipDbContext db)
    : IProjectSourceBindingRepository
{
    public Task<ProjectSourceBinding?> GetAsync(Guid id, CancellationToken ct)
        => db.ProjectSourceBindings.FirstOrDefaultAsync(item => item.Id == id, ct);

    public Task<ProjectSourceBinding?> FindAsync(
        Guid projectId,
        Guid connectionId,
        string externalResourceId,
        CancellationToken ct)
        => db.ProjectSourceBindings.FirstOrDefaultAsync(
            item => item.ProjectId == projectId
                    && item.ConnectionId == connectionId
                    && item.ExternalResourceId == externalResourceId,
            ct);

    public async Task<IReadOnlyList<ProjectSourceBinding>> ListByProjectAsync(
        Guid projectId,
        CancellationToken ct)
        => await db.ProjectSourceBindings
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.ResourceType)
            .ThenBy(item => item.ExternalResourceName)
            .ToListAsync(ct);

    public Task AddAsync(ProjectSourceBinding binding, CancellationToken ct)
        => db.ProjectSourceBindings.AddAsync(binding, ct).AsTask();
}

public sealed class PolicyProfileRepository(DipDbContext db)
    : IPolicyProfileRepository
{
    public Task<PolicyProfile?> GetAsync(Guid id, CancellationToken ct)
        => db.PolicyProfiles.FirstOrDefaultAsync(item => item.Id == id, ct);

    public Task<PolicyProfile?> GetGlobalAsync(CancellationToken ct)
        => db.PolicyProfiles
            .Where(item => item.ScopeType == PolicyScopeType.Global)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<PolicyProfile?> GetProjectAsync(Guid projectId, CancellationToken ct)
        => db.PolicyProfiles
            .Where(item => item.ScopeType == PolicyScopeType.Project
                           && item.ProjectId == projectId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<PolicyProfile?> GetAgentAsync(string agentId, CancellationToken ct)
        => db.PolicyProfiles
            .Where(item => item.ScopeType == PolicyScopeType.Agent
                           && item.AgentId == agentId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task AddAsync(PolicyProfile profile, CancellationToken ct)
        => db.PolicyProfiles.AddAsync(profile, ct).AsTask();
}
