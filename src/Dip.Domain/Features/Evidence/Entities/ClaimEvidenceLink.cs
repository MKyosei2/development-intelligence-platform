// Purpose: Represents C la im Ev ID en ce Li nk in the DIP domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class ClaimEvidenceLink
{
    private ClaimEvidenceLink() { }

    public ClaimEvidenceLink(
        Guid id,
        Guid claimId,
        Guid evidenceRecordId,
        EvidenceRelation relation,
        string? reason,
        DateTimeOffset createdAtUtc)
    {
        if (claimId == Guid.Empty)
            throw new ArgumentException("ClaimId is required.", nameof(claimId));
        if (evidenceRecordId == Guid.Empty)
            throw new ArgumentException("EvidenceRecordId is required.", nameof(evidenceRecordId));
        if (!Enum.IsDefined(relation))
            throw new ArgumentOutOfRangeException(nameof(relation));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ClaimId = claimId;
        EvidenceRecordId = evidenceRecordId;
        Relation = relation;
        Reason = Optional(reason, 1000, nameof(reason));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public Guid EvidenceRecordId { get; private set; }
    public EvidenceRelation Relation { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

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
