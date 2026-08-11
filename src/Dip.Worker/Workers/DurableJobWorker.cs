using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Jobs;
using Dip.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class DurableJobWorker(IServiceScopeFactory scopes,IQueueClient queue,IOptions<DurableJobQueueOptions> options,ILogger<DurableJobWorker> logger):BackgroundService
{
    private readonly DurableJobQueueOptions _o=options.Value;private readonly string _consumer=$"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                var envelope=await queue.ReadAsync(_o.StreamName,_o.ConsumerGroup,_consumer,TimeSpan.FromMilliseconds(Math.Max(100,_o.BlockMilliseconds)),stop);if(envelope is null)continue;
                try{await ProcessAsync(envelope,stop);await queue.AcknowledgeAsync(_o.StreamName,_o.ConsumerGroup,envelope.MessageId,stop);}
                catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}
                catch(Exception ex){logger.LogError(ex,"Durable job message {MessageId} failed.",envelope.MessageId);await queue.MoveToDeadLetterAsync(_o.StreamName,_o.ConsumerGroup,_o.DeadLetterStreamName,envelope,ex.Message,stop);}
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Durable job worker loop failed.");await Task.Delay(1000,stop);}
        }
    }
    private async Task ProcessAsync(QueueEnvelope envelope,CancellationToken stop)
    {
        var jobId=ReadJobId(envelope.PayloadJson);using var scope=scopes.CreateScope();var repo=scope.ServiceProvider.GetRequiredService<IDurableJobRepository>();var uow=scope.ServiceProvider.GetRequiredService<IUnitOfWork>();var lease=TimeSpan.FromSeconds(Math.Clamp(_o.LeaseSeconds,15,3600));var job=await repo.TryClaimAsync(jobId,_consumer,DateTimeOffset.UtcNow,lease,stop);if(job is null)return;
        var handler=scope.ServiceProvider.GetServices<IDurableJobHandler>().SingleOrDefault(x=>x.Type==job.Type)??throw new InvalidOperationException($"No durable job handler is registered for {job.Type}.");
        using var jobCancel=new CancellationTokenSource();using var linked=CancellationTokenSource.CreateLinkedTokenSource(stop,jobCancel.Token);var monitor=MonitorAsync(job.Id,jobCancel,stop);
        try
        {
            var result=await handler.ExecuteAsync(job,linked.Token);job.Complete(result.ResultJson,DateTimeOffset.UtcNow);await uow.SaveChangesAsync(stop);
        }
        catch(OperationCanceledException)when(stop.IsCancellationRequested){throw;}
        catch(OperationCanceledException)when(jobCancel.IsCancellationRequested){job.Cancel(DateTimeOffset.UtcNow);await uow.SaveChangesAsync(stop);}
        catch(TimeoutException ex){job.Fail("timeout",ex.Message,DateTimeOffset.UtcNow,true);await uow.SaveChangesAsync(stop);}
        catch(Exception ex){job.Fail("handler-failure",ex.Message,DateTimeOffset.UtcNow);await uow.SaveChangesAsync(stop);logger.LogError(ex,"Durable job {JobId} failed in handler {JobType}.",job.Id,job.Type);}
        finally{jobCancel.Cancel();try{await monitor;}catch(OperationCanceledException){}catch(Exception ex){logger.LogWarning(ex,"Job heartbeat monitor ended with an error for {JobId}.",job.Id);}}
    }
    private async Task MonitorAsync(Guid jobId,CancellationTokenSource executionCancel,CancellationToken stop)
    {
        var delay=TimeSpan.FromSeconds(Math.Clamp(_o.HeartbeatSeconds,2,300));var lease=TimeSpan.FromSeconds(Math.Clamp(_o.LeaseSeconds,15,3600));
        using var monitorStop=CancellationTokenSource.CreateLinkedTokenSource(stop,executionCancel.Token);var ct=monitorStop.Token;
        while(!ct.IsCancellationRequested)
        {
            await Task.Delay(delay,ct);using var scope=scopes.CreateScope();var repo=scope.ServiceProvider.GetRequiredService<IDurableJobRepository>();var uow=scope.ServiceProvider.GetRequiredService<IUnitOfWork>();var current=await repo.GetAsync(jobId,ct);if(current is null)return;if(current.CancellationRequestedAtUtc is not null){executionCancel.Cancel();return;}if(current.Status!=DurableJobStatus.Running)return;current.Heartbeat(_consumer,DateTimeOffset.UtcNow,lease);await uow.SaveChangesAsync(ct);
        }
    }
    private static Guid ReadJobId(string json)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement.TryGetProperty("payload",out var payload)?payload:doc.RootElement;if(root.ValueKind==JsonValueKind.String){using var nested=JsonDocument.Parse(root.GetString()!);root=nested.RootElement.Clone();}if(root.TryGetProperty("jobId",out var id)&&Guid.TryParse(id.GetString(),out var parsed))return parsed;throw new InvalidDataException("Durable job queue payload does not contain jobId.");
    }
}
