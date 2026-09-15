using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class TrackedArtifactRepository(DipDbContext db) : ITrackedArtifactRepository
{
    public Task<TrackedArtifact?> FindByPathKeyAsync(Guid projectId, string pathKey, CancellationToken ct)
        => db.TrackedArtifacts.FirstOrDefaultAsync(
            item => item.ProjectId == projectId && item.PathKey == pathKey,
            ct);

    public Task<TrackedArtifact?> GetAsync(Guid artifactId, CancellationToken ct)
        => db.TrackedArtifacts.FirstOrDefaultAsync(item => item.Id == artifactId, ct);

    public async Task<IReadOnlyList<TrackedArtifact>> ListByProjectAsync(Guid projectId, int take, CancellationToken ct)
        => await db.TrackedArtifacts.AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.DisplayPath)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(ct);

    public Task<TrackedArtifactVersion?> FindVersionBySourceEventAsync(Guid projectId, string sourceEventId, CancellationToken ct)
        => (from version in db.TrackedArtifactVersions
            join artifact in db.TrackedArtifacts on version.ArtifactId equals artifact.Id
            where artifact.ProjectId == projectId && version.SourceEventId == sourceEventId
            select version).AsNoTracking().FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TrackedArtifactVersion>> ListVersionsAsync(Guid artifactId, int take, CancellationToken ct)
        => await db.TrackedArtifactVersions.AsNoTracking()
            .Where(item => item.ArtifactId == artifactId)
            .OrderByDescending(item => item.VersionNumber)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

    public Task<TrackedArtifactVersion?> GetLatestVersionAsync(Guid artifactId, CancellationToken ct)
        => db.TrackedArtifactVersions.AsNoTracking()
            .Where(item => item.ArtifactId == artifactId)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(ct);

    public Task AddArtifactAsync(TrackedArtifact artifact, CancellationToken ct)
        => db.TrackedArtifacts.AddAsync(artifact, ct).AsTask();

    public Task AddVersionAsync(TrackedArtifactVersion version, CancellationToken ct)
        => db.TrackedArtifactVersions.AddAsync(version, ct).AsTask();

    public Task AddPreviewAsync(ArtifactPreview preview, CancellationToken ct)
        => db.ArtifactPreviews.AddAsync(preview, ct).AsTask();

    public Task AddDependencyAsync(ArtifactDependency dependency, CancellationToken ct)
        => db.ArtifactDependencies.AddAsync(dependency, ct).AsTask();
}
