// Purpose: Defines QA evidence provenance, system-health observations, and quality-trend snapshots.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class QaEvidenceProvenance
{
    private QaEvidenceProvenance() { }

    public QaEvidenceProvenance(
        Guid id,
        Guid projectId,
        Guid qaRunId,
        string? commitSha,
        string environmentFingerprint,
        string toolchainJson,
        string normalizerVersion,
        string policyVersion,
        string inputHash,
        string evidenceHash,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        QaRunId = qaRunId;
        CommitSha = Optional(commitSha, 64);
        EnvironmentFingerprint = Text(environmentFingerprint, 128);
        ToolchainJson = Text(toolchainJson, 16000);
        NormalizerVersion = Text(normalizerVersion, 80);
        PolicyVersion = Text(policyVersion, 80);
        InputHash = Text(inputHash, 64);
        EvidenceHash = Text(evidenceHash, 64);
        CapturedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid QaRunId { get; private set; }
    public string? CommitSha { get; private set; }
    public string EnvironmentFingerprint { get; private set; } = string.Empty;
    public string ToolchainJson { get; private set; } = "{}";
    public string NormalizerVersion { get; private set; } = string.Empty;
    public string PolicyVersion { get; private set; } = string.Empty;
    public string InputHash { get; private set; } = string.Empty;
    public string EvidenceHash { get; private set; } = string.Empty;
    public DateTimeOffset CapturedAtUtc { get; private set; }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}

public sealed class QaSystemHealthSnapshot
{
    private QaSystemHealthSnapshot() { }

    public QaSystemHealthSnapshot(
        Guid id,
        Guid projectId,
        Guid? qaRunId,
        string component,
        QaSystemHealthStatus status,
        string detail,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        QaRunId = qaRunId;
        Component = Text(component, 120);
        Status = status;
        Detail = Text(detail, 4000);
        ObservedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? QaRunId { get; private set; }
    public string Component { get; private set; } = string.Empty;
    public QaSystemHealthStatus Status { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public DateTimeOffset ObservedAtUtc { get; private set; }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}

public sealed class QaQualityTrendSnapshot
{
    private QaQualityTrendSnapshot() { }

    public QaQualityTrendSnapshot(
        Guid id,
        Guid projectId,
        Guid qaRunId,
        int openFindings,
        int critical,
        int warning,
        int flaky,
        int activeWaivers,
        decimal debtScore,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        QaRunId = qaRunId;
        OpenFindingCount = Math.Max(0, openFindings);
        CriticalCount = Math.Max(0, critical);
        WarningCount = Math.Max(0, warning);
        FlakyCount = Math.Max(0, flaky);
        ActiveWaiverCount = Math.Max(0, activeWaivers);
        TechnicalDebtScore = Math.Clamp(debtScore, 0m, 1000m);
        CapturedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid QaRunId { get; private set; }
    public int OpenFindingCount { get; private set; }
    public int CriticalCount { get; private set; }
    public int WarningCount { get; private set; }
    public int FlakyCount { get; private set; }
    public int ActiveWaiverCount { get; private set; }
    public decimal TechnicalDebtScore { get; private set; }
    public DateTimeOffset CapturedAtUtc { get; private set; }
}
