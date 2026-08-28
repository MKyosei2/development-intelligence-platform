using Dip.Api.Authorization;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;
public sealed record FixProposalApiRequest(string SubjectKey,string Question,string EvidenceJson,string? RepositoryContext);
public sealed record VerifyFixApiRequest(string WorkspaceRelativePath,string? BaseCommit,string UnifiedDiff,IReadOnlyList<RegressionTestDraft>? RegressionTests,string? Language,int TimeoutSeconds=900);
public sealed record IdeDiagnosticsApiRequest(string SubjectKey,string WorkspaceRelativePath,string? CommitSha,string? Language,IReadOnlyList<IdeDiagnostic> Diagnostics);
public sealed record AutonomousDebugApiRequest(
    string SubjectKey,string Question,string EvidenceJson,string WorkspaceRelativePath,string? RepositoryContext=null,
    string? BaseCommit=null,string? Language=null,string DomainId="software",AutonomousDebugBudget? Budget=null);

[ApiController][Route("api/v1/defect-intelligence")]
public sealed class DefectIntelligenceController(IFixProposalAgent fixAgent,IVerifiedFixService verifier,IIdeDiagnosticNormalizer ide,AutonomousDebugService autonomousDebug):ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("projects/{projectId:guid}/fix-proposal")]
    public async Task<IActionResult> Propose(Guid projectId,[FromBody]FixProposalApiRequest r,CancellationToken ct){var x=await fixAgent.ProposeAsync(new(projectId,r.SubjectKey,r.Question,r.EvidenceJson,r.RepositoryContext),ct);return x is null?UnprocessableEntity(new{status="NoGroundedProposal",message="External AI is disabled or did not return an evidence-grounded proposal."}):Ok(x);}
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("projects/{projectId:guid}/verify-fix")]
    public Task<VerifiedFixResult> Verify(Guid projectId,[FromBody]VerifyFixApiRequest r,CancellationToken ct){var language=LanguageId.TryParse(r.Language,out var l)?l:LanguageId.Auto;return verifier.VerifyAsync(new(projectId,r.WorkspaceRelativePath,r.BaseCommit,r.UnifiedDiff,r.RegressionTests??[],language,r.TimeoutSeconds),ct);}
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("projects/{projectId:guid}/ide-diagnostics")]
    public IActionResult Ide(Guid projectId,[FromBody]IdeDiagnosticsApiRequest r){var language=LanguageId.TryParse(r.Language,out var l)?l:LanguageId.Unknown;var findings=ide.Normalize(new(projectId,r.SubjectKey,language,r.WorkspaceRelativePath,r.CommitSha,r.Diagnostics));return Ok(new{schema="dip.finding.v2",count=findings.Count,findings});}

    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("projects/{projectId:guid}/autonomous-debug")]
    public Task<AutonomousDebugSessionDto> RunAutonomousDebug(Guid projectId,[FromBody]AutonomousDebugApiRequest r,CancellationToken ct)
    {
        var language=LanguageId.TryParse(r.Language,out var parsed)?parsed:LanguageId.Auto;
        return autonomousDebug.RunAsync(new(projectId,r.SubjectKey,r.Question,r.EvidenceJson,r.WorkspaceRelativePath,r.RepositoryContext,r.BaseCommit,language,r.DomainId,r.Budget),ct);
    }

    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/autonomous-debug/{sessionId:guid}")]
    public async Task<IActionResult> GetAutonomousDebug(Guid projectId,Guid sessionId,CancellationToken ct)
    {
        var result=await autonomousDebug.GetAsync(projectId,sessionId,ct);return result is null?NotFound():Ok(result);
    }
}
