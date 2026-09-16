// Purpose: Defines the ClaimEvidenceAssessment application contract.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record ClaimEvidenceAssessment(
    Guid EvidenceId,
    EvidenceRelation Relation,
    string Reason);
