// Purpose: Defines Asset QA Finding Record domain behavior and invariants for artifacts.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class AssetQaFindingRecord
{
    private AssetQaFindingRecord() { }

    public AssetQaFindingRecord(
        Guid id,
        Guid projectId,
        Guid evidenceId,
        string source,
        string ruleId,
        AssetQaSeverity severity,
        string title,
        string message,
        string? relativePath,
        string fingerprint,
        string metadataJson,
        DateTimeOffset observedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (evidenceId == Guid.Empty) throw new ArgumentException("EvidenceId is required.", nameof(evidenceId));
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        EvidenceId = evidenceId;
        Source = Required(source, 80, nameof(source));
        RuleId = Required(ruleId, 160, nameof(ruleId));
        Severity = severity;
        Title = Required(title, 500, nameof(title));
        Message = Required(message, 4000, nameof(message));
        RelativePath = Optional(relativePath, 1000, nameof(relativePath));
        Fingerprint = Required(fingerprint, 128, nameof(fingerprint));
        MetadataJson = Required(metadataJson, 32000, nameof(metadataJson));
        State = AssetQaFindingState.Open;
        ObservedAtUtc = observedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid EvidenceId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string RuleId { get; private set; } = string.Empty;
    public AssetQaSeverity Severity { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? RelativePath { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public string MetadataJson { get; private set; } = "{}";
    public AssetQaFindingState State { get; private set; }
    public DateTimeOffset ObservedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public void Resolve(DateTimeOffset now) { State = AssetQaFindingState.Resolved; ResolvedAtUtc = now; }
    public void Suppress(DateTimeOffset now) { State = AssetQaFindingState.Suppressed; ResolvedAtUtc = now; }

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
