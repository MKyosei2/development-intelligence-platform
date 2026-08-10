using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record ClaimEvidenceAssessment(
    Guid EvidenceId,
    EvidenceRelation Relation,
    string Reason);

public sealed record ClaimVerificationAssessment(
    Guid ClaimId,
    ClaimVerificationStatus Status,
    string Summary,
    IReadOnlyList<ClaimEvidenceAssessment> EvidenceLinks);

public sealed record ProgressAssessment(
    decimal VerifiedProgress,
    decimal? ReportedProgress);

public sealed record ProjectBlocker(
    string Code,
    string Severity,
    string Title,
    string Detail,
    string SubjectKey,
    Guid? EvidenceId);

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
