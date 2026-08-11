using System.Text;
using Dip.Infrastructure.Artifacts;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Tests.Artifacts;

public sealed class LocalArtifactStoreTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"dip-artifact-test-"+Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Put_IsContentAddressed_AndReadable()
    {
        var store=new LocalArtifactStore(Options.Create(new ArtifactStoreOptions{LocalRoot=_root}));
        var bytes=Encoding.UTF8.GetBytes("durable artifact");
        await using var source=new MemoryStream(bytes);

        var stored=await store.PutAsync(source,"text/plain",CancellationToken.None);
        await using var opened=await store.OpenReadAsync(stored.StorageKey,CancellationToken.None);
        using var reader=new StreamReader(opened,Encoding.UTF8);

        Assert.StartsWith("sha256/",stored.StorageKey);
        Assert.Equal(64,stored.Sha256.Length);
        Assert.Equal("durable artifact",await reader.ReadToEndAsync());
    }

    public void Dispose(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
}
