// Purpose: Defines domain behavior and state for this feature area.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class ReleaseReadinessSnapshot
{
    private ReleaseReadinessSnapshot() { }
    public ReleaseReadinessSnapshot(Guid id, Guid projectId, string commitSha, ReleaseDecision decision, decimal confidence, string blockersJson, string warningsJson, string requiredActionsJson, string summary, DateTimeOffset evaluatedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        CommitSha = Required(commitSha, 64, nameof(commitSha));
        Decision = decision;
        Confidence = Math.Clamp(confidence, 0m, 100m);
        BlockersJson = Required(blockersJson, 16000, nameof(blockersJson));
        WarningsJson = Required(warningsJson, 16000, nameof(warningsJson));
        RequiredActionsJson = Required(requiredActionsJson, 16000, nameof(requiredActionsJson));
        Summary = Required(summary, 4000, nameof(summary));
        EvaluatedAtUtc = evaluatedAtUtc;
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
    public string CommitSha
    {
        get;
        private set;
    } = string.Empty;
    public ReleaseDecision Decision
    {
        get;
        private set;
    }
    public decimal Confidence
    {
        get;
        private set;
    }
    public string BlockersJson
    {
        get;
        private set;
    } = "[]";
    public string WarningsJson
    {
        get;
        private set;
    } = "[]";
    public string RequiredActionsJson
    {
        get;
        private set;
    } = "[]";
    public string Summary
    {
        get;
        private set;
    } = string.Empty;
    public DateTimeOffset EvaluatedAtUtc
    {
        get;
        private set;
    }
    private static string Required(string? value, int max, string name)
    {
        var v = value?.Trim() ?? string.Empty;
        if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return v;
    }
}
