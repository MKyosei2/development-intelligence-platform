using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record PmEvidenceExplanation(string Title,string Summary,string RecommendedAction);

public sealed class PmEvidenceTranslator
{
    public PmEvidenceExplanation Explain(QaFindingCandidate finding)
    {
        var category=finding.Category.ToLowerInvariant();
        if(category.Contains("memory-safety")||category.Contains("native-crash")||category.Contains("undefined-behavior"))
            return new("Critical runtime stability problem","The application can crash or corrupt memory during normal execution. This should be treated as a release blocker until fixed and reproduced as resolved.","Prioritize the owning implementation area and re-run the same reproduction scenario after the fix.");
        if(category.Contains("memory-growth")||category.Contains("memory-leak"))
            return new("Memory use needs investigation","Automated evidence found retained or leaked memory that may become a stability problem during longer sessions or on lower-memory devices.","Review the memory evidence, confirm the owning subsystem, and repeat the scenario after the fix.");
        if(category.Contains("async"))
            return new("Asynchronous work may not finish correctly","Automated evidence found asynchronous work that was not awaited, remained pending, or otherwise needs review. This can cause intermittent behavior that is difficult to reproduce manually.","Review the asynchronous call path and re-run the same automated scenario after correction.");
        if(category.Contains("runtime"))
            return new("Application failed while running","The current build reached an unhandled runtime error. The feature cannot be considered fully verified while this evidence remains current.","Assign the runtime failure to the responsible implementation area and verify a clean rerun.");
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
}
