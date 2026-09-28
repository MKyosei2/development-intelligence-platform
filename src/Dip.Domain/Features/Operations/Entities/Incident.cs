// Purpose: Defines Incident domain behavior and invariants for operations.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class Incident
{
    private Incident() { }
    public Incident(Guid id, Guid projectId, string fingerprint, string subjectKey, string title, Severity severity, DateTimeOffset firstSeenAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        Fingerprint = Required(fingerprint, 128, nameof(fingerprint));
        SubjectKey = Required(subjectKey, 200, nameof(subjectKey));
        Title = Required(title, 240, nameof(title)); Severity = severity; Status = IncidentStatus.Open; FirstSeenAtUtc = LastSeenAtUtc = firstSeenAtUtc; OccurrenceCount = 0;
    }
    public Guid Id
    {
        get;
        private set;
    }
    public Guid ProjectId
    {
        get;
        private set;
    }
    public string Fingerprint
    {
        get;
        private set;
    } = string.Empty;
    public string SubjectKey
    {
        get;
        private set;
    } = string.Empty;
    public string Title { get; private set; } = string.Empty; public Severity Severity { get; private set; }
    public IncidentStatus Status { get; private set; }
    public DateTimeOffset FirstSeenAtUtc
    {
        get;
        private set;
    }
    public DateTimeOffset LastSeenAtUtc
    {
        get;
        private set;
    }
    public int OccurrenceCount
    {
        get;
        private set;
    }
    public int ReproducedCount
    {
        get;
        private set;
    }
    public string? SuspectedRegressionCommit
    {
        get;
        private set;
    }
    public string? FixedCommit
    {
        get;
        private set;
    }
    public string? RootCauseSummary
    {
        get;
        private set;
    }
    public DateTimeOffset? ResolvedAtUtc
    {
        get;
        private set;
    }
    public void Observe(Severity severity, DateTimeOffset at, bool reproduced)
    {
        if (Status == IncidentStatus.Resolved) { Status = IncidentStatus.Reopened; ResolvedAtUtc = null; }
        if (severity > Severity) Severity = severity; LastSeenAtUtc = at > LastSeenAtUtc ? at : LastSeenAtUtc; OccurrenceCount++; if (reproduced) ReproducedCount++;
    }
    public void MarkReproduced() { ReproducedCount++; }
    public void SetRegressionCommit(string commit) { SuspectedRegressionCommit = Required(commit, 64, nameof(commit)); Status = IncidentStatus.Investigating; }
    public void SetRootCause(string summary) { RootCauseSummary = Required(summary, 4000, nameof(summary)); Status = IncidentStatus.Investigating; }
    public void Resolve(string? fixedCommit, DateTimeOffset at) { FixedCommit = Optional(fixedCommit, 64); Status = IncidentStatus.Resolved; ResolvedAtUtc = at; }
    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
