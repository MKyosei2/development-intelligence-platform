// Purpose: Defines A ud it Lo g domain behavior and invariants for o pe ra ti on s.
namespace Dip.Domain.Entities;

/// <summary>
/// Immutable audit trail for changes that matter to project-state interpretation.
/// It records actions and timestamps; it is not a performance or trust score for people.
/// </summary>
public sealed class AuditLog
{
    private AuditLog() { }

    public AuditLog(
        Guid id,
        Guid projectId,
        string category,
        string action,
        string actor,
        string? subjectKey,
        string? detailJson,
        DateTimeOffset createdAtUtc,
        string? requestId = null,
        string? correlationId = null,
        string? reason = null)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        Category = Required(category, 80, nameof(category));
        Action = Required(action, 120, nameof(action));
        Actor = Required(actor, 200, nameof(actor));
        SubjectKey = Optional(subjectKey, 200, nameof(subjectKey));
        DetailJson = Optional(detailJson, 16000, nameof(detailJson));
        RequestId = Optional(requestId, 200, nameof(requestId));
        CorrelationId = Optional(correlationId, 200, nameof(correlationId));
        Reason = Optional(reason, 2000, nameof(reason));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public string? SubjectKey { get; private set; }
    public string? DetailJson { get; private set; }
    public string? RequestId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Required(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} must be 1-{maxLength} characters.", parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} must be at most {maxLength} characters.", parameterName);
        return normalized;
    }
}
