namespace Dip.Application.Abstractions.Artifacts;

public sealed record StoredArtifactContent(string StorageKey,string Sha256,long SizeBytes,string ContentType);

public interface IArtifactStore
{
    string ProviderName{get;}
    Task<StoredArtifactContent> PutAsync(Stream content,string contentType,CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageKey,CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey,CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string storageKey,CancellationToken cancellationToken);
}
