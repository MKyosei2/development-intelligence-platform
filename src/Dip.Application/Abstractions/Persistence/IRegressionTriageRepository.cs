// Purpose: Defines the IRegressionTriageRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IRegressionTriageRepository
{
    Task AddAsync(RegressionTriage triage, CancellationToken ct);
    Task<RegressionTriage?> GetLatestForIncidentAsync(Guid incidentId, CancellationToken ct);
    Task<IReadOnlyList<RegressionTriage>> ListAsync(Guid projectId, int take, CancellationToken ct);
}
