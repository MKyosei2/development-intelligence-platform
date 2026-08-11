using System.Security.Cryptography;
using Dip.Application.Abstractions.Artifacts;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Artifacts;

public sealed class LocalArtifactStore:IArtifactStore
{
    private readonly string _root;
    public LocalArtifactStore(IOptions<ArtifactStoreOptions> options){_root=Path.GetFullPath(options.Value.LocalRoot);Directory.CreateDirectory(_root);}
    public string ProviderName=>"local";
    public async Task<StoredArtifactContent> PutAsync(Stream content,string contentType,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);if(!content.CanRead)throw new ArgumentException("Artifact content must be readable.",nameof(content));
        var temp=Path.Combine(_root,$".upload-{Guid.NewGuid():N}.tmp");long size=0;string hash;
        try
        {
            await using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous|FileOptions.SequentialScan))
            using(var sha=SHA256.Create())
            {
                var buffer=new byte[81920];int read;
                while((read=await content.ReadAsync(buffer.AsMemory(0,buffer.Length),ct))>0){await output.WriteAsync(buffer.AsMemory(0,read),ct);sha.TransformBlock(buffer,0,read,null,0);size+=read;}
                sha.TransformFinalBlock(Array.Empty<byte>(),0,0);hash=Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            }
            var key=$"sha256/{hash[..2]}/{hash}";var target=ResolveKey(key);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if(!File.Exists(target))File.Move(temp,target);else File.Delete(temp);
            return new(key,hash,size,string.IsNullOrWhiteSpace(contentType)?"application/octet-stream":contentType.Trim());
        }
        catch{if(File.Exists(temp))File.Delete(temp);throw;}
    }
    public Task<Stream> OpenReadAsync(string storageKey,CancellationToken ct){ct.ThrowIfCancellationRequested();Stream stream=new FileStream(ResolveKey(storageKey),FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous|FileOptions.SequentialScan);return Task.FromResult(stream);}
    public Task DeleteAsync(string storageKey,CancellationToken ct){ct.ThrowIfCancellationRequested();var path=ResolveKey(storageKey);if(File.Exists(path))File.Delete(path);return Task.CompletedTask;}
    public Task<bool> ExistsAsync(string storageKey,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(File.Exists(ResolveKey(storageKey)));}
    private string ResolveKey(string key){var normalized=key.Replace('/',Path.DirectorySeparatorChar).Replace('\\',Path.DirectorySeparatorChar);if(Path.IsPathRooted(normalized))throw new InvalidOperationException("Artifact key must be relative.");var full=Path.GetFullPath(Path.Combine(_root,normalized));var prefix=_root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!full.StartsWith(prefix,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidOperationException("Artifact key escapes the configured root.");return full;}
}
