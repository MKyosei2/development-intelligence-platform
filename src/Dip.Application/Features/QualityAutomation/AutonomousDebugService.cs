using System.Text.Json;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

/// <summary>
/// Bounded hypothesis -> candidate -> isolated verification loop. It never edits
/// the source workspace. A mechanically verified candidate is returned as
/// ReadyForHumanReview so a human remains responsible for applying/merging it.
/// Session snapshots and attempts are persisted in the existing Development
/// Graph, avoiding new database tables.
/// </summary>
public sealed class AutonomousDebugService(
    IAutonomousDebugAgent hypothesisAgent,
    IFixProposalAgent fixAgent,
    IVerifiedFixService verifier,
    DevelopmentGraphService graph,
    ISystemClock clock)
{
    public async Task<AutonomousDebugSessionDto> RunAsync(AutonomousDebugRequest request,CancellationToken ct)
    {
        Validate(request);var budget=(request.Budget??new AutonomousDebugBudget()).Validate();var started=clock.UtcNow;var sessionId=Guid.NewGuid();var attempts=new List<AutonomousDebugAttemptDto>();var hypotheses=Array.Empty<DebugHypothesis>();
        var running=new AutonomousDebugSessionDto(sessionId,request.ProjectId,request.SubjectKey,request.DomainId,"Running","Collecting evidence-grounded root-cause hypotheses.",hypotheses,attempts,started,null);var sessionNode=await PersistSessionAsync(running,request,ct);
        try
        {
            var generated=await hypothesisAgent.GenerateHypothesesAsync(new(request.ProjectId,request.SubjectKey,request.Question,request.EvidenceJson,request.RepositoryContext,budget.MaxHypotheses),ct);var aiCalls=1;
            if(generated is null||generated.Hypotheses.Count==0)
            {
                var empty=running with{Status="NoGroundedHypothesis",Summary="The configured AI provider did not return a hypothesis grounded in supplied Evidence IDs.",CompletedAtUtc=clock.UtcNow};await PersistSessionAsync(empty,request,ct);return empty;
            }
            hypotheses=generated.Hypotheses.OrderByDescending(x=>x.Confidence).Take(budget.MaxHypotheses).ToArray();var hypothesisNodes=new Dictionary<string,Guid>(StringComparer.OrdinalIgnoreCase);
            foreach(var h in hypotheses)
            {
                var node=await graph.UpsertUniversalNodeAsync(new(request.ProjectId,request.DomainId,"dip.debug.hypothesis",$"debug-hypothesis:{sessionId:N}:{h.Id}",h.Title,"Proposed","DIP-AI",null,h.Id,"1",DevelopmentGraphNodeType.ExternalObservation,JsonSerializer.Serialize(new{sessionId,h.Id,h.Title,h.Explanation,h.Confidence,h.EvidenceIds,h.SuggestedExperiments})),ct);hypothesisNodes[h.Id]=node.Node.Id;
                await graph.LinkUniversalAsync(new(request.ProjectId,sessionNode.Node.Id,node.Node.Id,"contains",100m,false,DevelopmentGraphEdgeType.Contains,"{}"),ct);
            }

            var experimentNumber=0;var budgetReason="";var verifiedAny=false;
            foreach(var hypothesis in hypotheses)
            {
                ct.ThrowIfCancellationRequested();if(experimentNumber>=budget.MaxExperiments){budgetReason="Maximum experiment count reached.";break;}if(aiCalls>=budget.MaxAiCalls){budgetReason="Maximum AI call count reached.";break;}if(clock.UtcNow-started>TimeSpan.FromMinutes(budget.MaxWallClockMinutes)){budgetReason="Maximum wall-clock budget reached.";break;}
                experimentNumber++;var prompt=$"""
Evaluate this root-cause hypothesis and propose the smallest safe fix candidate that would falsify or support it through regression testing and existing QA.
Hypothesis ID: {hypothesis.Id}
Title: {hypothesis.Title}
Explanation: {hypothesis.Explanation}
Suggested experiments: {string.Join(", ",hypothesis.SuggestedExperiments)}
The candidate will be applied only to a disposable worktree and mechanically verified. Include at least one regression test. Do not change source merely to silence a diagnostic or test.
Original question: {request.Question}
""";
                var fix=await fixAgent.ProposeAsync(new(request.ProjectId,request.SubjectKey,prompt,request.EvidenceJson,request.RepositoryContext),ct);aiCalls++;
                if(fix is null)
                {
                    var noProposal=new AutonomousDebugAttemptDto(hypothesis.Id,hypothesis.Title,hypothesis.Confidence,"NoGroundedProposal",false,"No evidence-grounded patch candidate was returned for this hypothesis.",null,hypothesis.EvidenceIds);attempts.Add(noProposal);await PersistAttemptAsync(request,sessionId,experimentNumber,hypothesis,noProposal,hypothesisNodes[hypothesis.Id],ct);continue;
                }
                var verification=await verifier.VerifyAsync(new(request.ProjectId,request.WorkspaceRelativePath,request.BaseCommit,fix.UnifiedDiff,fix.RegressionTests,request.Language,budget.TimeoutSecondsPerExperiment),ct);
                var status=verification.Verified?"VerifiedCandidate":"RejectedCandidate";var attempt=new AutonomousDebugAttemptDto(hypothesis.Id,hypothesis.Title,hypothesis.Confidence,status,verification.Verified,verification.Summary,verification.ArtifactRoot,fix.EvidenceIds);attempts.Add(attempt);await PersistAttemptAsync(request,sessionId,experimentNumber,hypothesis,attempt,hypothesisNodes[hypothesis.Id],ct);
                if(verification.Verified){verifiedAny=true;break;}
            }
            var finalStatus=verifiedAny?"ReadyForHumanReview":string.IsNullOrWhiteSpace(budgetReason)?"NotVerified":"BudgetExhausted";var summary=verifiedAny?"A candidate passed isolated patch application, generated regression tests and all available QA checks. The source workspace was not modified; human review is still required.":string.IsNullOrWhiteSpace(budgetReason)?"No candidate passed the isolated verification boundary.":$"No verified candidate was found before the bounded search stopped: {budgetReason}";
            var completed=new AutonomousDebugSessionDto(sessionId,request.ProjectId,request.SubjectKey,request.DomainId,finalStatus,summary,hypotheses,attempts,started,clock.UtcNow);await PersistSessionAsync(completed,request,ct);return completed;
        }
        catch(OperationCanceledException)
        {
            var cancelled=new AutonomousDebugSessionDto(sessionId,request.ProjectId,request.SubjectKey,request.DomainId,"Cancelled","The autonomous debugging request was cancelled. No source workspace changes were made.",hypotheses,attempts,started,clock.UtcNow);try{await PersistSessionAsync(cancelled,request,CancellationToken.None);}catch{}throw;
        }
        catch(Exception ex)
        {
            var failed=new AutonomousDebugSessionDto(sessionId,request.ProjectId,request.SubjectKey,request.DomainId,"Failed",$"Autonomous debugging stopped before verification completed: {ex.GetType().Name}.",hypotheses,attempts,started,clock.UtcNow);try{await PersistSessionAsync(failed,request,CancellationToken.None);}catch{}throw;
        }
    }

    public async Task<AutonomousDebugSessionDto?> GetAsync(Guid projectId,Guid sessionId,CancellationToken ct)
    {
        if(projectId==Guid.Empty||sessionId==Guid.Empty)return null;var node=await graph.FindNodeAsync(projectId,DevelopmentGraphNodeType.ExternalObservation,$"debug-session:{sessionId:N}",ct);if(node is null)return null;
        try{using var doc=JsonDocument.Parse(node.MetadataJson);if(doc.RootElement.TryGetProperty("snapshot",out var snapshot))return snapshot.Deserialize<AutonomousDebugSessionDto>();}catch{}return null;
    }

    private async Task<UniversalGraphNodeDto> PersistSessionAsync(AutonomousDebugSessionDto session,AutonomousDebugRequest request,CancellationToken ct)
    {
        var metadata=JsonSerializer.Serialize(new{snapshot=session,baseCommit=request.BaseCommit,workspace=request.WorkspaceRelativePath,budget=request.Budget??new AutonomousDebugBudget(),safety=new{sourceWorkspaceModified=false,automaticMerge=false,arbitraryShellFromAi=false}});
        return await graph.UpsertUniversalNodeAsync(new(request.ProjectId,request.DomainId,"dip.debug.session",$"debug-session:{session.SessionId:N}",$"Autonomous debug: {request.SubjectKey}",session.Status,"DIP",null,session.SessionId.ToString("N"),"1",DevelopmentGraphNodeType.ExternalObservation,metadata),ct);
    }

    private async Task PersistAttemptAsync(AutonomousDebugRequest request,Guid sessionId,int number,DebugHypothesis hypothesis,AutonomousDebugAttemptDto attempt,Guid hypothesisNodeId,CancellationToken ct)
    {
        var node=await graph.UpsertUniversalNodeAsync(new(request.ProjectId,request.DomainId,"dip.debug.experiment",$"debug-experiment:{sessionId:N}:{number}",$"Experiment {number}: {hypothesis.Title}",attempt.Status,"DIP",null,$"{sessionId:N}:{number}","1",DevelopmentGraphNodeType.ExternalObservation,JsonSerializer.Serialize(new{sessionId,number,hypothesisId=hypothesis.Id,attempt.Status,attempt.Verified,attempt.Summary,attempt.EvidenceIds,suggested=hypothesis.SuggestedExperiments,executed="VerifyPatch+RegressionTest+AvailableQA"})),ct);
        await graph.LinkUniversalAsync(new(request.ProjectId,hypothesisNodeId,node.Node.Id,"tested-by",100m,false,DevelopmentGraphEdgeType.TestedBy,"{}"),ct);
    }

    private static void Validate(AutonomousDebugRequest request)
    {
        if(request.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(request));if(string.IsNullOrWhiteSpace(request.SubjectKey))throw new ArgumentException("SubjectKey is required.",nameof(request));if(request.SubjectKey.Length>200)throw new ArgumentException("SubjectKey must be at most 200 characters.",nameof(request));if(string.IsNullOrWhiteSpace(request.Question))throw new ArgumentException("Question is required.",nameof(request));if(request.Question.Length>16000)throw new ArgumentException("Question is too large.",nameof(request));if(string.IsNullOrWhiteSpace(request.EvidenceJson))throw new ArgumentException("EvidenceJson is required.",nameof(request));if(request.EvidenceJson.Length>262144)throw new ArgumentException("EvidenceJson exceeds the 256 KiB autonomous-debug input limit.",nameof(request));if(request.RepositoryContext?.Length>262144)throw new ArgumentException("RepositoryContext exceeds the 256 KiB autonomous-debug input limit.",nameof(request));if(string.IsNullOrWhiteSpace(request.WorkspaceRelativePath))throw new ArgumentException("WorkspaceRelativePath is required.",nameof(request));if(request.WorkspaceRelativePath.Length>2000)throw new ArgumentException("WorkspaceRelativePath is too long.",nameof(request));if(string.IsNullOrWhiteSpace(request.DomainId))throw new ArgumentException("DomainId is required.",nameof(request));if(request.DomainId.Length>120)throw new ArgumentException("DomainId is too long.",nameof(request));
    }
}
