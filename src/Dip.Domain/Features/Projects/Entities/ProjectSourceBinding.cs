// Purpose: Defines P ro je ct So ur ce Bi nd in g domain behavior and invariants for p ro je ct s.
namespace Dip.Domain.Entities;

public sealed class ProjectSourceBinding
{
    private ProjectSourceBinding() { }

    public ProjectSourceBinding(
        Guid id,
        Guid projectId,
        Guid connectionId,
        string externalResourceId,
        string externalResourceName,
        string resourceType,
        string? configurationJson,
        DateTimeOffset now)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (connectionId == Guid.Empty)
            throw new ArgumentException("ConnectionId is required.", nameof(connectionId));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        ConnectionId = connectionId;
        ExternalResourceId = Required(externalResourceId, 300, nameof(externalResourceId));
        ExternalResourceName = Required(externalResourceName, 500, nameof(externalResourceName));
        ResourceType = Required(resourceType, 80, nameof(resourceType));
        ConfigurationJson = Optional(configurationJson, 16000, nameof(configurationJson));
        Enabled = true;
        CreatedAtUtc = UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public string ExternalResourceId { get; private set; } = string.Empty;
    public string ExternalResourceName { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public string? ConfigurationJson { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset? LastSyncedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void RecordSync(DateTimeOffset now) => LastSyncedAtUtc = UpdatedAtUtc = now;
    public void SetEnabled(bool enabled, DateTimeOffset now)
    {
        Enabled = enabled;
        UpdatedAtUtc = now;
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
}
