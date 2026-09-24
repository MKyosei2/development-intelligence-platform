// Purpose: Defines durable QA finding lifecycle state, ownership, and transition invariants.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class QaFindingState
{
    private QaFindingState() { }

    public QaFindingState(
        Guid id,
        Guid projectId,
        string fingerprint,
        Guid findingId,
        string subjectKey,
        string? commitSha,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = Req(projectId);
        Fingerprint = Text(fingerprint, 64);
        LastFindingId = findingId;
        SubjectKey = Text(subjectKey, 200);
        LastCommitSha = Opt(commitSha, 64);
        Status = FindingLifecycleStatus.New;
        OwnershipSource = QaOwnershipSource.Unassigned;
        Occurrences = 1;
        Version = 1;
        FirstSeenAtUtc = LastSeenAtUtc = UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public Guid LastFindingId { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public string? LastCommitSha { get; private set; }
    public FindingLifecycleStatus Status { get; private set; }
    public int Occurrences { get; private set; }
    public string? Team { get; private set; }
    public string? Workstream { get; private set; }
    public QaOwnershipSource OwnershipSource { get; private set; }
    public string? DispositionReason { get; private set; }
    public string? UpdatedBy { get; private set; }
    public DateTimeOffset FirstSeenAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    public void Observe(Guid findingId, string? commitSha, DateTimeOffset now)
    {
        LastFindingId = findingId;
        LastCommitSha = Opt(commitSha, 64);
        Occurrences++;
        Version++;
        LastSeenAtUtc = UpdatedAtUtc = now;

        if (Status == FindingLifecycleStatus.New && Occurrences >= 2)
            Status = FindingLifecycleStatus.Confirmed;

        if (Status is FindingLifecycleStatus.Fixed
            or FindingLifecycleStatus.Verified
            or FindingLifecycleStatus.Closed)
        {
            Status = FindingLifecycleStatus.Confirmed;
        }
    }

    public void Transition(
        FindingLifecycleStatus status,
        string reason,
        string actor,
        DateTimeOffset now)
    {
        if (status != Status && !CanTransition(Status, status))
        {
            throw new InvalidOperationException(
                $"Finding lifecycle transition {Status} -> {status} is not allowed.");
        }

        Status = status;
        DispositionReason = Text(reason, 2000);
        UpdatedBy = Text(actor, 200);
        Version++;
        UpdatedAtUtc = now;
    }

    public void Assign(
        string? team,
        string? workstream,
        QaOwnershipSource source,
        DateTimeOffset now)
    {
        Team = Opt(team, 200);
        Workstream = Opt(workstream, 200);
        OwnershipSource = source;
        if (Status is FindingLifecycleStatus.New or FindingLifecycleStatus.Confirmed)
            Status = FindingLifecycleStatus.Assigned;
        Version++;
        UpdatedAtUtc = now;
    }

    private static bool CanTransition(
        FindingLifecycleStatus from,
        FindingLifecycleStatus to) => from switch
    {
        FindingLifecycleStatus.New => to is
            FindingLifecycleStatus.Confirmed
            or FindingLifecycleStatus.Assigned
            or FindingLifecycleStatus.InProgress
            or FindingLifecycleStatus.Fixed
            or FindingLifecycleStatus.Suppressed
            or FindingLifecycleStatus.AcceptedRisk
            or FindingLifecycleStatus.Duplicate
            or FindingLifecycleStatus.WontFix,

        FindingLifecycleStatus.Confirmed => to is
            FindingLifecycleStatus.Assigned
            or FindingLifecycleStatus.InProgress
            or FindingLifecycleStatus.Fixed
            or FindingLifecycleStatus.Suppressed
            or FindingLifecycleStatus.AcceptedRisk
            or FindingLifecycleStatus.Duplicate
            or FindingLifecycleStatus.WontFix
            or FindingLifecycleStatus.Closed,

        FindingLifecycleStatus.Assigned => to is
            FindingLifecycleStatus.Confirmed
            or FindingLifecycleStatus.InProgress
            or FindingLifecycleStatus.Fixed
            or FindingLifecycleStatus.Suppressed
            or FindingLifecycleStatus.AcceptedRisk
            or FindingLifecycleStatus.Duplicate
            or FindingLifecycleStatus.WontFix
            or FindingLifecycleStatus.Closed,

        FindingLifecycleStatus.InProgress => to is
            FindingLifecycleStatus.Confirmed
            or FindingLifecycleStatus.Fixed
            or FindingLifecycleStatus.Suppressed
            or FindingLifecycleStatus.AcceptedRisk
            or FindingLifecycleStatus.WontFix,

        FindingLifecycleStatus.Fixed => to is
            FindingLifecycleStatus.Verified
            or FindingLifecycleStatus.Confirmed
            or FindingLifecycleStatus.InProgress,

        FindingLifecycleStatus.Verified => to is
            FindingLifecycleStatus.Closed
            or FindingLifecycleStatus.Confirmed,

        FindingLifecycleStatus.Closed => to is FindingLifecycleStatus.Confirmed,

        FindingLifecycleStatus.Suppressed => to is
            FindingLifecycleStatus.Confirmed
            or FindingLifecycleStatus.Fixed
            or FindingLifecycleStatus.Closed,

        FindingLifecycleStatus.AcceptedRisk => to is
            FindingLifecycleStatus.Confirmed
            or FindingLifecycleStatus.Closed,

        FindingLifecycleStatus.Duplicate => to is
            FindingLifecycleStatus.Confirmed
            or FindingLifecycleStatus.Closed,

        FindingLifecycleStatus.WontFix => to is
            FindingLifecycleStatus.Confirmed
            or FindingLifecycleStatus.Closed,

        _ => false
    };

    private static Guid Req(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("Guid is required.")
            : value;

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException($"Text must be 1-{maximumLength} characters.");
        return normalized;
    }

    private static string? Opt(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Text must be <= {maximumLength} characters.");
        return normalized;
    }
}
