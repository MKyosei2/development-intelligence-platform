// Purpose: Defines domain behavior and state for this feature area.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class PolicyProfile
{
    private PolicyProfile() { }

    public PolicyProfile(
        Guid id,
        PolicyScopeType scopeType,
        Guid? projectId,
        string? agentId,
        string name,
        string evidencePolicyJson,
        string agentPolicyJson,
        string retentionPolicyJson,
        string alertPolicyJson,
        string privacyPolicyJson,
        Guid? qaPolicyId,
        bool enabled,
        DateTimeOffset now)
    {
        ValidateScope(scopeType, projectId, agentId);
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ScopeType = scopeType;
        ProjectId = projectId;
        AgentId = Optional(agentId, 300, nameof(agentId));
        Name = Required(name, 200, nameof(name));
        EvidencePolicyJson = Json(evidencePolicyJson, nameof(evidencePolicyJson));
        AgentPolicyJson = Json(agentPolicyJson, nameof(agentPolicyJson));
        RetentionPolicyJson = Json(retentionPolicyJson, nameof(retentionPolicyJson));
        AlertPolicyJson = Json(alertPolicyJson, nameof(alertPolicyJson));
        PrivacyPolicyJson = Json(privacyPolicyJson, nameof(privacyPolicyJson));
        QaPolicyId = qaPolicyId;
        Enabled = enabled;
        Version = 1;
        CreatedAtUtc = UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public PolicyScopeType ScopeType { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? AgentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public bool Enabled { get; private set; }
    public string EvidencePolicyJson { get; private set; } = "{}";
    public string AgentPolicyJson { get; private set; } = "{}";
    public string RetentionPolicyJson { get; private set; } = "{}";
    public string AlertPolicyJson { get; private set; } = "{}";
    public string PrivacyPolicyJson { get; private set; } = "{}";
    public Guid? QaPolicyId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string evidencePolicyJson,
        string agentPolicyJson,
        string retentionPolicyJson,
        string alertPolicyJson,
        string privacyPolicyJson,
        Guid? qaPolicyId,
        bool enabled,
        DateTimeOffset now)
    {
        Name = Required(name, 200, nameof(name));
        EvidencePolicyJson = Json(evidencePolicyJson, nameof(evidencePolicyJson));
        AgentPolicyJson = Json(agentPolicyJson, nameof(agentPolicyJson));
        RetentionPolicyJson = Json(retentionPolicyJson, nameof(retentionPolicyJson));
        AlertPolicyJson = Json(alertPolicyJson, nameof(alertPolicyJson));
        PrivacyPolicyJson = Json(privacyPolicyJson, nameof(privacyPolicyJson));
        QaPolicyId = qaPolicyId;
        Enabled = enabled;
        Version++;
        UpdatedAtUtc = now;
    }

    private static void ValidateScope(PolicyScopeType scope, Guid? projectId, string? agentId)
    {
        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope));
        if (scope == PolicyScopeType.Global && (projectId.HasValue || !string.IsNullOrWhiteSpace(agentId)))
            throw new ArgumentException("Global policy cannot target a project or agent.");
        if (scope == PolicyScopeType.Project && (!projectId.HasValue || projectId.Value == Guid.Empty))
            throw new ArgumentException("Project policy requires ProjectId.");
        if (scope == PolicyScopeType.Agent && string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("Agent policy requires AgentId.");
    }

    private static string Required(string? value, int max, string name)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > max)
            throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return normalized;
    }

    private static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > max)
            throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return normalized;
    }

    private static string Json(string? value, string name)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "{}" : value.Trim();
        if (normalized.Length > 32000)
            throw new ArgumentException($"{name} must be at most 32000 characters.", name);
        try { using var _ = System.Text.Json.JsonDocument.Parse(normalized); }
        catch (System.Text.Json.JsonException exception)
        {
            throw new ArgumentException($"{name} must be valid JSON.", name, exception);
        }
        return normalized;
    }
}
