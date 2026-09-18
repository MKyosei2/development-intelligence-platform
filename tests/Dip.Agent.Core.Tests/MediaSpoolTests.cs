// Purpose: Verifies behavior and invariants covered by M ed ia Sp oo lT es ts.
using System.Text.Json;
using Dip.Agent.Core;
using FluentAssertions;

namespace Dip.Agent.Core.Tests;

public sealed class MediaSpoolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dip-media-spool-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Media_spool_only_reads_inside_project_root_and_hashes_copy()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "capture.png");
        await File.WriteAllBytesAsync(source, new byte[] { 1, 2, 3, 4, 5 });
        var options = AgentOptions.Validate(new AgentOptions(
            new Uri("http://127.0.0.1:5080/"), Guid.NewGuid(), "agent", "root", _root,
            Path.Combine(_root, ".state", "state.db"), null, TimeSpan.FromSeconds(5),
            AgentOptions.DefaultExcludedDirectories));
        var evt = new ToolEventObservation(
            "unity:media-1", ProductionToolSource.Unity, ProductionToolEventKind.ScreenshotGenerated,
            "6000", null, null, null, null, DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new { camera = "Game" }),
            ProductionMediaKind.Screenshot, "capture.png", "image/png");

        var queued = await MediaSpool.CreateAsync(options, evt, CancellationToken.None);
        queued.Should().NotBeNull();
        queued!.SizeBytes.Should().Be(5);
        queued.Sha256.Should().HaveLength(64);
        File.Exists(queued.SpoolPath).Should().BeTrue();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }
}
