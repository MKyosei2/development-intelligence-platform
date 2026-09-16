// Purpose: Verifies S ql it eA ge nt St at eS to re Te st s behavior.
using Dip.Agent.Core;
using FluentAssertions;

namespace Dip.Agent.Core.Tests;

public sealed class SqliteAgentStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dip-agent-state-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Snapshot_and_outbox_are_durable_across_store_instances()
    {
        Directory.CreateDirectory(_root);
        var db = Path.Combine(_root, "state.db");
        var eventId = "local-file:" + new string('a', 64);
        var observation = new LocalFileObservation(
            eventId, FileObservationKind.Created, "scene.ma", null, ".ma", 12,
            new string('b', 64), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var snapshot = new Dictionary<string, FileSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            ["scene.ma"] = new("scene.ma", 12, DateTimeOffset.UtcNow, new string('b', 64))
        };

        var first = new SqliteAgentStateStore(db);
        await first.InitializeAsync(CancellationToken.None);
        await first.ApplyScanAsync(new ScanResult(new[] { observation }, snapshot, Array.Empty<string>()), CancellationToken.None);

        var second = new SqliteAgentStateStore(db);
        await second.InitializeAsync(CancellationToken.None);
        (await second.LoadSnapshotAsync(CancellationToken.None)).Should().ContainKey("scene.ma");
        var queued = await second.PeekAsync(10, CancellationToken.None);
        queued.Should().ContainSingle(x => x.Id == eventId);

        await second.CompleteAsync(new[] { eventId }, CancellationToken.None);
        (await second.PeekAsync(10, CancellationToken.None)).Should().BeEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }
}
