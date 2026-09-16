// Purpose: Coordinates local project scanning, durable queueing, and delivery of agent evidence.
namespace Dip.Agent.Core;

public sealed class AgentRuntime : IDisposable
{
    private readonly AgentOptions _options;
    private readonly SqliteAgentStateStore _state;
    private readonly ProjectFileScanner _scanner;
    private readonly AgentApiClient _api;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _initialized;

    public AgentRuntime(AgentOptions options)
    {
        _options = AgentOptions.Validate(options);
        _state = new SqliteAgentStateStore(_options.StateDatabasePath);
        _scanner = new ProjectFileScanner(_options);
        _api = new AgentApiClient(_options);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initializeGate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await _state.InitializeAsync(ct);
            Directory.CreateDirectory(_options.MediaSpoolDirectory);
            _initialized = true;
        }
        finally { _initializeGate.Release(); }
    }

    public async Task<ScanResult> ReconcileAsync(CancellationToken ct)
    {
        await InitializeAsync(ct);
        var previous = await _state.LoadSnapshotAsync(ct);
        var scan = await _scanner.ScanAsync(previous, ct);
        await _state.ApplyScanAsync(scan, ct);
        return scan;
    }

    public async Task EnqueueToolEventAsync(ToolEventObservation item, CancellationToken ct)
    {
        await InitializeAsync(ct);
        ValidateToolEvent(item);
        if (await _state.HasToolEventAsync(item.EventId, ct)) return;
        var media = await MediaSpool.CreateAsync(_options, item, ct);
        await _state.EnqueueToolEventAsync(item, media, ct);
    }

    public async Task<int> FlushAsync(CancellationToken ct)
    {
        await InitializeAsync(ct);
        var total = 0;
        while (true)
        {
            var batch = await _state.PeekAsync(_options.BatchSize, ct);
            if (batch.Count == 0) return total;
            try
            {
                await _api.SendAsync(batch.Select(x => x.Observation).ToArray(), ct);
                await _state.CompleteAsync(batch.Select(x => x.Id), ct);
                total += batch.Count;
            }
            catch
            {
                await _state.RecordFailureAsync(batch.Select(x => x.Id), ct);
                throw;
            }
        }
    }

    public async Task<int> FlushToolEventsAsync(CancellationToken ct)
    {
        await InitializeAsync(ct);
        var total = 0;
        while (true)
        {
            var batch = await _state.PeekToolEventsAsync(Math.Min(_options.BatchSize, 250), ct);
            if (batch.Count == 0) return total;
            try
            {
                await _api.SendToolEventsAsync(batch.Select(x => x.Event).ToArray(), ct);
                await _state.CompleteToolEventsAsync(batch.Select(x => x.Id), ct);
                total += batch.Count;
            }
            catch
            {
                await _state.RecordToolFailureAsync(batch.Select(x => x.Id), ct);
                throw;
            }
        }
    }

    public async Task<int> FlushMediaAsync(CancellationToken ct)
    {
        await InitializeAsync(ct);
        var total = 0;
        while (true)
        {
            var batch = await _state.PeekMediaAsync(10, ct);
            if (batch.Count == 0) return total;
            foreach (var item in batch)
            {
                try
                {
                    if (!File.Exists(item.SpoolPath))
                    {
                        await _state.CompleteMediaAsync(new[] { item.Id }, ct);
                        continue;
                    }
                    await _api.SendMediaAsync(item, ct);
                    await _state.CompleteMediaAsync(new[] { item.Id }, ct);
                    try { File.Delete(item.SpoolPath); } catch { }
                    total++;
                }
                catch
                {
                    await _state.RecordMediaFailureAsync(new[] { item.Id }, ct);
                    // Media policy can change at runtime. Keep the durable item for retry,
                    // but continue with the rest of the media queue so one denied file does
                    // not block later screenshots/videos.
                }
            }
        }
    }

    private static void ValidateToolEvent(ToolEventObservation item)
    {
        if (string.IsNullOrWhiteSpace(item.EventId) || item.EventId.Length > 300)
            throw new ArgumentException("Tool event EventId must be 1-300 characters.");
        if (!Enum.IsDefined(item.Source) || item.Source == ProductionToolSource.Unknown)
            throw new ArgumentException("Tool event source is unsupported.");
        if (!Enum.IsDefined(item.Kind) || item.Kind == ProductionToolEventKind.Unknown)
            throw new ArgumentException("Tool event kind is unsupported.");
    }

    public void Dispose()
    {
        _initializeGate.Dispose();
        _api.Dispose();
    }
}
