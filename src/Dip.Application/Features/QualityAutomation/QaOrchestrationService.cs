using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.Incidents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

public sealed class QaOrchestrationService
{
    private readonly IProjectRepository _projects;
    private readonly IQaRunRepository _runs;
    private readonly IQaFindingRepository _findings;
    private readonly IReproductionAttemptRepository _attempts;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;
    private readonly IQaAdapterRegistry _adapters;
    private readonly IWorkspaceTechnologyDetector _detector;
    private readonly IReproductionCoordinator _reproduction;
    private readonly IQaWorkspaceResolver _workspace;
    private readonly ProjectTruthService _truth;
    private readonly PmEvidenceTranslator _pm;
    private readonly IncidentAggregationService _incidents;

    public QaOrchestrationService(
        IProjectRepository projects,
        IQaRunRepository runs,
        IQaFindingRepository findings,
        IReproductionAttemptRepository attempts,
        IEvidenceRepository evidence,
        IUnitOfWork unitOfWork,
        ISystemClock clock,
        IQaAdapterRegistry adapters,
        IWorkspaceTechnologyDetector detector,
        IReproductionCoordinator reproduction,
        IQaWorkspaceResolver workspace,
        ProjectTruthService truth,
        PmEvidenceTranslator pm,
        IncidentAggregationService incidents)
    {
        _projects=projects;_runs=runs;_findings=findings;_attempts=attempts;_evidence=evidence;_unitOfWork=unitOfWork;_clock=clock;
        _adapters=adapters;_detector=detector;_reproduction=reproduction;_workspace=workspace;_truth=truth;_pm=pm;_incidents=incidents;
    }

    public async Task<QaRunResultDto> RunAsync(QaExecutionCommand command,CancellationToken cancellationToken)
    {
        if(command.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(command));
        _=await _projects.GetByIdAsync(command.ProjectId,cancellationToken)??throw new KeyNotFoundException($"Project {command.ProjectId} not found.");
        if(!string.IsNullOrWhiteSpace(command.RuntimeExecutable)&&Path.IsPathRooted(command.RuntimeExecutable))
            throw new InvalidOperationException("RuntimeExecutable must be relative to the configured QA workspace.");

        var workspace=_workspace.Resolve(command.WorkspaceRelativePath);
        var profile=command.AutoDetectTechnologies?await _detector.DetectAsync(workspace,cancellationToken):WorkspaceTechnologyProfile.Empty;
        var language=command.Language??profile.PrimaryLanguage??throw new NotSupportedException("QA language was not supplied and no supported source language could be detected in the workspace.");
        if(!_adapters.TryResolve(language,out var adapter))
            throw new NotSupportedException($"No QA adapter is registered for '{language.Value}'. Registered adapters: {string.Join(", ",_adapters.SupportedLanguages.Select(x=>x.Value))}.");
        var effective=command with{Language=language};

        var runId=Guid.NewGuid();
        var artifactRoot=_workspace.QaArtifactRoot(command.ProjectId,runId);
        Directory.CreateDirectory(artifactRoot);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot,"technology-profile.json"),JsonSerializer.Serialize(profile,new JsonSerializerOptions{WriteIndented=true}),cancellationToken);
        var run=new QaRun(runId,command.ProjectId,command.SubjectKey,language,command.WorkspaceRelativePath,command.CommitSha,artifactRoot,_clock.UtcNow);
        await _runs.AddAsync(run,cancellationToken);run.Start(_clock.UtcNow);await _unitOfWork.SaveChangesAsync(cancellationToken);

        HardcodedQaResult result;
        try
        {
            result=await adapter!.RunAsync(effective,workspace,artifactRoot,cancellationToken);
        }
        catch(Exception ex) when(ex is not OperationCanceledException)
        {
            var payload=JsonSerializer.Serialize(new{error=ex.Message,type=ex.GetType().FullName,language=language.Value,profile});
            run.Complete(QaRunStatus.InfrastructureFailure,0,0,0,payload,_clock.UtcNow);
            await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),command.ProjectId,null,command.SubjectKey,EvidenceSourceType.System,"qa.infrastructure",EvidenceOutcome.Unknown,"QA infrastructure failed",ex.Message,EvidenceConfidence.High,null,payload,_clock.UtcNow,_clock.UtcNow),cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);await _truth.RecalculateAsync(command.ProjectId,cancellationToken);
            throw;
        }

        var findingEntities=result.Findings.Select(candidate=>new QaFinding(
            Guid.NewGuid(),command.ProjectId,runId,command.SubjectKey,language,candidate.Category,candidate.Severity,candidate.Tool,
            candidate.Title,candidate.Message,QaFingerprint.Create(language,candidate),candidate.FilePath,candidate.Line,candidate.Column,candidate.Function,
            candidate.ExceptionType,candidate.StackTrace,candidate.RuleId,_clock.UtcNow)).ToList();
        await _findings.AddRangeAsync(findingEntities,cancellationToken);await _unitOfWork.SaveChangesAsync(cancellationToken);
        foreach(var finding in findingEntities.Where(x=>x.Severity is Severity.Critical or Severity.Warning))
            await _incidents.ObserveQaFindingAsync(finding,command.CommitSha,cancellationToken);

        if(command.AutomaticallyReproduce&&!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            foreach(var finding in findingEntities.Where(ShouldReproduce).Take(10))
            {
                var executable=ResolveRuntimeExecutable(workspace,command.RuntimeExecutable!);
                var scenario=new ReproductionScenario(command.ProjectId,finding.Id,language,workspace,artifactRoot,executable,command.RuntimeArguments??Array.Empty<string>(),
                    ReproductionEnvironment(language),finding.Fingerprint,Math.Clamp(command.TimeoutSeconds,5,1800),finding.Severity==Severity.Critical?5:3,command.RecordScreen,command.RecordSystemAudio);
                var reproduced=await _reproduction.ReproduceAsync(scenario,cancellationToken);
                foreach(var item in reproduced.Attempts)
                {
                    var entity=new ReproductionAttempt(Guid.NewGuid(),command.ProjectId,finding.Id,item.AttemptNumber,item.ArtifactDirectory,_clock.UtcNow);
                    entity.Complete(item.Status,item.Reproduced,item.Crashed,item.TimedOut,item.ExitCode,item.DurationMs,item.VideoPath,item.AudioPath,item.DumpPath,item.ResultJson,_clock.UtcNow);
                    await _attempts.AddAsync(entity,cancellationToken);
                }
                finding.ApplyReproduction(reproduced.Status,reproduced.ReproducedAttempts,reproduced.TotalAttempts);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                if(reproduced.Status==ReproductionStatus.Reproduced)
                    await _incidents.AttachReproductionAsync(command.ProjectId,finding.Fingerprint,$"Automatically reproduced {reproduced.ReproducedAttempts}/{reproduced.TotalAttempts} attempt(s).",cancellationToken);
            }
        }

        var critical=findingEntities.Count(x=>x.Severity==Severity.Critical);var warning=findingEntities.Count(x=>x.Severity==Severity.Warning);
        var noRunnableTools=result.Tools.Count==0||result.Tools.All(x=>!x.WasAvailable);
        var requiredRuntimeUnavailable=!string.IsNullOrWhiteSpace(command.RuntimeExecutable)&&result.Tools.Any(x=>!x.WasAvailable&&x.Tool.EndsWith("-runtime",StringComparison.OrdinalIgnoreCase));
        var finalStatus=noRunnableTools||requiredRuntimeUnavailable?QaRunStatus.InfrastructureFailure:critical>0?QaRunStatus.Failed:warning>0?QaRunStatus.Partial:QaRunStatus.Succeeded;
        run.Complete(finalStatus,findingEntities.Count,critical,warning,result.SummaryJson,_clock.UtcNow);

        var evidence=BuildEvidence(effective,run,findingEntities,result,profile);
        await _evidence.AddRangeAsync(evidence,cancellationToken);await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _truth.RecalculateAsync(command.ProjectId,cancellationToken);
        return Map(run,findingEntities);
    }

    public async Task<QaRunResultDto?> GetAsync(Guid qaRunId,CancellationToken cancellationToken)
    {
        var run=await _runs.GetAsync(qaRunId,cancellationToken);if(run is null)return null;
        var findings=await _findings.ListByRunAsync(qaRunId,cancellationToken);return Map(run,findings);
    }

    public async Task<IReadOnlyList<QaCrashClusterDto>> GetCrashClustersAsync(Guid projectId,int take,CancellationToken cancellationToken)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        var requested=Math.Clamp(take,1,200);
        var findings=await _findings.ListRecentByProjectAsync(projectId,Math.Clamp(requested*20,100,4000),cancellationToken);
        return findings
            .Where(x=>x.Severity==Severity.Critical||x.Category.Contains("crash",StringComparison.OrdinalIgnoreCase)||x.Category.Contains("runtime",StringComparison.OrdinalIgnoreCase)||x.Category.Contains("hang",StringComparison.OrdinalIgnoreCase)||x.Category.Contains("data-race",StringComparison.OrdinalIgnoreCase))
            .GroupBy(x=>x.Fingerprint,StringComparer.OrdinalIgnoreCase)
            .Select(g=>new QaCrashClusterDto(g.Key,g.OrderByDescending(x=>x.ObservedAtUtc).First().Category,g.Max(x=>x.Severity).ToString(),g.OrderByDescending(x=>x.ObservedAtUtc).First().Title,g.Count(),g.Min(x=>x.ObservedAtUtc),g.Max(x=>x.ObservedAtUtc),g.Count(x=>x.ReproductionStatus==ReproductionStatus.Reproduced),g.Select(x=>x.SubjectKey).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).Take(20).ToArray()))
            .OrderByDescending(x=>x.Occurrences).ThenByDescending(x=>x.LastSeenAtUtc).Take(requested).ToArray();
    }

    private static bool ShouldReproduce(QaFinding finding)=>finding.Severity==Severity.Critical||finding.Category.Contains("runtime",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("crash",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("test",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("hang",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("data-race",StringComparison.OrdinalIgnoreCase);

    private static string ResolveRuntimeExecutable(string workspace,string value)
    {
        if(Path.IsPathRooted(value))throw new InvalidOperationException("Runtime executable must be workspace-relative.");
        var candidate=Path.GetFullPath(Path.Combine(workspace,value));var prefix=workspace.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!candidate.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Runtime executable escapes the QA workspace.");
        return candidate;
    }

    private static IReadOnlyDictionary<string,string> ReproductionEnvironment(LanguageId language)=>language==LanguageId.Python
        ?new Dictionary<string,string>{{"PYTHONFAULTHANDLER","1"},{"PYTHONASYNCIODEBUG","1"}}
        :new Dictionary<string,string>();

    private IEnumerable<EvidenceRecord> BuildEvidence(QaExecutionCommand command,QaRun run,IReadOnlyList<QaFinding> findings,HardcodedQaResult result,WorkspaceTechnologyProfile profile)
    {
        var language=command.Language!.Value;
        var confidence=result.Tools.Count>0&&result.Tools.All(x=>x.WasAvailable)?EvidenceConfidence.Verified:EvidenceConfidence.High;
        var outcome=run.Status==QaRunStatus.InfrastructureFailure?EvidenceOutcome.Unknown:findings.Any(x=>x.Severity==Severity.Critical)?EvidenceOutcome.Failed:findings.Any(x=>x.Severity==Severity.Warning)?EvidenceOutcome.Warning:EvidenceOutcome.Succeeded;
        var profilePayload=JsonSerializer.Serialize(new{language=language.Value,profile.Languages,profile.BuildSystems,profile.Frameworks,profile.GameEngines});
        yield return new EvidenceRecord(Guid.NewGuid(),command.ProjectId,null,command.SubjectKey,EvidenceSourceType.Quality,$"qa.{language.Value}",outcome,
            $"{language.Value} QA {outcome}",$"{findings.Count} finding(s): {findings.Count(x=>x.Severity==Severity.Critical)} critical, {findings.Count(x=>x.Severity==Severity.Warning)} warning. Detected build systems: {string.Join(", ",profile.BuildSystems)}.",confidence,null,TrimPayload(profilePayload),_clock.UtcNow,_clock.UtcNow);
        foreach(var finding in findings.Where(x=>x.Severity is Severity.Critical or Severity.Warning).Take(50))
        {
            var reproduced=finding.ReproductionStatus==ReproductionStatus.Reproduced;
            var candidate=new QaFindingCandidate(finding.Category,finding.Severity,finding.Tool,finding.Title,finding.Message,finding.FilePath,finding.Line,finding.Column,finding.Function,finding.ExceptionType,finding.StackTrace,finding.RuleId);
            var pm=_pm.Explain(candidate);
            var payload=TrimPayload(JsonSerializer.Serialize(new{finding.Fingerprint,finding.Tool,finding.Title,finding.Message,finding.FilePath,finding.Line,finding.ExceptionType,stackPreview=Preview(finding.StackTrace,4000),finding.ReproductionStatus,finding.ReproducedAttempts,finding.TotalAttempts,pm.RecommendedAction}));
            var summary=reproduced?$"{pm.Summary} The same failure was automatically reproduced {finding.ReproducedAttempts}/{finding.TotalAttempts} attempts.":pm.Summary;
            yield return new EvidenceRecord(Guid.NewGuid(),command.ProjectId,null,command.SubjectKey,EvidenceSourceType.Quality,$"qa.finding.{finding.Category}",finding.Severity==Severity.Critical?EvidenceOutcome.Failed:EvidenceOutcome.Warning,pm.Title,summary,reproduced?EvidenceConfidence.Verified:EvidenceConfidence.High,null,payload,_clock.UtcNow,_clock.UtcNow);
        }
    }

    private static QaRunResultDto Map(QaRun run,IReadOnlyCollection<QaFinding> findings)=>new(run.Id,run.Status.ToString(),run.Language.Value,run.FindingCount,run.CriticalFindingCount,run.WarningFindingCount,
        findings.Select(x=>new QaFindingDto(x.Id,x.Category,x.Severity.ToString(),x.Tool,x.Title,x.Message,x.Fingerprint,x.FilePath,x.Line,x.ReproductionStatus.ToString(),x.ReproducedAttempts,x.TotalAttempts)).ToArray(),$"api/v1/qa/runs/{run.Id}/artifacts");
    private static string TrimPayload(string value)=>value.Length<=15900?value:value[..15900];
    private static string? Preview(string? value,int max)=>string.IsNullOrEmpty(value)?value:value.Length<=max?value:value[..max];
}
