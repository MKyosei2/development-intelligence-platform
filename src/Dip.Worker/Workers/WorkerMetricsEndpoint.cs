// Purpose: Implements Worker Metrics Endpoint responsibilities for this repository area.
using System.Net;
using System.Net.Sockets;
using System.Text;
using Dip.Application.Abstractions.Observability;

namespace Dip.Worker.Workers;

public sealed class WorkerMetricsEndpoint(IDipOperationalMetrics metrics, IConfiguration configuration, ILogger<WorkerMetricsEndpoint> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = Math.Clamp(configuration.GetValue<int?>("OperationalMetrics:Port") ?? 9091, 1024, 65535);
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        logger.LogInformation("Worker metrics endpoint listening on port {Port}.", port);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                _ = HandleAsync(client, stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }
    private async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                var buffer = new byte[4096];
                _ = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                var body = metrics.RenderPrometheus();
                var bytes = Encoding.UTF8.GetBytes(body);
                var headerText =
                    "HTTP/1.1 200 OK\r\n"
                    + "Content-Type: text/plain; version=0.0.4; charset=utf-8\r\n"
                    + $"Content-Length: {bytes.Length}\r\n"
                    + "Connection: close\r\n\r\n";
                var header = Encoding.ASCII.GetBytes(headerText);
                await stream.WriteAsync(header, ct);
                await stream.WriteAsync(bytes, ct);
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                logger.LogDebug(ex, "Worker metrics client disconnected.");
            }
        }
    }
}
