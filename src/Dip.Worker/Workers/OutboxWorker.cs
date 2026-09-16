// Purpose: Runs O ut bo xW or ke r background processing.
using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Jobs;
using Dip.Infrastructure.Messaging;
using Dip.Worker.Options;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class OutboxWorker(
    IServiceScopeFactory scopes,
    IQueueClient queue,
    IOptions<QueueOptions> qopt,
    IOptions<DurableJobQueueOptions> jobOptions,
    IOptions<WorkerOptions> wopt,
    ILogger<OutboxWorker> logger):BackgroundService
{
    private readonly QueueOptions _q=qopt.Value;private readonly DurableJobQueueOptions _jobs=jobOptions.Value;private readonly WorkerOptions _w=wopt.Value;
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                using var s=scopes.CreateScope();var repo=s.ServiceProvider.GetRequiredService<IOutboxRepository>();var uow=s.ServiceProvider.GetRequiredService<IUnitOfWork>();var pending=await repo.GetPendingAsync(Math.Clamp(_w.BatchSize,1,500),DateTimeOffset.UtcNow,stop);
                foreach(var m in pending)
                {
                    try
                    {
                        using var doc=JsonDocument.Parse(m.PayloadJson);var stream=m.MessageType.Equals(DurableJobService.RequestedMessageType,StringComparison.OrdinalIgnoreCase)?_jobs.StreamName:_q.StreamName;
                        await queue.PublishAsync(stream,new{messageId=m.Id.ToString("N"),messageType=m.MessageType,payloadVersion="1.0",correlationId=m.CorrelationId,causationId=m.CausationId,attempt=0,createdAtUtc=m.CreatedAtUtc,payload=doc.RootElement.Clone()},stop);
                        m.MarkProcessed(DateTimeOffset.UtcNow);await uow.SaveChangesAsync(stop);
                    }
                    catch(Exception ex)when(ex is not OperationCanceledException){m.MarkFailed(ex.Message,DateTimeOffset.UtcNow.AddSeconds(2));await uow.SaveChangesAsync(stop);logger.LogError(ex,"Failed to publish outbox message {Id}",m.Id);}
                }
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}catch(Exception ex){logger.LogError(ex,"Outbox polling cycle failed.");}
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1,_w.PollingIntervalSeconds)),stop);
        }
    }
}
