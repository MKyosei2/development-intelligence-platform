// Purpose: Defines the IArtifactRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IArtifactRepository
{
    Task<ArtifactBlob?> FindBlobAsync(string provider, string sha256, CancellationToken ct);
    Task<ArtifactBlob> GetOrCreateBlobAsync(ArtifactBlob candidate, CancellationToken ct);
    Task<ArtifactRecord?> GetRecordAsync(Guid id, CancellationToken ct);
    Task<ArtifactRecord?> GetRecordForUpdateAsync(Guid id, CancellationToken ct);
    Task<ArtifactBlob?> GetBlobAsync(Guid id, CancellationToken ct);
    Task<ArtifactBlob?> GetBlobForUpdateAsync(Guid id, CancellationToken ct);
    Task AddBlobAsync(ArtifactBlob blob, CancellationToken ct);
    Task AddRecordAsync(ArtifactRecord record, CancellationToken ct);
    Task<IReadOnlyList<ArtifactRecord>> ListForJobAsync(Guid jobId, CancellationToken ct);
    Task<IReadOnlyList<ArtifactRecord>> ListExpiredAsync(DateTimeOffset now, int take, CancellationToken ct);
    Task<bool> HasRecordsForBlobAsync(Guid blobId, CancellationToken ct);
    void RemoveRecord(ArtifactRecord record);
    void RemoveBlob(ArtifactBlob blob);
}
