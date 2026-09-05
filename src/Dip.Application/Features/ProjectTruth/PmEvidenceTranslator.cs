using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record PmEvidenceExplanation(string Title,string Summary,string RecommendedAction);

public sealed record PmQaDecision(
    string Priority,
    string Decision,
    string Why,
    string RecommendedAction,
    string Alternative,
    string ResidualRisk,
    string OwnerRole,
    bool BlocksRelease,
    bool RequiresApproval,
    decimal ConfidencePercent);

public sealed record PmQaDecisionItemDto(
    Guid IssueId,
    string Fingerprint,
    string Category,
    string Severity,
    string Title,
    int OccurrenceCount,
    DateTimeOffset LastSeenAtUtc,
    PmQaDecision Decision);

public sealed class PmEvidenceTranslator
{
    public PmEvidenceExplanation Explain(QaFindingCandidate finding)
    {
        var category=finding.Category.ToLowerInvariant();
        if(category.Contains("inspection")||category.Contains("construction"))
            return new("Construction verification needs attention","Inspection or construction evidence does not currently support a fully completed state.","Review the inspection evidence, responsible work item and required approval before accepting completion.");
        if(category.Contains("render")||category.Contains("publish")||category.Contains("review")||category.Contains("retake")||category.Contains("frame-sequence"))
            return new("Media production verification needs attention","Render, publish, review or frame-sequence evidence indicates that the production item is not fully verified yet.","Open the latest publish/reference evidence and clear the remaining gate before marking the item complete.");
        if(category.Contains("approval"))
            return new("Required approval is not complete","A required approval gate is pending or rejected, so downstream work should not treat the item as formally complete.","Resolve the approval decision and retain the approval record as evidence.");
        if(category.Contains("memory-safety")||category.Contains("native-crash")||category.Contains("undefined-behavior"))
            return new("Critical runtime stability problem","The application can crash or corrupt memory during normal execution. This should be treated as a release blocker until fixed and reproduced as resolved.","Prioritize the owning implementation area and re-run the same reproduction scenario after the fix.");
        if(category.Contains("memory-growth")||category.Contains("memory-leak"))
            return new("Memory use needs investigation","Automated evidence found retained or leaked memory that may become a stability problem during longer sessions or on lower-memory devices.","Review the memory evidence, confirm the owning subsystem, and repeat the scenario after the fix.");
        if(category.Contains("async")||category.Contains("race")||category.Contains("deadlock"))
            return new("Concurrent work may not finish correctly","Automated evidence found asynchronous/concurrent behavior that can fail intermittently or stop progress.","Reproduce under stress, inspect thread/task evidence, and verify the same scenario after correction.");
        if(category.Contains("runtime")||category.Contains("network"))
            return new("Application failed while running","The current build reached a runtime or network failure. The feature cannot be considered fully verified while this evidence remains current.","Assign the failure to the responsible implementation area and verify a clean rerun under the same conditions.");
        if(category.Contains("hang"))
            return new("Application stopped responding","The automated run exceeded its allowed time and appears to have stopped making usable progress.","Investigate the captured dump/logs and confirm the same scenario completes within the expected time.");
        if(category.Contains("compile")||category.Contains("syntax")||category.Contains("configure")||category.Contains("restore"))
            return new("Current source cannot be produced successfully","The automated build path did not complete, so the reported implementation is not currently reproducible from source.","Restore a clean build before relying on completion or release claims.");
        if(category.Contains("test"))
            return new("Expected behavior did not pass automated verification","One or more automated checks disagree with the expected behavior. This may be a product issue or an outdated test and needs confirmation.","Review the failed scenario and either fix the implementation or update the verified expectation with evidence.");
        if(category.Contains("static")||category.Contains("type"))
            return new("Code analysis found a maintainability or correctness risk","Automated analysis found a code-level risk. It is not automatically a release blocker, but it reduces confidence until reviewed.","Have the responsible engineer review the finding and record whether it is fixed or intentionally accepted.");
        return finding.Severity==Severity.Critical
            ? new("Critical automated verification problem","Automated evidence found a critical problem that conflicts with a fully complete state.","Review the attached technical evidence and verify the fix with a new run.")
            : new("Automated verification needs attention","Automated evidence found a warning that should be reviewed before relying on the reported status.","Review the evidence and either resolve it or record why it is acceptable.");
    }

    public PmQaDecision Decide(QaFindingCandidate finding,bool mechanicallyReproduced=false)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var intelligence=ProductionQaIntelligence.Classify(finding,mechanicallyReproduced?ReproductionStatus.Reproduced:ReproductionStatus.NotRequested);
        var explanation=Explain(finding);
        var priority=intelligence.ReleaseBlocker||finding.Severity==Severity.Critical?"P0":finding.Severity==Severity.Warning?"P1":"P2";
        var block=intelligence.ReleaseBlocker;
        var decision=block
            ?"Do not promote this revision until a new run proves the failure is cleared."
            :finding.Severity==Severity.Warning
                ?"Assign and resolve or explicitly accept this risk before the next milestone gate."
                :"Observe and keep the evidence attached; no immediate release block is justified.";
        var alternative=block
            ?"If the release cannot wait, reduce scope/disable the affected capability and require an explicit risk acceptance with rollback evidence."
            :"Accept temporarily only when an owner, expiry/review point and verification plan are recorded.";
        var residual=mechanicallyReproduced
            ?"High until the same reproduction scenario stops failing on the candidate revision."
            :finding.Severity==Severity.Critical
                ?"High until independent runtime or regression evidence clears the finding."
                :"Moderate until reviewed or a newer clean verification supersedes this evidence.";
        var approval=block||finding.Category.Contains("approval",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("release",StringComparison.OrdinalIgnoreCase);
        var confidence=mechanicallyReproduced?97m:intelligence.ConfidencePercent;
        return new(priority,decision,explanation.Summary,explanation.RecommendedAction,alternative,residual,intelligence.OwnerRole,block,approval,confidence);
    }

    public PmEvidenceExplanation Explain(EvidenceRecord evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var kind=evidence.EvidenceKind.ToLowerInvariant();
        if(kind.Contains("inspection"))
            return evidence.Outcome==EvidenceOutcome.Succeeded
                ?new("Inspection passed","The recorded inspection supports the current completion claim.","Retain the inspection record and link it to the affected work item.")
                :new("Inspection is not passing","The current inspection evidence does not support treating the affected construction work as complete.","Resolve the inspection finding and capture a new passing inspection before approval.");
        if(kind.Contains("approval"))
            return evidence.Outcome==EvidenceOutcome.Succeeded
                ?new("Approval recorded","A required approval gate has been satisfied.","Keep the approval attached to the project item for auditability.")
                :new("Approval gate is unresolved","A required approval is pending or rejected.","Resolve the approval before downstream work relies on formal completion.");
        if(kind.Contains("render")||kind.Contains("publish")||kind.Contains("review")||kind.Contains("frame-sequence"))
            return evidence.Outcome==EvidenceOutcome.Succeeded
                ?new("Production gate passed","The current media-production evidence supports the reported state.","Keep the render/publish/review evidence linked to the shot or asset.")
                :new("Production gate needs attention","Render, publish, reference or review evidence does not yet support final completion.","Resolve the failed/pending production gate and re-run the same verification.");
        return new("Project evidence update",evidence.Summary,evidence.Outcome==EvidenceOutcome.Failed?"Review the failed evidence before relying on the reported project state.":"Use this evidence together with independent sources when making a project decision.");
    }
}
