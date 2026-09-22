// Purpose: Defines the IIncidentRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IIncidentRepository
{
    Task<Incident?> FindByFingerprintAsync(Guid projectId, string fingerprint, CancellationToken ct);
    Task<Incident?> GetAsync(Guid id, CancellationToken ct);
    Task<Incident?> FindLatestOpenForSubjectAsync(Guid projectId, string subjectKey, CancellationToken ct);
    Task AddAsync(Incident incident, CancellationToken ct);
    Task AddOccurrenceAsync(IncidentOccurrence occurrence, CancellationToken ct);
    Task AddEventAsync(IncidentEvent incidentEvent, CancellationToken ct);
    Task<IReadOnlyList<Incident>> ListRecentAsync(Guid projectId, int take, CancellationToken ct);
    Task<IReadOnlyList<IncidentEvent>> ListEventsAsync(Guid incidentId, CancellationToken ct);
    Task<IReadOnlyList<IncidentOccurrence>> ListOccurrencesAsync(Guid incidentId, int take, CancellationToken ct);
}
