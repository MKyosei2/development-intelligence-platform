// Purpose: Defines P2 In te ll ig en ce Re co rd domain behavior and invariants for e vi de nc e.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>
/// Stores a durable P2 analysis result. The record is decision-support evidence,
/// not a replacement for Project Truth and never scores a person.
/// </summary>
public sealed class P2IntelligenceRecord
{
    private P2IntelligenceRecord() { }

    public P2IntelligenceRecord(
        Guid id,
        Guid projectId,
        string subjectKey,
        P2IntelligenceKind kind,
        P2IntelligenceStatus status,
        decimal? score,
        EvidenceConfidence confidence,
        string summary,
        string payloadJson,
        string? sourceReference,
        DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (!Enum.IsDefined(confidence)) throw new ArgumentOutOfRangeException(nameof(confidence));
        if (score is < 0m or > 100m) throw new ArgumentOutOfRangeException(nameof(score));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        SubjectKey = Required(subjectKey, 200, nameof(subjectKey));
        Kind = kind;
        Status = status;
        Score = score is null ? null : decimal.Round(score.Value, 2);
        Confidence = confidence;
        Summary = Required(summary, 4000, nameof(summary));
        PayloadJson = Required(payloadJson, 32000, nameof(payloadJson));
        SourceReference = Optional(sourceReference, 1000, nameof(sourceReference));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public P2IntelligenceKind Kind { get; private set; }
    public P2IntelligenceStatus Status { get; private set; }
    public decimal? Score { get; private set; }
    public EvidenceConfidence Confidence { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public string? SourceReference { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

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
