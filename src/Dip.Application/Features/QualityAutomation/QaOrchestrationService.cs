using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

public sealed class QaOrchestrationService
{
    private readonly IProjectRepository _projects;private readonly IQaRunRepository _runs;private readonly IQaFindingRepository _findings;private readonly IPipelineRunRepository _pipelineRuns;private readonly IQualityIssueRepository _qualityIssues;private readonly IReproductionAttemptRepository _attempts;private readonly IEvidenceRepository _evidence;private readonly IUnitOfWork _unitOfWork;private readonly ISystemClock _clock;
    private readonly IQaAdapterRegistry _registry;private readonly IRepositoryTechnologyDetector _detector;private readonly IReproductionCoordinator _reproduction;private readonly IQaWorkspaceResolver _workspace;private readonly ProjectTruthService _truth;private readonly PmEvidenceTranslator _pm;private readonly IncidentAggregationService _incidents;private readonly QaGovernanceRunProcessor _governance;private readonly IReadOnlyList<ICrossCuttingQaPack> _crossPacks;
    public QaOrchestrationService(IProjectRepository projects,IQaRunRepository runs,IQaFindingRepository findings,IPipelineRunRepository pipelineRuns,IQualityIssueRepository qualityIssues,IReproductionAttemptRepository attempts,IEvidenceRepository evidence,IUnitOfWork unitOfWork,ISystemClock clock,IQaAdapterRegistry registry,IRepositoryTechnologyDetector detector,IReproductionCoordinator reproduction,IQaWorkspaceResolver workspace,ProjectTruthService truth,PmEvidenceTranslator pm,IncidentAggregationService incidents,QaGovernanceRunProcessor governance,IEnumerable<ICrossCuttingQaPack> crossPacks)
    {_projects=projects;_runs=runs;_findings=findings;_pipelineRuns=pipelineRuns;_qualityIssues=qualityIssues;_attempts=attempts;_evidence=evidence;_unitOfWork=unitOfWork;_clock=clock;_registry=registry;_detector=detector;_reproduction=reproduction;_workspace=workspace;_truth=truth;_pm=pm;_incidents=incidents;_governance=governance;_crossPacks=crossPacks.ToArray();}

    public async Task<QaRunResultDto> RunAsync(QaExecutionCommand command,CancellationToken cancellationToken)
    {
        if(command.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(command));_=await _projects.GetByIdAsync(command.ProjectId,cancellationToken)??throw new KeyNotFoundException($"Project {command.ProjectId} not found.");
        if(!string.IsNullOrWhiteSpace(command.RuntimeExecutable)&&Path.IsPathRooted(command.RuntimeExecutable))throw new InvalidOperationException("RuntimeExecutable must be relative to the configured QA workspace.");
        var workspace=_workspace.Resolve(command.WorkspaceRelativePath);var requested=string.IsNullOrWhiteSpace(command.Language.Value)?LanguageId.Auto:command.Language;var profile=await _detector.DetectAsync(workspace,cancellationToken);var selections=_registry.Resolve(requested,profile,workspace);
        if(selections.Count==0)throw new NotSupportedException($"No QA adapter can handle '{requested}'. Detected languages: {string.Join(", ",profile.Languages.Select(x=>x.Value))}. Adapters: {string.Join(", ",_registry.AdapterIds)}. Add .dip/qa.json for a custom language.");
        var unsupportedDetectedLanguages=requested.IsAuto
            ?profile.Languages.Where(language=>selections.All(x=>x.Language!=language)).Select(x=>x.Value).ToArray()
            :Array.Empty<string>();
        var runLanguage=requested.IsAuto||selections.Count>1?LanguageId.Multi:selections[0].Language;var runId=Guid.NewGuid();var artifactRoot=_workspace.QaArtifactRoot(command.ProjectId,runId);Directory.CreateDirectory(artifactRoot);var run=new QaRun(runId,command.ProjectId,command.SubjectKey,runLanguage,command.WorkspaceRelativePath,command.CommitSha,artifactRoot,_clock.UtcNow);await _runs.AddAsync(run,cancellationToken);run.Start(_clock.UtcNow);await _unitOfWork.SaveChangesAsync(cancellationToken);

        var outputs=new List<(LanguageId Language,QaAdapterResult Result)>();var adapterFailures=new List<object>();
        foreach(var selection in selections)
        {
            try
            {
                var child=command with{Language=selection.Language,AutomaticallyReproduce=false};var childRoot=Path.Combine(artifactRoot,"languages",selection.Language.Value);Directory.CreateDirectory(childRoot);var result=await selection.Adapter.RunAsync(child,workspace,childRoot,cancellationToken);outputs.Add((selection.Language,result));
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                adapterFailures.Add(new{language=selection.Language.Value,adapter=selection.Adapter.AdapterId,error=ex.Message,type=ex.GetType().FullName});var tool=new QaToolExecution($"adapter:{selection.Adapter.AdapterId}",false,false,null,0,"","",ex.Message);outputs.Add((selection.Language,new QaAdapterResult(selection.Language,new[]{tool},Array.Empty<QaFindingCandidate>(),JsonSerializer.Serialize(new{error=ex.Message}))));
            }
        }
        var crossResults=new List<QaCrossCuttingResult>();
        foreach(var pack in _crossPacks.Where(x=>x.Applies(profile,workspace)))
        {
            try{var packRoot=Path.Combine(artifactRoot,"cross-cutting",pack.PackId);Directory.CreateDirectory(packRoot);crossResults.Add(await pack.RunAsync(command,profile,workspace,packRoot,cancellationToken));}
            catch(Exception ex) when(ex is not OperationCanceledException){adapterFailures.Add(new{language="cross-cutting",adapter=pack.PackId,error=ex.Message,type=ex.GetType().FullName});crossResults.Add(new(pack.PackId,[new($"pack:{pack.PackId}",false,false,null,0,"","",ex.Message)],[],JsonSerializer.Serialize(new{error=ex.Message})));}
        }
        var candidates=outputs.SelectMany(o=>o.Result.Findings.Select(f=>(Language:o.Language,Finding:f))).Concat(crossResults.SelectMany(x=>x.Findings.Select(f=>(Language:runLanguage,Finding:f)))).ToArray();var findingEntities=candidates.Select(x=>new QaFinding(Guid.NewGuid(),command.ProjectId,runId,command.SubjectKey,x.Language,x.Finding.Category,x.Finding.Severity,x.Finding.Tool,x.Finding.Title,x.Finding.Message,QaFingerprint.Create(x.Language,x.Finding),x.Finding.FilePath,x.Finding.Line,x.Finding.Column,x.Finding.Function,x.Finding.ExceptionType,x.Finding.StackTrace,x.Finding.RuleId,_clock.UtcNow,x.Finding.DiagnosticSchemaVersion,x.Finding.SourceFormat,x.Finding.SourceFormatVersion)).ToList();
        await _findings.AddRangeAsync(findingEntities,cancellationToken);await _unitOfWork.SaveChangesAsync(cancellationToken);foreach(var finding in findingEntities.Where(x=>x.Severity is Severity.Critical or Severity.Warning))await _incidents.ObserveQaFindingAsync(finding,command.CommitSha,cancellationToken);

        if(command.AutomaticallyReproduce&&!string.IsNullOrWhiteSpace(command.RuntimeExecutable)&&selections.Count==1)
        {
            foreach(var finding in findingEntities.Where(ShouldReproduce).Take(10))
            {
                var executable=ResolveRuntimeExecutable(workspace,command.RuntimeExecutable!);var scenario=new ReproductionScenario(command.ProjectId,finding.Id,finding.Language,workspace,artifactRoot,executable,command.RuntimeArguments??Array.Empty<string>(),ReproductionEnvironment(finding.Language),finding.Fingerprint,Math.Clamp(command.TimeoutSeconds,5,1800),finding.Severity==Severity.Critical?5:3,command.RecordScreen,command.RecordSystemAudio);var reproduced=await _reproduction.ReproduceAsync(scenario,cancellationToken);
                foreach(var item in reproduced.Attempts){var entity=new ReproductionAttempt(Guid.NewGuid(),command.ProjectId,finding.Id,item.AttemptNumber,item.ArtifactDirectory,_clock.UtcNow);entity.Complete(item.Status,item.Reproduced,item.Crashed,item.TimedOut,item.ExitCode,item.DurationMs,item.VideoPath,item.AudioPath,item.DumpPath,item.ResultJson,_clock.UtcNow);await _attempts.AddAsync(entity,cancellationToken);}finding.ApplyReproduction(reproduced.Status,reproduced.ReproducedAttempts,reproduced.TotalAttempts);await _unitOfWork.SaveChangesAsync(cancellationToken);if(reproduced.Status==ReproductionStatus.Reproduced)await _incidents.AttachReproductionAsync(command.ProjectId,finding.Fingerprint,$"Automatically reproduced {reproduced.ReproducedAttempts}/{reproduced.TotalAttempts} attempt(s).",cancellationToken);
            }
        }
        var tools=outputs.SelectMany(x=>x.Result.Tools).Concat(crossResults.SelectMany(x=>x.Tools)).ToArray();var critical=findingEntities.Count(x=>x.Severity==Severity.Critical);var warning=findingEntities.Count(x=>x.Severity==Severity.Warning);
        var unavailableLanguages=outputs.Where(x=>x.Result.Tools.Count==0||x.Result.Tools.All(t=>!t.WasAvailable)).Select(x=>x.Language.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var allAdaptersFailed=unavailableLanguages.Length>=selections.Count;
        var finalStatus=allAdaptersFailed?QaRunStatus.InfrastructureFailure:critical>0?QaRunStatus.Failed:warning>0||adapterFailures.Count>0||unavailableLanguages.Length>0||unsupportedDetectedLanguages.Length>0?QaRunStatus.Partial:QaRunStatus.Succeeded;
        var summary=JsonSerializer.Serialize(new{schema="dip-universal-qa-v1",requestedLanguage=requested.Value,runLanguage=runLanguage.Value,detected=new{languages=profile.Languages.Select(x=>x.Value),profile.BuildSystems,profile.Frameworks,profile.GameEngines,profile.Evidence},adapters=selections.Select(x=>new{language=x.Language.Value,adapterId=x.Adapter.AdapterId}),crossCuttingPacks=crossResults.Select(x=>x.PackId),adapterFailures,unavailableLanguages,unsupportedDetectedLanguages,tools=tools.Select(x=>new{x.Tool,x.WasAvailable,x.Succeeded,x.ExitCode,x.DurationMs,x.Executable,x.Version,x.SandboxJson,x.CpuTimeMs}),findingCount=findingEntities.Count,critical,warning});run.Complete(finalStatus,findingEntities.Count,critical,warning,summary,_clock.UtcNow);
        var pipelineRunId=await MaterializePipelineAndIssuesAsync(command,run,findingEntities,cancellationToken);
        await _evidence.AddRangeAsync(BuildEvidence(command,run,findingEntities,tools,profile,summary,pipelineRunId),cancellationToken);await _unitOfWork.SaveChangesAsync(cancellationToken);await _governance.ProcessCompletedRunAsync(run,findingEntities,cancellationToken);await _truth.RecalculateAsync(command.ProjectId,cancellationToken);return Map(run,findingEntities,pipelineRunId);
    }
    public async Task<QaRunResultDto?> GetAsync(Guid qaRunId,CancellationToken cancellationToken){var run=await _runs.GetAsync(qaRunId,cancellationToken);if(run is null)return null;var findings=await _findings.ListByRunAsync(qaRunId,cancellationToken);var pipeline=await _pipelineRuns.GetByIdAsync(qaRunId,cancellationToken);return Map(run,findings,pipeline?.Id);}
    public async Task<IReadOnlyList<QaCrashClusterDto>> GetCrashClustersAsync(Guid projectId,int take,CancellationToken cancellationToken)
    {if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));var requested=Math.Clamp(take,1,200);var findings=await _findings.ListRecentByProjectAsync(projectId,Math.Clamp(requested*20,100,4000),cancellationToken);return findings.Where(x=>x.Severity==Severity.Critical||x.Category.Contains("crash",StringComparison.OrdinalIgnoreCase)||x.Category.Contains("runtime",StringComparison.OrdinalIgnoreCase)||x.Category.Contains("hang",StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.Fingerprint,StringComparer.OrdinalIgnoreCase).Select(g=>new QaCrashClusterDto(g.Key,g.OrderByDescending(x=>x.ObservedAtUtc).First().Category,g.Max(x=>x.Severity).ToString(),g.OrderByDescending(x=>x.ObservedAtUtc).First().Title,g.Count(),g.Min(x=>x.ObservedAtUtc),g.Max(x=>x.ObservedAtUtc),g.Count(x=>x.ReproductionStatus==ReproductionStatus.Reproduced),g.Select(x=>x.SubjectKey).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).Take(20).ToArray())).OrderByDescending(x=>x.Occurrences).ThenByDescending(x=>x.LastSeenAtUtc).Take(requested).ToArray();}
    private static bool ShouldReproduce(QaFinding finding)=>finding.Severity==Severity.Critical||finding.Category.Contains("runtime",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("crash",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("test",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("hang",StringComparison.OrdinalIgnoreCase);
    private static string ResolveRuntimeExecutable(string workspace,string value){if(Path.IsPathRooted(value))throw new InvalidOperationException("Runtime executable must be workspace-relative.");var candidate=Path.GetFullPath(Path.Combine(workspace,value));var prefix=workspace.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!candidate.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Runtime executable escapes the QA workspace.");return candidate;}
    private static IReadOnlyDictionary<string,string> ReproductionEnvironment(LanguageId language)=>language==LanguageId.Python?new Dictionary<string,string>{{"PYTHONFAULTHANDLER","1"},{"PYTHONASYNCIODEBUG","1"}}:new Dictionary<string,string>();
    private async Task<Guid?> MaterializePipelineAndIssuesAsync(QaExecutionCommand command,QaRun run,IReadOnlyList<QaFinding> findings,CancellationToken ct)
    {
        if(!TryCommitSha(command.CommitSha,out var commitSha))return null;
        var pipeline=new PipelineRun(run.Id,command.ProjectId,commitSha,$"qa:{run.Id:N}",1,"qa-workspace","DIP-QA","universal-qa-v1",run.CreatedAtUtc);
        pipeline.Start(run.StartedAtUtc??run.CreatedAtUtc);
        var finished=run.FinishedAtUtc??_clock.UtcNow;
        switch(run.Status)
        {
            case QaRunStatus.Succeeded:pipeline.Complete(finished);break;
            case QaRunStatus.Partial:pipeline.CompletePartial("QA completed with warnings, unsupported languages, or unavailable optional capabilities.",finished);break;
            case QaRunStatus.Cancelled:pipeline.Cancel("QA run was cancelled.",finished);break;
            case QaRunStatus.InfrastructureFailure:pipeline.Fail("QA infrastructure was unavailable.",finished);break;
            default:pipeline.Fail("QA reported one or more critical findings.",finished);break;
        }
        await _pipelineRuns.AddAsync(pipeline,ct);

        foreach(var finding in findings
            .Where(x=>x.Severity is Severity.Critical or Severity.Warning)
            .GroupBy(x=>x.Fingerprint,StringComparer.OrdinalIgnoreCase)
            .Select(x=>x.OrderByDescending(item=>item.Severity).First()))
        {
            var existing=await _qualityIssues.FindOpenByFingerprintAsync(command.ProjectId,finding.Fingerprint,ct);
            if(existing is not null)
            {
                existing.Reoccur(pipeline.Id,finding.Severity,_clock.UtcNow);
                continue;
            }

            var evidenceJson=TrimPayload(JsonSerializer.Serialize(new
            {
                origin="dip-manual-qa",
                qaRunId=run.Id,
                pipelineRunId=pipeline.Id,
                commitSha=command.CommitSha,
                language=finding.Language.Value,
                finding.Tool,
                finding.FilePath,
                finding.Line,
                finding.Column,
                finding.Function,
                finding.ExceptionType,
                finding.ReproductionStatus,
                finding.ReproducedAttempts,
                finding.TotalAttempts,
                finding.DiagnosticSchemaVersion,
                finding.SourceFormat,
                finding.SourceFormatVersion
            }));
            var issue=new QualityIssue(
                Guid.NewGuid(),
                command.ProjectId,
                pipeline.Id,
                finding.Fingerprint,
                Limit(finding.Category,80),
                finding.Severity,
                Limit(finding.Title,200),
                Limit(finding.Message,4000),
                LimitOptional(finding.FilePath,1000),
                LimitOptional(finding.RuleId,200),
                evidenceJson,
                _clock.UtcNow);
            await _qualityIssues.AddAsync(issue,ct);
        }
        return pipeline.Id;
    }

    private IEnumerable<EvidenceRecord> BuildEvidence(QaExecutionCommand command,QaRun run,IReadOnlyList<QaFinding> findings,IReadOnlyList<QaToolExecution> tools,QaTechnologyProfile profile,string summary,Guid? pipelineRunId)
    {var confidence=tools.Count>0&&tools.All(x=>x.WasAvailable)?EvidenceConfidence.Verified:EvidenceConfidence.High;var outcome=run.Status==QaRunStatus.InfrastructureFailure?EvidenceOutcome.Unknown:run.Status==QaRunStatus.Failed||findings.Any(x=>x.Severity==Severity.Critical)?EvidenceOutcome.Failed:run.Status==QaRunStatus.Partial||findings.Any(x=>x.Severity==Severity.Warning)?EvidenceOutcome.Warning:EvidenceOutcome.Succeeded;yield return new EvidenceRecord(Guid.NewGuid(),command.ProjectId,pipelineRunId,command.SubjectKey,EvidenceSourceType.Quality,$"qa.{run.Language.Value}",outcome,$"{run.Language.Value} universal QA {outcome}",$"{findings.Count} finding(s); detected languages: {string.Join(", ",profile.Languages.Select(x=>x.Value))}.",confidence,null,TrimPayload(summary),_clock.UtcNow,_clock.UtcNow);foreach(var finding in findings.Where(x=>x.Severity is Severity.Critical or Severity.Warning).Take(50)){var reproduced=finding.ReproductionStatus==ReproductionStatus.Reproduced;var candidate=new QaFindingCandidate(finding.Category,finding.Severity,finding.Tool,finding.Title,finding.Message,finding.FilePath,finding.Line,finding.Column,finding.Function,finding.ExceptionType,finding.StackTrace,finding.RuleId,finding.DiagnosticSchemaVersion,finding.SourceFormat,finding.SourceFormatVersion);var pm=_pm.Explain(candidate);var payload=TrimPayload(JsonSerializer.Serialize(new{language=finding.Language.Value,finding.Fingerprint,finding.Tool,finding.Title,finding.Message,finding.FilePath,finding.Line,finding.ExceptionType,stackPreview=Preview(finding.StackTrace,4000),finding.ReproductionStatus,finding.ReproducedAttempts,finding.TotalAttempts,pm.RecommendedAction}));var text=reproduced?$"{pm.Summary} The same failure was automatically reproduced {finding.ReproducedAttempts}/{finding.TotalAttempts} attempts.":pm.Summary;yield return new EvidenceRecord(Guid.NewGuid(),command.ProjectId,pipelineRunId,command.SubjectKey,EvidenceSourceType.Quality,$"qa.finding.{finding.Category}",finding.Severity==Severity.Critical?EvidenceOutcome.Failed:EvidenceOutcome.Warning,pm.Title,text,reproduced?EvidenceConfidence.Verified:EvidenceConfidence.High,null,payload,_clock.UtcNow,_clock.UtcNow);}}
    private static QaRunResultDto Map(QaRun run,IReadOnlyCollection<QaFinding> findings,Guid? pipelineRunId)=>new(run.Id,run.Status.ToString(),run.Language.Value,run.FindingCount,run.CriticalFindingCount,run.WarningFindingCount,findings.Select(x=>new QaFindingDto(x.Id,x.Category,x.Severity.ToString(),x.Tool,x.Title,x.Message,x.Fingerprint,x.FilePath,x.Line,x.ReproductionStatus.ToString(),x.ReproducedAttempts,x.TotalAttempts)).ToArray(),$"api/v1/qa/runs/{run.Id}/artifacts",run.SummaryJson,pipelineRunId);
    private static bool TryCommitSha(string? value,out CommitSha sha){var text=value?.Trim()??string.Empty;if(text.Length is>=7 and<=64&&text.All(Uri.IsHexDigit)){sha=new CommitSha(text);return true;}sha=default;return false;}
    private static string Limit(string value,int max){var text=value.Trim();return text.Length<=max?text:text[..max];}
    private static string? LimitOptional(string? value,int max){if(string.IsNullOrWhiteSpace(value))return null;var text=value.Trim();return text.Length<=max?text:text[..max];}
    private static string TrimPayload(string value)=>value.Length<=15900?value:value[..15900];private static string? Preview(string? value,int max)=>string.IsNullOrEmpty(value)?value:value.Length<=max?value:value[..max];
}
