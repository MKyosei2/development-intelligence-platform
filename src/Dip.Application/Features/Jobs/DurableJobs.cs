using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Jobs;

public sealed class DurableJobQueueOptions
{
    public const string SectionName="DurableJobs";
    public string StreamName{get;init;}="dip:jobs";
    public string DeadLetterStreamName{get;init;}="dip:jobs:dead-letter";
    public string ConsumerGroup{get;init;}="dip-job-workers";
    public int LeaseSeconds{get;init;}=60;
    public int HeartbeatSeconds{get;init;}=15;
    public int BlockMilliseconds{get;init;}=5000;
    public int RecoveryIntervalSeconds{get;init;}=15;
}

public sealed record DurableJobDto(Guid Id,Guid ProjectId,string Type,string Status,string SubjectKey,int Attempt,int MaxAttempts,decimal ProgressPercent,string? CurrentStage,DateTimeOffset CreatedAtUtc,DateTimeOffset? StartedAtUtc,DateTimeOffset? FinishedAtUtc,bool CancellationRequested,string? FailureCode,string? FailureMessage);
public sealed record EnqueuedJobDto(Guid JobId,string Status,string StatusUrl);
public sealed record DurableJobExecutionResult(string? ResultJson,string? ArtifactReference=null);

public interface IDurableJobHandler
{
    DurableJobType Type{get;}
    Task<DurableJobExecutionResult> ExecuteAsync(DurableJob job,CancellationToken cancellationToken);
}

public sealed class DurableJobService
{
    public const string RequestedMessageType="durable-job.requested.v1";
    private readonly IDurableJobRepository _jobs;private readonly IOutboxRepository _outbox;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public DurableJobService(IDurableJobRepository jobs,IOutboxRepository outbox,IUnitOfWork uow,ISystemClock clock){_jobs=jobs;_outbox=outbox;_uow=uow;_clock=clock;}

    public async Task<EnqueuedJobDto> EnqueueAsync<T>(Guid projectId,DurableJobType type,string subjectKey,T payload,int maxAttempts,CancellationToken ct)
    {
        var job=new DurableJob(Guid.NewGuid(),projectId,type,subjectKey,JsonSerializer.Serialize(payload),maxAttempts,_clock.UtcNow);await _jobs.AddAsync(job,ct);await EnqueueOutboxAsync(job.Id,ct);await _uow.SaveChangesAsync(ct);return new(job.Id,job.Status.ToString(),$"/api/v1/jobs/{job.Id}");
    }
    public async Task<DurableJobDto?> GetAsync(Guid id,CancellationToken ct){var job=await _jobs.GetAsync(id,ct);return job is null?null:Map(job);}
    public async Task<IReadOnlyList<DurableJobDto>> ListAsync(Guid projectId,int take,CancellationToken ct)=>(await _jobs.ListRecentAsync(projectId,take,ct)).Select(Map).ToArray();
    public async Task<bool> RequestCancellationAsync(Guid id,CancellationToken ct){var job=await _jobs.GetAsync(id,ct);if(job is null)return false;job.RequestCancellation(_clock.UtcNow);await _uow.SaveChangesAsync(ct);return true;}
    public async Task<bool> RetryAsync(Guid id,CancellationToken ct){var job=await _jobs.GetAsync(id,ct);if(job is null)return false;job.Retry(_clock.UtcNow);await EnqueueOutboxAsync(job.Id,ct);await _uow.SaveChangesAsync(ct);return true;}
    public async Task<int> RecoverExpiredAsync(int take,CancellationToken ct)
    {
        var expired=await _jobs.ListExpiredLeasesAsync(_clock.UtcNow,take,ct);var requeued=0;foreach(var job in expired){if(job.RecoverExpiredLease(_clock.UtcNow)){await EnqueueOutboxAsync(job.Id,ct);requeued++;}}if(expired.Count>0)await _uow.SaveChangesAsync(ct);return requeued;
    }
    private Task EnqueueOutboxAsync(Guid jobId,CancellationToken ct)=>_outbox.AddAsync(new OutboxMessage(Guid.NewGuid(),RequestedMessageType,JsonSerializer.Serialize(new{jobId}),jobId.ToString("N"),null,_clock.UtcNow),ct);
    public static DurableJobDto Map(DurableJob x)=>new(x.Id,x.ProjectId,x.Type.ToString(),x.Status.ToString(),x.SubjectKey,x.Attempt,x.MaxAttempts,x.ProgressPercent,x.CurrentStage,x.CreatedAtUtc,x.StartedAtUtc,x.FinishedAtUtc,x.CancellationRequestedAtUtc is not null,x.FailureCode,x.FailureMessage);
}
