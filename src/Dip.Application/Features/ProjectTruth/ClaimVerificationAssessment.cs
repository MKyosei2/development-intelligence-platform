// Purpose: Defines the ClaimVerificationAssessment application contract.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record ClaimVerificationAssessment(
    Guid ClaimId,
    ClaimVerificationStatus Status,
    string Summary,
    IReadOnlyList<ClaimEvidenceAssessment> EvidenceLinks);
