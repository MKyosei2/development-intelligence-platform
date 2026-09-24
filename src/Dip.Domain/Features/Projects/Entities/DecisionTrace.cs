// Purpose: Defines D ec is io nT ra ce domain behavior and invariants for p ro je ct s.
namespace Dip.Domain.Entities;

public sealed class DecisionTrace
{
    private DecisionTrace() { }

    public DecisionTrace(
        Guid id, Guid projectId, string decisionType, string subjectKey,
        string previousValue, string newValue, string reason,
        string evidenceIdsJson, string detailJson, DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id; ProjectId = projectId;
        DecisionType = Required(decisionType, 120, nameof(decisionType));
        SubjectKey = Required(subjectKey, 200, nameof(subjectKey));
        PreviousValue = Required(previousValue, 500, nameof(previousValue));
        NewValue = Required(newValue, 500, nameof(newValue));
        Reason = Required(reason, 4000, nameof(reason));
        EvidenceIdsJson = Required(evidenceIdsJson, 16000, nameof(evidenceIdsJson));
        DetailJson = Required(detailJson, 32000, nameof(detailJson));
        CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string DecisionType { get; private set; } = string.Empty;
    public string SubjectKey { get; private set; } = string.Empty;
    public string PreviousValue { get; private set; } = string.Empty;
    public string NewValue { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string EvidenceIdsJson { get; private set; } = string.Empty;
    public string DetailJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
}
