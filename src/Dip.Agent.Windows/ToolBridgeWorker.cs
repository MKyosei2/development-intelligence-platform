// Purpose: Runs T oo lB ri dg e background processing for this repository area.
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Dip.Agent.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dip.Agent.Windows;

public sealed class ToolBridgeWorker : BackgroundService
{
    private readonly AgentOptions _options;
    private readonly AgentRuntime _runtime;
    private readonly ILogger<ToolBridgeWorker> _log;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ToolBridgeWorker(AgentOptions options, AgentRuntime runtime, ILogger<ToolBridgeWorker> log)
    {
        _options = options;
        _runtime = runtime;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _runtime.InitializeAsync(stoppingToken);
        _log.LogInformation("DIP production-tool bridge listening on named pipe {PipeName}", _options.PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _options.PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(stoppingToken);
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
                var line = await reader.ReadLineAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.Length > 64 * 1024)
                {
                    _log.LogWarning("Rejected production-tool bridge message larger than 64 KiB.");
                    continue;
                }

                var message = JsonSerializer.Deserialize<ToolBridgeMessage>(line, JsonOptions);
                if (message is null)
                {
                    _log.LogWarning("Rejected undecodable production-tool bridge message.");
                    continue;
                }
                if (!TokenMatches(message.Token))
                {
                    _log.LogWarning("Rejected production-tool bridge message with invalid bridge token.");
                    continue;
                }

                await _runtime.EnqueueToolEventAsync(message.Event, stoppingToken);
                _log.LogInformation("Queued {Source}/{Kind} event {EventId}", message.Event.Source, message.Event.Kind, message.Event.EventId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _log.LogError(exception, "Production-tool bridge receive failed; listener will restart.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private bool TokenMatches(string? provided)
    {
        if (string.IsNullOrWhiteSpace(_options.BridgeToken)) return true;
        return string.Equals(_options.BridgeToken, provided, StringComparison.Ordinal);
    }
}
