// Purpose: Verifies T oo lE ve nt Ou tb ox behavior, edge cases, and invariants for this repository area.
using System.Text.Json;
using Dip.Agent.Core;
using FluentAssertions;

namespace Dip.Agent.Core.Tests;

public sealed class ToolEventOutboxTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dip-agent-tool-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Tool_and_media_outboxes_are_durable()
    {
        Directory.CreateDirectory(_root);
        var db = Path.Combine(_root, "state.db");
        var mediaPath = Path.Combine(_root, "capture.png");
        await File.WriteAllBytesAsync(mediaPath, new byte[] { 1, 2, 3, 4 });

        var store = new SqliteAgentStateStore(db);
        await store.InitializeAsync(CancellationToken.None);
        var evt = new ToolEventObservation(
            "maya:event-1", ProductionToolSource.Maya, ProductionToolEventKind.SceneSaved,
            "2026", "scene.ma", "scene.ma", null, 4, DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new { polygons = 42 }));
        var media = new QueuedMediaUpload(
            "media:maya:event-1", evt.EventId, ProductionToolSource.Maya, ProductionMediaKind.Screenshot,
            "capture.png", mediaPath, "image/png", new string('a', 64), 4,
            DateTimeOffset.UtcNow, 0);

        await store.EnqueueToolEventAsync(evt, media, CancellationToken.None);

        var second = new SqliteAgentStateStore(db);
        await second.InitializeAsync(CancellationToken.None);
        (await second.PeekToolEventsAsync(10, CancellationToken.None)).Should().ContainSingle(x => x.Id == evt.EventId);
        (await second.PeekMediaAsync(10, CancellationToken.None)).Should().ContainSingle(x => x.EventId == evt.EventId);

        await second.CompleteToolEventsAsync(new[] { evt.EventId }, CancellationToken.None);
        await second.CompleteMediaAsync(new[] { media.Id }, CancellationToken.None);
        (await second.PeekToolEventsAsync(10, CancellationToken.None)).Should().BeEmpty();
        (await second.PeekMediaAsync(10, CancellationToken.None)).Should().BeEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch {
            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
        }
    }
}
