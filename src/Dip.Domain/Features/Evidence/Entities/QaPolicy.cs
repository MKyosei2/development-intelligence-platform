// Purpose: Defines QA ownership rules, policy lifecycle, and persisted release-gate evaluations.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class QaOwnershipRule
{
    private QaOwnershipRule() { }

    public QaOwnershipRule(
        Guid id,
        Guid projectId,
        string name,
        string pathPattern,
        string team,
        string? workstream,
        int priority,
        bool enabled,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        Name = Text(name, 200);
        PathPattern = Text(pathPattern, 1000);
        Team = Text(team, 200);
        Workstream = Optional(workstream, 200);
        Priority = Math.Clamp(priority, -10000, 10000);
        Enabled = enabled;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string PathPattern { get; private set; } = string.Empty;
    public string Team { get; private set; } = string.Empty;
    public string? Workstream { get; private set; }
    public int Priority { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

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

public sealed class QaPolicy
{
    private QaPolicy() { }

    public QaPolicy(
        Guid id,
        Guid projectId,
        string name,
        string version,
        string rulesJson,
        bool enabled,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        Name = Text(name, 200);
        Version = Text(version, 80);
        RulesJson = Text(rulesJson, 32000);
        Enabled = enabled;
        LifecycleStatus = enabled
            ? QaPolicyLifecycleStatus.Active
            : QaPolicyLifecycleStatus.Draft;
        Revision = 1;
        ConcurrencyVersion = 1;
        CreatedAtUtc = UpdatedAtUtc = now;
        if (enabled)
            ActivatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string RulesJson { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public QaPolicyLifecycleStatus LifecycleStatus { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesPolicyId { get; private set; }
    public string? ApprovedBy { get; private set; }
    public string? ActivatedBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? ActivatedAtUtc { get; private set; }
    public long ConcurrencyVersion { get; private set; }

    public void Approve(string actor, DateTimeOffset now)
    {
        if (LifecycleStatus != QaPolicyLifecycleStatus.Draft)
            throw new InvalidOperationException("Only draft policies can be approved.");
        ApprovedBy = Text(actor, 200);
        ApprovedAtUtc = UpdatedAtUtc = now;
        LifecycleStatus = QaPolicyLifecycleStatus.Approved;
        Enabled = false;
        ConcurrencyVersion++;
    }

    public void Activate(
        string actor,
        Guid? supersedesId,
        int revision,
        DateTimeOffset now)
    {
        if (LifecycleStatus != QaPolicyLifecycleStatus.Approved)
            throw new InvalidOperationException("Only approved policies can be activated.");
        ActivatedBy = Text(actor, 200);
        ActivatedAtUtc = UpdatedAtUtc = now;
        SupersedesPolicyId = supersedesId;
        Revision = Math.Max(1, revision);
        LifecycleStatus = QaPolicyLifecycleStatus.Active;
        Enabled = true;
        ConcurrencyVersion++;
    }

    public void Supersede(DateTimeOffset now)
    {
        LifecycleStatus = QaPolicyLifecycleStatus.Superseded;
        Enabled = false;
        UpdatedAtUtc = now;
        ConcurrencyVersion++;
    }

    public void Archive(DateTimeOffset now)
    {
        LifecycleStatus = QaPolicyLifecycleStatus.Archived;
        Enabled = false;
        UpdatedAtUtc = now;
        ConcurrencyVersion++;
    }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}

public sealed class QaPolicyEvaluation
{
    private QaPolicyEvaluation() { }

    public QaPolicyEvaluation(
        Guid id,
        Guid projectId,
        Guid qaRunId,
        Guid? policyId,
        string policyVersion,
        QaGateVerdict verdict,
        string blockersJson,
        string warningsJson,
        string summary,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        QaRunId = qaRunId;
        PolicyId = policyId;
        PolicyVersion = Text(policyVersion, 80);
        Verdict = verdict;
        BlockersJson = Text(blockersJson, 16000);
        WarningsJson = Text(warningsJson, 16000);
        Summary = Text(summary, 4000);
        EvaluatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid QaRunId { get; private set; }
    public Guid? PolicyId { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
    public QaGateVerdict Verdict { get; private set; }
    public string BlockersJson { get; private set; } = "[]";
    public string WarningsJson { get; private set; } = "[]";
    public string Summary { get; private set; } = string.Empty;
    public DateTimeOffset EvaluatedAtUtc { get; private set; }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}
