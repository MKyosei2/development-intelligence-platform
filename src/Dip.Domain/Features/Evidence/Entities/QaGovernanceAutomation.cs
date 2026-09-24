// Purpose: Defines QA test-impact, retention, execution-cost, and bug-minimization governance records.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class QaTestImpactRule
{
    private QaTestImpactRule() { }

    public QaTestImpactRule(
        Guid id,
        Guid projectId,
        string pathPattern,
        string testKey,
        string commandJson,
        int priority,
        bool enabled,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        PathPattern = Text(pathPattern, 1000);
        TestKey = Text(testKey, 500);
        CommandJson = Text(commandJson, 8000);
        Priority = Math.Clamp(priority, -10000, 10000);
        Enabled = enabled;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string PathPattern { get; private set; } = string.Empty;
    public string TestKey { get; private set; } = string.Empty;
    public string CommandJson { get; private set; } = "{}";
    public int Priority { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}

public sealed class QaArtifactRetentionRule
{
    private QaArtifactRetentionRule() { }

    public QaArtifactRetentionRule(
        Guid id,
        Guid projectId,
        string kindPattern,
        string retentionClass,
        int retentionDays,
        bool preserveOnCritical,
        bool enabled,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        KindPattern = Text(kindPattern, 200);
        RetentionClass = Text(retentionClass, 40);
        RetentionDays = Math.Clamp(retentionDays, 1, 3650);
        PreserveOnCritical = preserveOnCritical;
        Enabled = enabled;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string KindPattern { get; private set; } = string.Empty;
    public string RetentionClass { get; private set; } = string.Empty;
    public int RetentionDays { get; private set; }
    public bool PreserveOnCritical { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}

public sealed class QaRunCostRecord
{
    private QaRunCostRecord() { }

    public QaRunCostRecord(
        Guid id,
        Guid projectId,
        Guid qaRunId,
        long wallDurationMs,
        long toolDurationMs,
        long artifactBytes,
        decimal estimatedCpuSeconds,
        string budgetVerdict,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        QaRunId = qaRunId;
        WallDurationMs = Math.Max(0, wallDurationMs);
        ToolDurationMs = Math.Max(0, toolDurationMs);
        ArtifactBytes = Math.Max(0, artifactBytes);
        EstimatedCpuSeconds = Math.Max(0, estimatedCpuSeconds);
        BudgetVerdict = budgetVerdict.Length <= 40
            ? budgetVerdict
            : budgetVerdict[..40];
        CapturedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid QaRunId { get; private set; }
    public long WallDurationMs { get; private set; }
    public long ToolDurationMs { get; private set; }
    public long ArtifactBytes { get; private set; }
    public decimal EstimatedCpuSeconds { get; private set; }
    public string BudgetVerdict { get; private set; } = string.Empty;
    public DateTimeOffset CapturedAtUtc { get; private set; }
}

public sealed class QaBugMinimizationRecord
{
    private QaBugMinimizationRecord() { }

    public QaBugMinimizationRecord(
        Guid id,
        Guid projectId,
        Guid findingId,
        string originalInput,
        string minimizedInput,
        int originalUnits,
        int minimizedUnits,
        QaMinimizationStatus status,
        string detail,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        FindingId = findingId;
        OriginalInput = Text(originalInput, 16000);
        MinimizedInput = Text(minimizedInput, 16000);
        OriginalUnits = Math.Max(0, originalUnits);
        MinimizedUnits = Math.Max(0, minimizedUnits);
        Status = status;
        Detail = Text(detail, 4000);
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid FindingId { get; private set; }
    public string OriginalInput { get; private set; } = string.Empty;
    public string MinimizedInput { get; private set; } = string.Empty;
    public int OriginalUnits { get; private set; }
    public int MinimizedUnits { get; private set; }
    public QaMinimizationStatus Status { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}
