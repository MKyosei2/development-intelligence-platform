// Purpose: Defines the IExternalWorkItemRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IExternalWorkItemRepository
{
    Task AddAsync(ExternalWorkItemLink link,CancellationToken ct);
    Task<ExternalWorkItemLink?> FindByIncidentAsync(Guid incidentId,Dip.Domain.Enums.ExternalWorkItemProvider provider,CancellationToken ct);
    Task<IReadOnlyList<ExternalWorkItemLink>> ListAsync(Guid projectId,CancellationToken ct);
}
