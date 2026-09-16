// Purpose: Defines the ProjectTruthAssessment application contract.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record ProjectTruthAssessment(
    ProjectTruthStatus TruthStatus,
    ProjectRiskLevel RiskLevel,
    decimal HealthScore,
    ProgressAssessment Progress,
    EvidenceConfidence EvidenceConfidence,
    IReadOnlyList<ClaimVerificationAssessment> ClaimAssessments,
    IReadOnlyList<ProjectBlocker> Blockers,
    int ContradictionCount,
    int UnverifiedClaimCount,
    string Summary,
    IReadOnlyList<string> TopActions);
