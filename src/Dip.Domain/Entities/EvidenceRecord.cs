using System.Text.Json;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>
/// A machine-observed or human-supplied fact that can be used to evaluate the
/// current state of a project. Evidence never labels a person as truthful or
/// untruthful; it only records what was observed, by which source, and with
/// what confidence.
/// </summary>
public sealed class EvidenceRecord
{
    private EvidenceRecord() { }

    public EvidenceRecord(
        Guid id,
        Guid projectId,
        Guid? pipelineRunId,
        string subjectKey,
        EvidenceSourceType sourceType,
        string evidenceKind,
        EvidenceOutcome outcome,
        string title,
        string summary,
        EvidenceConfidence confidence,
        string? sourceReference,
        string? payloadJson,
        DateTimeOffset observedAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        if (pipelineRunId == Guid.Empty)
            throw new ArgumentException("PipelineRunId cannot be empty.", nameof(pipelineRunId));

        if (!Enum.IsDefined(sourceType))
            throw new ArgumentOutOfRangeException(nameof(sourceType));

        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));

        if (!Enum.IsDefined(confidence))
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        PipelineRunId = pipelineRunId;
        SubjectKey = Required(subjectKey, 200, nameof(subjectKey));
        SourceType = sourceType;
        EvidenceKind = Required(evidenceKind, 120, nameof(evidenceKind));
        Outcome = outcome;
        Title = Required(title, 200, nameof(title));
        Summary = Required(summary, 4000, nameof(summary));
        Confidence = confidence;
        SourceReference = Optional(sourceReference, 1000, nameof(sourceReference));
        PayloadJson = Optional(payloadJson, 16000, nameof(payloadJson));
        ObservedAtUtc = observedAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? PipelineRunId { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public EvidenceSourceType SourceType { get; private set; }
    public string EvidenceKind { get; private set; } = string.Empty;
    public EvidenceOutcome Outcome { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public EvidenceConfidence Confidence { get; private set; }
    public string? SourceReference { get; private set; }
    public string? PayloadJson { get; private set; }
    public DateTimeOffset ObservedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Best-effort lookup for provider/domain metadata embedded in PayloadJson.
    /// PayloadJson remains backward compatible: non-JSON historical payloads are
    /// allowed and simply return null here.
    /// </summary>
    public string? PayloadValue(string name)
    {
        if(string.IsNullOrWhiteSpace(PayloadJson)||string.IsNullOrWhiteSpace(name))return null;
        try
        {
            using var doc=JsonDocument.Parse(PayloadJson);
            if(doc.RootElement.ValueKind!=JsonValueKind.Object)return null;
            foreach(var property in doc.RootElement.EnumerateObject())
            {
                if(!property.Name.Equals(name,StringComparison.OrdinalIgnoreCase))continue;
                return property.Value.ValueKind==JsonValueKind.String
                    ?property.Value.GetString()
                    :property.Value.GetRawText();
            }
        }
        catch{}
        return null;
    }

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
