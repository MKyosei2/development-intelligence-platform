// Purpose: Orchestrates the DIP workflow for P ro je ct Tr ut hS er vi ce.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.ProjectTruth;

public sealed class ProjectTruthService
{
    private readonly IProjectRepository _projects;
    private readonly IEvidenceRepository _evidence;
    private readonly IProjectClaimRepository _claims;
    private readonly IClaimEvidenceLinkRepository _links;
    private readonly IProjectTruthRepository _truth;
    private readonly IDecisionTraceRepository _decisionTraces;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthEvaluator _evaluator;
    private readonly AdaptiveProjectRiskEvaluator _adaptiveRisk;

    public ProjectTruthService(
        IProjectRepository projects,
        IEvidenceRepository evidence,
        IProjectClaimRepository claims,
        IClaimEvidenceLinkRepository links,
        IProjectTruthRepository truth,
        IDecisionTraceRepository decisionTraces,
        IUnitOfWork unitOfWork,
        ISystemClock clock,
        ProjectTruthEvaluator evaluator,
        AdaptiveProjectRiskEvaluator adaptiveRisk)
    {
        _projects = projects;
        _evidence = evidence;
        _claims = claims;
        _links = links;
        _truth = truth;
        _decisionTraces = decisionTraces;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _evaluator = evaluator;
        _adaptiveRisk = adaptiveRisk;
    }

    public async Task<ProjectTruthSnapshot> RecalculateAsync(Guid projectId,CancellationToken cancellationToken)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        var project=await _projects.GetByIdAsync(projectId,cancellationToken)??throw new KeyNotFoundException($"Project {projectId} not found.");
        var evidence=await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-90),cancellationToken);
        var claims=await _claims.ListByProjectAsync(projectId,cancellationToken);
        var previousSnapshot=await _truth.GetLatestAsync(projectId,cancellationToken);
        var history=await _truth.ListRecentAsync(projectId,30,cancellationToken);
        var assessment=_evaluator.Evaluate(claims,evidence,_clock.UtcNow);
        var adaptive=_adaptiveRisk.Evaluate(assessment.RiskLevel,evidence,history,_clock.UtcNow);
        var effectiveRisk=adaptive.RiskLevel;
        var effectiveTruth=MaxTruth(assessment.TruthStatus,RiskTruth(effectiveRisk));
        var health=Math.Clamp(assessment.HealthScore-adaptive.HealthPenalty,0m,100m);
        var blockers=assessment.Blockers.ToList();
        var actions=assessment.TopActions.ToList();
        if((int)effectiveRisk>(int)assessment.RiskLevel)
        {
            blockers.Add(new ProjectBlocker("adaptive-risk",effectiveRisk==ProjectRiskLevel.Critical?"Critical":"Blocker","Recent evidence raised project risk",string.Join(" ",adaptive.Reasons),"project",null));
            actions.Insert(0,$"Review adaptive risk signals before the next release/milestone decision: {string.Join(" ",adaptive.Reasons.Take(2))}");
        }
        actions=actions.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();

        var claimsById=claims.ToDictionary(item=>item.Id);
        foreach(var claimAssessment in assessment.ClaimAssessments)
        {
            if(!claimsById.TryGetValue(claimAssessment.ClaimId,out var claim))continue;
            claim.ApplyVerification(claimAssessment.Status,_clock.UtcNow);
            var linkEntities=claimAssessment.EvidenceLinks.Select(link=>new ClaimEvidenceLink(Guid.NewGuid(),claim.Id,link.EvidenceId,link.Relation,link.Reason,_clock.UtcNow)).ToArray();
            await _links.ReplaceForClaimAsync(claim.Id,linkEntities,cancellationToken);
        }

        var adaptiveSummary=$"Adaptive risk: {effectiveRisk}; recent failure rate {adaptive.RecentFailureRatePercent:0.#}%; trend {adaptive.Trend}.";
        var snapshot=new ProjectTruthSnapshot(
            Guid.NewGuid(),projectId,effectiveTruth,effectiveRisk,health,
            assessment.Progress.VerifiedProgress,assessment.Progress.ReportedProgress,assessment.EvidenceConfidence,
            blockers.Count(item=>item.Severity is "Critical" or "Blocker"),assessment.ContradictionCount,assessment.UnverifiedClaimCount,
            $"{assessment.Summary} {adaptiveSummary}",JsonSerializer.Serialize(blockers),JsonSerializer.Serialize(actions),_clock.UtcNow);
        await _truth.AddAsync(snapshot,cancellationToken);

        var previousHealth=previousSnapshot?.HealthScore.ToString("0.##")??"No previous snapshot";
        var previousState=previousSnapshot is null?"Unknown":$"{previousSnapshot.TruthStatus}/{previousSnapshot.RiskLevel}";
        var newState=$"{effectiveTruth}/{effectiveRisk}";
        if(previousSnapshot is null||previousSnapshot.TruthStatus!=effectiveTruth||previousSnapshot.RiskLevel!=effectiveRisk||Math.Abs(previousSnapshot.HealthScore-health)>=0.01m)
        {
            var evidenceIds=evidence.OrderByDescending(item=>item.ObservedAtUtc).Take(20).Select(item=>item.Id).ToArray();
            var reason=previousSnapshot is null
                ?"Initial Project Truth snapshot was established from the available evidence."
                :$"Project Truth changed from {previousState} / health {previousHealth} to {newState} / health {health:0.##}.";
            await _decisionTraces.AddAsync(new DecisionTrace(
                Guid.NewGuid(),projectId,"ProjectTruthChanged","project",$"{previousState}; health={previousHealth}",$"{newState}; health={health:0.##}",reason,
                JsonSerializer.Serialize(evidenceIds),JsonSerializer.Serialize(new
                {
                    assessment.Summary,
                    topActions=actions,
                    blockers,
                    assessment.ContradictionCount,
                    assessment.UnverifiedClaimCount,
                    adaptiveRisk=adaptive
                }),_clock.UtcNow),cancellationToken);
        }

        project.UpdateHealth(new HealthScore(health),_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    private static ProjectTruthStatus RiskTruth(ProjectRiskLevel risk)=>risk switch
    {
        ProjectRiskLevel.Critical=>ProjectTruthStatus.Blocked,
        ProjectRiskLevel.High=>ProjectTruthStatus.AtRisk,
        ProjectRiskLevel.Medium=>ProjectTruthStatus.Attention,
        _=>ProjectTruthStatus.Unknown
    };

    private static ProjectTruthStatus MaxTruth(ProjectTruthStatus a,ProjectTruthStatus b)=>(ProjectTruthStatus)Math.Max((int)a,(int)b);
}
