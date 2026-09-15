using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Evidence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record ClaimEvidenceAssessmentDto(
    Guid Id,
    Guid ProjectId,
    Guid ClaimId,
    ClaimEvidenceAssessmentStatus Status,
    decimal DiscrepancyScore,
    EvidenceConfidence Confidence,
    string Summary,
    IReadOnlyList<Guid> SupportingEvidenceIds,
    IReadOnlyList<Guid> ContradictingEvidenceIds,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset AssessedAtUtc);

public sealed class ClaimEvidenceAssessmentService
{
    private readonly IProjectClaimRepository _claims;
    private readonly IEvidenceRepository _evidenceRepository;
    private readonly IClaimEvidenceAssessmentRepository _assessments;
    private readonly ClaimVerificationEvaluator _verifier;
    private readonly EvidenceIngestionService _evidence;
    private readonly ProductionGraphProjectionService _graph;
    private readonly ProjectTruthService _truth;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public ClaimEvidenceAssessmentService(
        IProjectClaimRepository claims,
        IEvidenceRepository evidenceRepository,
        IClaimEvidenceAssessmentRepository assessments,
        ClaimVerificationEvaluator verifier,
        EvidenceIngestionService evidence,
        ProductionGraphProjectionService graph,
        ProjectTruthService truth,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _claims = claims;
        _evidenceRepository = evidenceRepository;
        _assessments = assessments;
        _verifier = verifier;
        _evidence = evidence;
        _graph = graph;
        _truth = truth;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ClaimEvidenceAssessmentDto> AssessAsync(Guid projectId, Guid claimId, CancellationToken ct)
    {
        var claim = await _claims.GetByIdAsync(claimId, ct)
                    ?? throw new KeyNotFoundException($"Claim {claimId} was not found.");
        if (claim.ProjectId != projectId) throw new InvalidOperationException("Claim belongs to another project.");
        var evidence = await _evidenceRepository.ListForProjectAsync(projectId, claim.ClaimedAtUtc.AddHours(-24), ct);
        var verification = _verifier.Evaluate(claim, evidence, _clock.UtcNow);
        var supporting = verification.EvidenceLinks.Where(x => x.Relation == EvidenceRelation.Supports).Select(x => x.EvidenceId).Distinct().ToArray();
        var contradicting = verification.EvidenceLinks.Where(x => x.Relation == EvidenceRelation.Contradicts).Select(x => x.EvidenceId).Distinct().ToArray();
        var status = verification.Status switch
        {
            ClaimVerificationStatus.Confirmed => ClaimEvidenceAssessmentStatus.Supported,
            ClaimVerificationStatus.PartiallyConfirmed => ClaimEvidenceAssessmentStatus.PartiallySupported,
            ClaimVerificationStatus.Contradicted => ClaimEvidenceAssessmentStatus.Contradicted,
            _ => ClaimEvidenceAssessmentStatus.InsufficientEvidence
        };
        var score = ComputeDiscrepancyScore(status, supporting.Length, contradicting.Length, claim.ClaimedProgress);
        var confidence = Confidence(verification.EvidenceLinks.Count, status);
        var reasons = ReasonCodes(status, supporting.Length, contradicting.Length, verification.Status).ToArray();
        var record = new ClaimEvidenceAssessmentRecord(
            Guid.NewGuid(), projectId, claimId, status, score, confidence, verification.Summary,
            JsonSerializer.Serialize(supporting), JsonSerializer.Serialize(contradicting), JsonSerializer.Serialize(reasons), _clock.UtcNow);
        await _assessments.AddAsync(record, ct);
        await _uow.SaveChangesAsync(ct);

        await _graph.ProjectClaimAssessmentAsync(projectId, record.Id, claimId, supporting, contradicting, ct);
        await _evidence.IngestAsync(new IngestEvidenceCommand(
            projectId,
            EvidenceSourceType.Analysis,
            "claim-evidence-assessment",
            record.Id.ToString("N"),
            EvidenceEventType.ClaimAssessment,
            claim.SubjectKey,
            "claim.evidence-assessment",
            $"Claim assessment: {status}",
            verification.Summary,
            status switch
            {
                ClaimEvidenceAssessmentStatus.Supported => EvidenceOutcome.Succeeded,
                ClaimEvidenceAssessmentStatus.PartiallySupported => EvidenceOutcome.Warning,
                ClaimEvidenceAssessmentStatus.Contradicted => EvidenceOutcome.Failed,
                _ => EvidenceOutcome.Unknown
            },
            confidence,
            record.AssessedAtUtc,
            SourceReference: $"claim:{claimId:N}",
            PrivacyClass: EvidencePrivacyClass.ProjectRestricted,
            Metadata: JsonSerializer.SerializeToElement(new
            {
                assessmentId = record.Id,
                claimId,
                discrepancyScore = score,
                status = status.ToString(),
                supportingEvidenceIds = supporting,
                contradictingEvidenceIds = contradicting,
                reasonCodes = reasons
            })), ct);
        await _truth.RecalculateAsync(projectId, ct);
        return Map(record);
    }

    public async Task<IReadOnlyList<ClaimEvidenceAssessmentDto>> ListAsync(Guid projectId, int take, CancellationToken ct)
        => (await _assessments.ListForProjectAsync(projectId, Math.Clamp(take, 1, 1000), ct)).Select(Map).ToArray();

    public static decimal ComputeDiscrepancyScore(ClaimEvidenceAssessmentStatus status, int supports, int contradictions, decimal? claimedProgress)
    {
        var score = status switch
        {
            ClaimEvidenceAssessmentStatus.Supported => 5m,
            ClaimEvidenceAssessmentStatus.PartiallySupported => 35m,
            ClaimEvidenceAssessmentStatus.InsufficientEvidence => 30m,
            ClaimEvidenceAssessmentStatus.Contradicted => 90m,
            _ => 30m
        };
        score += Math.Min(10m, contradictions * 3m);
        score -= Math.Min(10m, supports * 2m);
        if (status == ClaimEvidenceAssessmentStatus.Contradicted && claimedProgress >= 90m) score += 5m;
        return Math.Clamp(decimal.Round(score, 2), 0m, 100m);
    }

    private static EvidenceConfidence Confidence(int evidenceCount, ClaimEvidenceAssessmentStatus status)
    {
        if (evidenceCount >= 3 && status is ClaimEvidenceAssessmentStatus.Supported or ClaimEvidenceAssessmentStatus.Contradicted)
            return EvidenceConfidence.Verified;
        if (evidenceCount >= 2) return EvidenceConfidence.High;
        if (evidenceCount == 1) return EvidenceConfidence.Medium;
        return EvidenceConfidence.Low;
    }

    private static IEnumerable<string> ReasonCodes(ClaimEvidenceAssessmentStatus status, int supports, int contradictions, ClaimVerificationStatus legacy)
    {
        yield return "status:" + status.ToString().ToLowerInvariant();
        if (supports == 0) yield return "no-supporting-evidence";
        if (contradictions > 0) yield return "objective-contradiction";
        if (supports > 1) yield return "multiple-supporting-evidence";
        if (legacy == ClaimVerificationStatus.Stale) yield return "stale-report";
        if (legacy == ClaimVerificationStatus.Unverified) yield return "insufficient-objective-evidence";
    }

    private static ClaimEvidenceAssessmentDto Map(ClaimEvidenceAssessmentRecord x)
        => new(x.Id, x.ProjectId, x.ClaimId, x.Status, x.DiscrepancyScore, x.Confidence, x.Summary,
            ParseGuids(x.SupportingEvidenceJson), ParseGuids(x.ContradictingEvidenceJson), ParseStrings(x.ReasonCodesJson), x.AssessedAtUtc);

    private static IReadOnlyList<Guid> ParseGuids(string json)
    {
        try { return JsonSerializer.Deserialize<Guid[]>(json) ?? Array.Empty<Guid>(); }
        catch (JsonException) { return Array.Empty<Guid>(); }
    }

    private static IReadOnlyList<string> ParseStrings(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }
}
