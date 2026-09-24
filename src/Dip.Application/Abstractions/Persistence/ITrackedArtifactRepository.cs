// Purpose: Defines persistence operations required by I Tr ac ke dA rt if ac t in p er si st en ce.
using Dip.Domain.Entities;

namespace Dip.Application.Abstractions.Persistence;

public interface ITrackedArtifactRepository
{
    Task<TrackedArtifact?> FindByPathKeyAsync(Guid projectId, string pathKey, CancellationToken ct);
    Task<TrackedArtifact?> GetAsync(Guid artifactId, CancellationToken ct);
    Task<IReadOnlyList<TrackedArtifact>> ListByProjectAsync(Guid projectId, int take, CancellationToken ct);
    Task<TrackedArtifactVersion?> FindVersionBySourceEventAsync(Guid projectId, string sourceEventId, CancellationToken ct);
    Task<IReadOnlyList<TrackedArtifactVersion>> ListVersionsAsync(Guid artifactId, int take, CancellationToken ct);
    Task<TrackedArtifactVersion?> GetLatestVersionAsync(Guid artifactId, CancellationToken ct);
    Task AddArtifactAsync(TrackedArtifact artifact, CancellationToken ct);
    Task AddVersionAsync(TrackedArtifactVersion version, CancellationToken ct);
    Task AddPreviewAsync(ArtifactPreview preview, CancellationToken ct);
    Task AddDependencyAsync(ArtifactDependency dependency, CancellationToken ct);
}
