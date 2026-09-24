// Purpose: Defines P ip el in eR un domain behavior and invariants for o pe ra ti on s.
using Dip.Domain.Enums;
using Dip.Domain.Exceptions;
using Dip.Domain.ValueObjects;
namespace Dip.Domain.Entities;
public sealed class PipelineRun { private PipelineRun() { }
public PipelineRun(Guid id, Guid projectId, CommitSha sha, string externalRunId, int attemptNumber, string branch, string collectorType, string collectorVersion, DateTimeOffset queuedAtUtc) { Id = id == Guid.Empty ? Guid.NewGuid() : id;
 ProjectId = projectId;
 CommitSha = sha;
 ExternalRunId = Req(externalRunId, 200);
 AttemptNumber = attemptNumber >= 1 ? attemptNumber : throw new ArgumentOutOfRangeException(nameof(attemptNumber));
 Branch = Req(branch, 200);
 CollectorType = Req(collectorType, 100);
 CollectorVersion = Req(collectorVersion, 50);
 QueuedAtUtc = queuedAtUtc;
 Status = PipelineStatus.Queued;
 }
public Guid Id { get;
 private set;
 }
public Guid ProjectId { get;
 private set;
 }
public CommitSha CommitSha { get;
 private set;
 }
public string ExternalRunId { get;
 private set;
 } = string.Empty;
 public int AttemptNumber { get;
 private set;
 }
public string Branch { get;
 private set;
 } = string.Empty;
 public string CollectorType { get;
 private set;
 } = string.Empty;
 public string CollectorVersion { get;
 private set;
 } = string.Empty;
 public PipelineStatus Status { get;
 private set;
 }
public DateTimeOffset QueuedAtUtc { get;
 private set;
 }
public DateTimeOffset? StartedAtUtc { get;
 private set;
 }
public DateTimeOffset? FinishedAtUtc { get;
 private set;
 }
public string? FailureReason { get;
 private set;
 }
public void Start(DateTimeOffset at) { Ensure(PipelineStatus.Queued);
if (at < QueuedAtUtc) throw new DomainException("Start time cannot precede queue time.");
 Status = PipelineStatus.Running;
 StartedAtUtc = at;
 }
public void Complete(DateTimeOffset at) { Ensure(PipelineStatus.Running);
 Finish(at);
 Status = PipelineStatus.Succeeded;
 FailureReason = null;
 }
public void CompletePartial(string reason, DateTimeOffset at) { Ensure(PipelineStatus.Running);
 Finish(at);
 Status = PipelineStatus.PartiallySucceeded;
 FailureReason = Req(reason, 2000);
 }
public void Fail(string reason, DateTimeOffset at) { if (Status is not (PipelineStatus.Queued or PipelineStatus.Running or PipelineStatus.Retrying)) throw new DomainException($"Cannot fail from {Status}.");
if (StartedAtUtc.HasValue) Finish(at);
 else FinishedAtUtc = at;
 Status = PipelineStatus.Failed;
 FailureReason = Req(reason, 2000);
 }
public void Cancel(string reason, DateTimeOffset at) { if (Status is not (PipelineStatus.Queued or PipelineStatus.Running)) throw new DomainException($"Cannot cancel from {Status}.");
 FinishedAtUtc = at;
 Status = PipelineStatus.Cancelled;
 FailureReason = Req(reason, 2000);
 }
public void Timeout(string reason, DateTimeOffset at) { Ensure(PipelineStatus.Running);
 Finish(at);
 Status = PipelineStatus.TimedOut;
 FailureReason = Req(reason, 2000);
 }
private void Finish(DateTimeOffset at) { if (!StartedAtUtc.HasValue || at < StartedAtUtc.Value) throw new DomainException("Finish time cannot precede start time.");
 FinishedAtUtc = at;
 }
private void Ensure(PipelineStatus expected) { if (Status != expected) throw new DomainException($"Expected {expected}, was {Status}.");
 }
private static string Req(string? v, int max) { var s = v?.Trim() ?? string.Empty;
if (s.Length is < 1 || s.Length > max) throw new ArgumentException($"Text length must be 1-{max}.");
 return s;
 } }
