// Purpose: Represents D ur ab le Jo b in the Operations domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class DurableJob
{
    private DurableJob() { }
    public DurableJob(Guid id,Guid projectId,DurableJobType type,string subjectKey,string payloadJson,int maxAttempts,DateTimeOffset createdAtUtc)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        Id=id==Guid.Empty?Guid.NewGuid():id;ProjectId=projectId;Type=type;SubjectKey=Required(subjectKey,200,nameof(subjectKey));
        PayloadJson=Required(payloadJson,2_000_000,nameof(payloadJson));MaxAttempts=Math.Clamp(maxAttempts,1,20);
        Status=DurableJobStatus.Queued;ProgressPercent=0;CurrentStage="Queued";CreatedAtUtc=createdAtUtc;QueuedAtUtc=createdAtUtc;
    }
    public Guid Id{get;private set;} public Guid ProjectId{get;private set;} public DurableJobType Type{get;private set;} public DurableJobStatus Status{get;private set;}
    public string SubjectKey{get;private set;}=string.Empty; public string PayloadJson{get;private set;}="{}"; public string? ResultJson{get;private set;}
    public int Attempt{get;private set;} public int MaxAttempts{get;private set;} public decimal ProgressPercent{get;private set;} public string? CurrentStage{get;private set;}
    public DateTimeOffset CreatedAtUtc{get;private set;} public DateTimeOffset? QueuedAtUtc{get;private set;} public DateTimeOffset? StartedAtUtc{get;private set;} public DateTimeOffset? FinishedAtUtc{get;private set;}
    public DateTimeOffset? HeartbeatAtUtc{get;private set;} public string? LeaseOwner{get;private set;} public DateTimeOffset? LeaseExpiresAtUtc{get;private set;}
    public DateTimeOffset? CancellationRequestedAtUtc{get;private set;} public string? FailureCode{get;private set;} public string? FailureMessage{get;private set;}

    public bool CanClaim(DateTimeOffset now)=>Status==DurableJobStatus.Queued&&CancellationRequestedAtUtc is null&&(LeaseExpiresAtUtc is null||LeaseExpiresAtUtc<=now);
    public void Claim(string owner,DateTimeOffset now,TimeSpan lease)
    {
        if(!CanClaim(now))throw new InvalidOperationException($"Job {Id} cannot be claimed from {Status}.");
        if(Attempt>=MaxAttempts)throw new InvalidOperationException($"Job {Id} exhausted its attempts.");
        Attempt++;Status=DurableJobStatus.Running;StartedAtUtc??=now;HeartbeatAtUtc=now;LeaseOwner=Required(owner,200,nameof(owner));LeaseExpiresAtUtc=now+lease;CurrentStage="Running";
    }
    public void Heartbeat(string owner,DateTimeOffset now,TimeSpan lease,decimal? progress=null,string? stage=null)
    {
        if(Status!=DurableJobStatus.Running||!string.Equals(LeaseOwner,owner,StringComparison.Ordinal))throw new InvalidOperationException("Worker does not own this running job.");
        HeartbeatAtUtc=now;LeaseExpiresAtUtc=now+lease;
        if(progress.HasValue)ProgressPercent=Math.Clamp(decimal.Round(progress.Value,2),0m,99.99m);
        if(!string.IsNullOrWhiteSpace(stage))CurrentStage=Trim(stage,200);
    }
    public void RequestCancellation(DateTimeOffset now){if(Status is DurableJobStatus.Succeeded or DurableJobStatus.Failed or DurableJobStatus.Cancelled or DurableJobStatus.TimedOut)return;CancellationRequestedAtUtc??=now;if(Status==DurableJobStatus.Queued)Cancel(now);}
    public void Complete(string? resultJson,DateTimeOffset now){EnsureRunning();Status=DurableJobStatus.Succeeded;ResultJson=Optional(resultJson,2_000_000);ProgressPercent=100m;CurrentStage="Succeeded";FinishedAtUtc=now;ClearLease();}
    public void Fail(string code,string message,DateTimeOffset now,bool timedOut=false){EnsureRunning();Status=timedOut?DurableJobStatus.TimedOut:DurableJobStatus.Failed;FailureCode=Trim(code,120);FailureMessage=Trim(message,8000);CurrentStage=Status.ToString();FinishedAtUtc=now;ClearLease();}
    public void Cancel(DateTimeOffset now){if(Status is DurableJobStatus.Succeeded or DurableJobStatus.Failed or DurableJobStatus.Cancelled or DurableJobStatus.TimedOut)return;Status=DurableJobStatus.Cancelled;CurrentStage="Cancelled";FinishedAtUtc=now;ClearLease();}
    public bool RecoverExpiredLease(DateTimeOffset now)
    {
        if(Status!=DurableJobStatus.Running||LeaseExpiresAtUtc is null||LeaseExpiresAtUtc>now)return false;
        if(CancellationRequestedAtUtc is not null){Cancel(now);return false;}
        if(Attempt>=MaxAttempts){Status=DurableJobStatus.Failed;FailureCode="lease-expired";FailureMessage="Worker lease expired and the maximum attempt count was reached.";CurrentStage="Failed";FinishedAtUtc=now;ClearLease();return false;}
        Status=DurableJobStatus.Queued;QueuedAtUtc=now;CurrentStage="Recovered";ClearLease();return true;
    }
    public void Retry(DateTimeOffset now)
    {
        if(Status is not (DurableJobStatus.Failed or DurableJobStatus.TimedOut or DurableJobStatus.Cancelled))throw new InvalidOperationException("Only terminal unsuccessful jobs can be retried.");
        if(Attempt>=MaxAttempts)throw new InvalidOperationException("Maximum attempts have been reached.");
        Status=DurableJobStatus.Queued;QueuedAtUtc=now;FinishedAtUtc=null;CancellationRequestedAtUtc=null;FailureCode=null;FailureMessage=null;ProgressPercent=0;CurrentStage="Queued";ClearLease();
    }
    private void EnsureRunning(){if(Status!=DurableJobStatus.Running)throw new InvalidOperationException($"Job {Id} is not running.");}
    private void ClearLease(){LeaseOwner=null;LeaseExpiresAtUtc=null;HeartbeatAtUtc=null;}
    private static string Required(string? value,int max,string name){var text=value?.Trim()??string.Empty;if(text.Length is <1||text.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);return text;}
    private static string Trim(string value,int max)=>value.Length<=max?value:value[..max];
    private static string? Optional(string? value,int max)=>string.IsNullOrWhiteSpace(value)?null:Trim(value.Trim(),max);
}
