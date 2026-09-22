// Purpose: Defines domain behavior and state for this feature area.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class ChangeRiskAssessment
{
    private ChangeRiskAssessment() { }
    public ChangeRiskAssessment(Guid id, Guid projectId, string commitSha, decimal riskScore, ChangeRiskLevel riskLevel, string reasonsJson, string affectedRepositoriesJson, DateTimeOffset assessedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        CommitSha = Required(commitSha, 64, nameof(commitSha));
        RiskScore = Math.Clamp(riskScore, 0m, 100m);
        RiskLevel = riskLevel;
        ReasonsJson = Required(reasonsJson, 16000, nameof(reasonsJson));
        AffectedRepositoriesJson = Required(affectedRepositoriesJson, 16000, nameof(affectedRepositoriesJson));
        AssessedAtUtc = assessedAtUtc;
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
    public decimal RiskScore
    {
        get;
        private set;
    }
    public ChangeRiskLevel RiskLevel
    {
        get;
        private set;
    }
    public string ReasonsJson
    {
        get;
        private set;
    } = "[]";
    public string AffectedRepositoriesJson
    {
        get;
        private set;
    } = "[]";
    public DateTimeOffset AssessedAtUtc
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
