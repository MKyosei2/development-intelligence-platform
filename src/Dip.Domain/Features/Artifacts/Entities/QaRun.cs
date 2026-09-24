// Purpose: Defines Q aR un domain behavior and invariants for a rt if ac ts.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Domain.Entities;

public sealed class QaRun
{
    private QaRun() { }
    public QaRun(Guid id, Guid projectId, string subjectKey, LanguageId language, string workspaceRelativePath, string? commitSha, string artifactRoot, DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(language.Value)) throw new ArgumentException("Language is required.", nameof(language));
        Id = id == Guid.Empty ? Guid.NewGuid() : id; ProjectId = projectId; SubjectKey = Required(subjectKey, 200, nameof(subjectKey)); Language = language;
        WorkspaceRelativePath = Required(workspaceRelativePath, 500, nameof(workspaceRelativePath)); CommitSha = Optional(commitSha, 64, nameof(commitSha));
        ArtifactRoot = Required(artifactRoot, 1000, nameof(artifactRoot)); Status = QaRunStatus.Queued; CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public LanguageId Language { get; private set; }
    public string WorkspaceRelativePath { get; private set; } = string.Empty; public string? CommitSha { get; private set; }
    public string ArtifactRoot { get; private set; } = string.Empty; public QaRunStatus Status { get; private set; }
    public int FindingCount { get; private set; }
    public int CriticalFindingCount { get; private set; }
    public int WarningFindingCount { get; private set; }
    public string? SummaryJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? FinishedAtUtc { get; private set; }
    public void Start(DateTimeOffset at)
    {
        if (Status != QaRunStatus.Queued) throw new InvalidOperationException($"Cannot start QA run from {Status}.");
        Status = QaRunStatus.Running;
        StartedAtUtc = at;
    }
    public void Complete(QaRunStatus status, int findings, int critical, int warning, string summaryJson, DateTimeOffset at)
    {
        if (Status != QaRunStatus.Running) throw new InvalidOperationException($"Cannot complete QA run from {Status}.");
        if (status is not (QaRunStatus.Succeeded or QaRunStatus.Failed or QaRunStatus.Partial or QaRunStatus.InfrastructureFailure or QaRunStatus.Cancelled)) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        FindingCount = Math.Max(0, findings);
        CriticalFindingCount = Math.Max(0, critical);
        WarningFindingCount = Math.Max(0, warning);
        SummaryJson = Optional(summaryJson, 32000, nameof(summaryJson));
        FinishedAtUtc = at;
    }
    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
    private static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.Length > max) throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return text;
    }
}
