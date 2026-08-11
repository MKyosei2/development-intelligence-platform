using System.Text.Json;
using Dip.Application.Abstractions.Artifacts;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;

namespace Dip.Application.Features.Artifacts;

public sealed record ArtifactLinkContext(Guid? JobId=null,Guid? QaRunId=null,Guid? IncidentId=null,Guid? BenchmarkRunId=null);
public sealed record ArtifactRecordDto(Guid Id,string Kind,string FileName,string Sha256,long SizeBytes,string ContentType,string Provider,DateTimeOffset CreatedAtUtc);

public sealed class ArtifactService
{
    private readonly IArtifactStore _store;private readonly IArtifactRepository _repo;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public ArtifactService(IArtifactStore store,IArtifactRepository repo,IUnitOfWork uow,ISystemClock clock){_store=store;_repo=repo;_uow=uow;_clock=clock;}

    public async Task<ArtifactRecord> StoreAsync(Guid projectId,string kind,string fileName,Stream content,string contentType,ArtifactLinkContext links,string retentionClass="standard",TimeSpan? retention=null,CancellationToken ct=default)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        var stored=await _store.PutAsync(content,contentType,ct);
        var candidate=new ArtifactBlob(Guid.NewGuid(),stored.Sha256,stored.SizeBytes,stored.ContentType,_store.ProviderName,stored.StorageKey,_clock.UtcNow);
        var blob=await _repo.GetOrCreateBlobAsync(candidate,ct);
        var record=new ArtifactRecord(Guid.NewGuid(),projectId,blob.Id,kind,Path.GetFileName(fileName),retentionClass,links.JobId,links.QaRunId,links.IncidentId,links.BenchmarkRunId,_clock.UtcNow,retention is null?null:_clock.UtcNow+retention.Value);
        await _repo.AddRecordAsync(record,ct);await _uow.SaveChangesAsync(ct);return record;
    }

    public async Task<ArtifactRecord> StoreJsonAsync(Guid projectId,string kind,string fileName,object value,ArtifactLinkContext links,string retentionClass="standard",CancellationToken ct=default)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true});await using var stream=new MemoryStream(bytes,false);return await StoreAsync(projectId,kind,fileName,stream,"application/json",links,retentionClass,null,ct);
    }

    public async Task<IReadOnlyList<ArtifactRecord>> ImportPathAsync(Guid projectId,string kind,string path,ArtifactLinkContext links,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(path))return Array.Empty<ArtifactRecord>();var full=Path.GetFullPath(path);var files=File.Exists(full)?new[]{full}:Directory.Exists(full)?Directory.EnumerateFiles(full,"*",SearchOption.AllDirectories).Take(200).ToArray():Array.Empty<string>();
        var result=new List<ArtifactRecord>();foreach(var file in files){ct.ThrowIfCancellationRequested();var info=new FileInfo(file);if(info.Length>512L*1024*1024)continue;await using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true);result.Add(await StoreAsync(projectId,kind,Path.GetFileName(file),stream,ContentType(file),links,"standard",null,ct));}return result;
    }

    public async Task<(ArtifactRecord Record,ArtifactBlob Blob,Stream Content)?> OpenAsync(Guid artifactId,CancellationToken ct)
    {
        var record=await _repo.GetRecordAsync(artifactId,ct);if(record is null)return null;var blob=await _repo.GetBlobAsync(record.ArtifactBlobId,ct);if(blob is null)return null;if(!string.Equals(blob.StorageProvider,_store.ProviderName,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException($"Artifact {artifactId} is stored by provider '{blob.StorageProvider}' but the configured provider is '{_store.ProviderName}'.");var stream=await _store.OpenReadAsync(blob.StorageKey,ct);return(record,blob,stream);
    }
    public async Task<IReadOnlyList<ArtifactRecordDto>> ListForJobAsync(Guid jobId,CancellationToken ct)
    {var records=await _repo.ListForJobAsync(jobId,ct);var list=new List<ArtifactRecordDto>();foreach(var r in records){var b=await _repo.GetBlobAsync(r.ArtifactBlobId,ct);if(b is not null)list.Add(new(r.Id,r.Kind,r.FileName,b.Sha256,b.SizeBytes,b.ContentType,b.StorageProvider,r.CreatedAtUtc));}return list;}

    public async Task<int> CleanupExpiredAsync(DateTimeOffset now,int take,CancellationToken ct)
    {
        var expired=await _repo.ListExpiredAsync(now,take,ct);var deleted=0;
        foreach(var record in expired)
        {
            ct.ThrowIfCancellationRequested();var blob=await _repo.GetBlobForUpdateAsync(record.ArtifactBlobId,ct);_repo.RemoveRecord(record);await _uow.SaveChangesAsync(ct);deleted++;
            if(blob is null||await _repo.HasRecordsForBlobAsync(blob.Id,ct))continue;
            if(string.Equals(blob.StorageProvider,_store.ProviderName,StringComparison.OrdinalIgnoreCase))
            {
                try{await _store.DeleteAsync(blob.StorageKey,ct);}catch(FileNotFoundException){}
                _repo.RemoveBlob(blob);await _uow.SaveChangesAsync(ct);
            }
        }
        return deleted;
    }
    private static string ContentType(string file)=>Path.GetExtension(file).ToLowerInvariant() switch{".json"=>"application/json",".txt" or ".log"=>"text/plain",".png"=>"image/png",".jpg" or ".jpeg"=>"image/jpeg",".wav"=>"audio/wav",".mp4"=>"video/mp4",".zip"=>"application/zip",_=>"application/octet-stream"};
}
