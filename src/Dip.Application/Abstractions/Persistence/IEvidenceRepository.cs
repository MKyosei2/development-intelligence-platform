// Purpose: Defines the IEvidenceRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IEvidenceRepository
{
    Task AddAsync(EvidenceRecord evidence, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence, CancellationToken ct);
    Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(Guid projectId, DateTimeOffset? observedSince, CancellationToken ct);
    async Task<IReadOnlyList<EvidenceRecord>> ListByPipelineRunsAsync(Guid projectId, IReadOnlyCollection<Guid> pipelineRunIds, CancellationToken ct)
    {
        if (pipelineRunIds.Count == 0) return Array.Empty<EvidenceRecord>();
        var ids = pipelineRunIds.ToHashSet();
        return (await ListForProjectAsync(projectId, null, ct))
            .Where(x => x.PipelineRunId.HasValue && ids.Contains(x.PipelineRunId.Value))
            .ToArray();
    }
}
