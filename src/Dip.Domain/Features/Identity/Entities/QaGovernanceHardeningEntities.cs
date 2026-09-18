// Purpose: Represents Q aG ov er na nc eH ar de ni ng En ti ti es in the DIP domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class QaGovernanceProcessedRun
{
    private QaGovernanceProcessedRun() { }
    public QaGovernanceProcessedRun(Guid id, Guid projectId, Guid qaRunId, string governanceVersion, string inputHash, DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id; ProjectId = Required(projectId); QaRunId = Required(qaRunId);
        GovernanceVersion = Text(governanceVersion, 80); InputHash = Text(inputHash, 64); Status = QaProcessedRunStatus.Processing;
        StartedAtUtc = UpdatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid QaRunId { get; private set; }
    public string GovernanceVersion { get; private set; } = string.Empty;
    public string InputHash { get; private set; } = string.Empty;
    public string? ResultHash { get; private set; }
    public QaProcessedRunStatus Status { get; private set; }
    public string? FailureDetail { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public void Complete(string resultHash, DateTimeOffset now) { ResultHash = Text(resultHash, 64); Status = QaProcessedRunStatus.Completed; FailureDetail = null; CompletedAtUtc = UpdatedAtUtc = now; }
    public void Fail(string detail, DateTimeOffset now) { FailureDetail = Text(detail, 4000); Status = QaProcessedRunStatus.Failed; UpdatedAtUtc = now; }
    private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException("Guid is required.") : value;
    private static string Text(string? value, int max) { var v = value?.Trim() ?? string.Empty; if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"Text must be 1-{max} characters."); return v; }
}

public sealed class QaReleaseGateOverride
{
    private QaReleaseGateOverride() { }
    public QaReleaseGateOverride(Guid id, Guid projectId, Guid? policyEvaluationId, Guid? qaRunId, string commitSha, string reason, string requestedBy, DateTimeOffset now, DateTimeOffset expiresAtUtc)
    {
        if (expiresAtUtc <= now) throw new ArgumentException("Override expiry must be in the future.");
        Id = id == Guid.Empty ? Guid.NewGuid() : id; ProjectId = Required(projectId); PolicyEvaluationId = policyEvaluationId; QaRunId = qaRunId;
        CommitSha = Text(commitSha, 64); Reason = Text(reason, 2000); RequestedBy = Text(requestedBy, 200); Status = QaReleaseOverrideStatus.Requested;
        CreatedAtUtc = UpdatedAtUtc = now; ExpiresAtUtc = expiresAtUtc; Version=1;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? PolicyEvaluationId { get; private set; }
    public Guid? QaRunId { get; private set; }
    public string CommitSha { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string RequestedBy { get; private set; } = string.Empty;
    public QaReleaseOverrideStatus Status { get; private set; }
    public string? ApprovedBy { get; private set; }
    public string? DecisionReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public long Version { get; private set; }
    public bool IsEffective(DateTimeOffset now) => Status == QaReleaseOverrideStatus.Approved && now < ExpiresAtUtc;
    public void Approve(string actor, string reason, DateTimeOffset now) { if (Status != QaReleaseOverrideStatus.Requested) throw new InvalidOperationException("Only requested overrides can be approved."); if (string.Equals(RequestedBy, actor, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Release override requester cannot approve the same override."); ApprovedBy = Text(actor, 200); DecisionReason = Text(reason, 2000); Status = QaReleaseOverrideStatus.Approved; DecidedAtUtc = UpdatedAtUtc = now; Version++; }
    public void Reject(string actor, string reason, DateTimeOffset now) { if (Status != QaReleaseOverrideStatus.Requested) throw new InvalidOperationException("Only requested overrides can be rejected."); ApprovedBy = Text(actor, 200); DecisionReason = Text(reason, 2000); Status = QaReleaseOverrideStatus.Rejected; DecidedAtUtc = UpdatedAtUtc = now; Version++; }
    public void Revoke(string actor, string reason, DateTimeOffset now) { if (Status != QaReleaseOverrideStatus.Approved) throw new InvalidOperationException("Only approved overrides can be revoked."); ApprovedBy = Text(actor, 200); DecisionReason = Text(reason, 2000); Status = QaReleaseOverrideStatus.Revoked; DecidedAtUtc = UpdatedAtUtc = now; Version++; }
    public void Expire(DateTimeOffset now) { if (Status == QaReleaseOverrideStatus.Approved && now >= ExpiresAtUtc) { Status = QaReleaseOverrideStatus.Expired; UpdatedAtUtc = now; } }
    private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException("Guid is required.") : value;
    private static string Text(string? value, int max) { var v = value?.Trim() ?? string.Empty; if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"Text must be 1-{max} characters."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException($"Text must be <= {max} characters."); return v; }
}

public sealed class QaBackupVerificationRecord
{
    private QaBackupVerificationRecord() { }
    public QaBackupVerificationRecord(Guid id, Guid projectId, string backupKind, string backupReference, string sourceCommit, string environmentFingerprint, QaBackupVerificationStatus status, string detail, DateTimeOffset now)
    { Id=id==Guid.Empty?Guid.NewGuid():id; ProjectId=projectId; BackupKind=T(backupKind,80); BackupReference=T(backupReference,1000); SourceCommit=T(sourceCommit,64); EnvironmentFingerprint=T(environmentFingerprint,128); Status=status; Detail=T(detail,4000); VerifiedAtUtc=now; }
    public Guid Id{get;private set;} public Guid ProjectId{get;private set;} public string BackupKind{get;private set;}=string.Empty; public string BackupReference{get;private set;}=string.Empty; public string SourceCommit{get;private set;}=string.Empty; public string EnvironmentFingerprint{get;private set;}=string.Empty; public QaBackupVerificationStatus Status{get;private set;} public string Detail{get;private set;}=string.Empty; public DateTimeOffset VerifiedAtUtc{get;private set;}
    private static string T(string? value,int max){var v=value?.Trim()??string.Empty;if(v.Length is<1||v.Length>max)throw new ArgumentException();return v;}
}
