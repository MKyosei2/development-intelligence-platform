// Purpose: Represents C la im Ev ID en ce As se ss me nt Re co rd in the DIP domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class ClaimEvidenceAssessmentRecord
{
    private ClaimEvidenceAssessmentRecord() { }

    public ClaimEvidenceAssessmentRecord(
        Guid id,
        Guid projectId,
        Guid claimId,
        ClaimEvidenceAssessmentStatus status,
        decimal discrepancyScore,
        EvidenceConfidence confidence,
        string summary,
        string supportingEvidenceJson,
        string contradictingEvidenceJson,
        string reasonCodesJson,
        DateTimeOffset assessedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (claimId == Guid.Empty) throw new ArgumentException("ClaimId is required.", nameof(claimId));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (!Enum.IsDefined(confidence)) throw new ArgumentOutOfRangeException(nameof(confidence));
        if (discrepancyScore is < 0m or > 100m) throw new ArgumentOutOfRangeException(nameof(discrepancyScore));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        ClaimId = claimId;
        Status = status;
        DiscrepancyScore = discrepancyScore;
        Confidence = confidence;
        Summary = Required(summary, 4000, nameof(summary));
        SupportingEvidenceJson = Required(supportingEvidenceJson, 32000, nameof(supportingEvidenceJson));
        ContradictingEvidenceJson = Required(contradictingEvidenceJson, 32000, nameof(contradictingEvidenceJson));
        ReasonCodesJson = Required(reasonCodesJson, 16000, nameof(reasonCodesJson));
        AssessedAtUtc = assessedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ClaimId { get; private set; }
    public ClaimEvidenceAssessmentStatus Status { get; private set; }
    public decimal DiscrepancyScore { get; private set; }
    public EvidenceConfidence Confidence { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string SupportingEvidenceJson { get; private set; } = "[]";
    public string ContradictingEvidenceJson { get; private set; } = "[]";
    public string ReasonCodesJson { get; private set; } = "[]";
    public DateTimeOffset AssessedAtUtc { get; private set; }

    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
}
