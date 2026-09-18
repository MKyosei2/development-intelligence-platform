// Purpose: Runs background processing for C ol le ct or Wo rk er.
using Dip.Agent.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dip.Agent.Windows;

public sealed class CollectorWorker : BackgroundService
{
    private readonly AgentOptions _options;
    private readonly AgentRuntime _runtime;
    private readonly ILogger<CollectorWorker> _log;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private FileSystemWatcher? _watcher;

    public CollectorWorker(AgentOptions options, AgentRuntime runtime, ILogger<CollectorWorker> log)
    {
        _options = options;
        _runtime = runtime;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _runtime.InitializeAsync(stoppingToken);
        _watcher = CreateWatcher();
        _log.LogInformation(
            "DIP Agent monitoring project {ProjectId} at {Root}. Agent={AgentId}, RootId={RootId}",
            _options.ProjectId,
            _options.ProjectRoot,
            _options.AgentId,
            _options.RootId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scan = await _runtime.ReconcileAsync(stoppingToken);
                if (scan.Observations.Count > 0)
                    _log.LogInformation("Observed {Count} local file changes.", scan.Observations.Count);
                foreach (var warning in scan.Warnings.Take(20))
                    _log.LogWarning("File scan warning: {Warning}", warning);

                var fileSent = await _runtime.FlushAsync(stoppingToken);
                var toolSent = await _runtime.FlushToolEventsAsync(stoppingToken);
                var mediaSent = await _runtime.FlushMediaAsync(stoppingToken);
                if (fileSent + toolSent + mediaSent > 0)
                    _log.LogInformation(
                        "Uploaded {Files} file, {Tools} tool and {Media} media observations.",
                        fileSent, toolSent, mediaSent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _log.LogError(exception, "DIP Agent reconciliation/upload failed; durable outbox will retry.");
            }

            var delay = Task.Delay(_options.ReconcileInterval, stoppingToken);
            var wake = _wake.WaitAsync(stoppingToken);
            await Task.WhenAny(delay, wake);
            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _wake.Dispose();
        return base.StopAsync(cancellationToken);
    }

    private FileSystemWatcher CreateWatcher()
    {
        var watcher = new FileSystemWatcher(_options.ProjectRoot)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Size
        };
        watcher.Changed += (_, _) => Signal();
        watcher.Created += (_, _) => Signal();
        watcher.Deleted += (_, _) => Signal();
        watcher.Renamed += (_, _) => Signal();
        watcher.Error += (_, args) =>
        {
            _log.LogWarning(args.GetException(), "FileSystemWatcher overflow/error; periodic reconciliation remains authoritative.");
            Signal();
        };
        return watcher;
    }

    private void Signal()
    {
        try
        {
            if (_wake.CurrentCount == 0) _wake.Release();
        }
        catch (ObjectDisposedException) { }
    }
}
