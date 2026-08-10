using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.ProjectTruth;

public sealed class ProjectTruthService
{
    private readonly IProjectRepository _projects;
    private readonly IEvidenceRepository _evidence;
    private readonly IProjectClaimRepository _claims;
    private readonly IClaimEvidenceLinkRepository _links;
    private readonly IProjectTruthRepository _truth;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthEvaluator _evaluator;

    public ProjectTruthService(
        IProjectRepository projects,
        IEvidenceRepository evidence,
        IProjectClaimRepository claims,
        IClaimEvidenceLinkRepository links,
        IProjectTruthRepository truth,
        IUnitOfWork unitOfWork,
        ISystemClock clock,
        ProjectTruthEvaluator evaluator)
    {
        _projects = projects;
        _evidence = evidence;
        _claims = claims;
        _links = links;
        _truth = truth;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _evaluator = evaluator;
    }

    public async Task<ProjectTruthSnapshot> RecalculateAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        var project = await _projects.GetByIdAsync(projectId, cancellationToken)
                      ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var evidence = await _evidence.ListForProjectAsync(
            projectId,
            _clock.UtcNow.AddDays(-90),
            cancellationToken);
        var claims = await _claims.ListByProjectAsync(projectId, cancellationToken);

        var assessment = _evaluator.Evaluate(claims, evidence, _clock.UtcNow);
        var claimsById = claims.ToDictionary(item => item.Id);

        foreach (var claimAssessment in assessment.ClaimAssessments)
        {
            if (!claimsById.TryGetValue(claimAssessment.ClaimId, out var claim))
                continue;

            claim.ApplyVerification(claimAssessment.Status, _clock.UtcNow);

            var linkEntities = claimAssessment.EvidenceLinks.Select(link =>
                new ClaimEvidenceLink(
                    Guid.NewGuid(),
                    claim.Id,
                    link.EvidenceId,
                    link.Relation,
                    link.Reason,
                    _clock.UtcNow)).ToArray();

            await _links.ReplaceForClaimAsync(
                claim.Id,
                linkEntities,
                cancellationToken);
        }

        var snapshot = new ProjectTruthSnapshot(
            Guid.NewGuid(),
            projectId,
            assessment.TruthStatus,
            assessment.RiskLevel,
            assessment.HealthScore,
            assessment.Progress.VerifiedProgress,
            assessment.Progress.ReportedProgress,
            assessment.EvidenceConfidence,
            assessment.Blockers.Count(item => item.Severity is "Critical" or "Blocker"),
            assessment.ContradictionCount,
            assessment.UnverifiedClaimCount,
            assessment.Summary,
            JsonSerializer.Serialize(assessment.Blockers),
            JsonSerializer.Serialize(assessment.TopActions),
            _clock.UtcNow);

        await _truth.AddAsync(snapshot, cancellationToken);
        project.UpdateHealth(new HealthScore(assessment.HealthScore), _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return snapshot;
    }
}
