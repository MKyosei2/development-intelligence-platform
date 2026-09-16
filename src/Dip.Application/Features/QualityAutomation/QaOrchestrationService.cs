// Purpose: Coordinates Q aO rc he st ra ti on Se rv ic e application behavior for QualityAutomation.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

public sealed class QaOrchestrationService
{
    private readonly IProjectRepository _projects;
    private readonly IQaRunRepository _runs;
    private readonly IQaFindingRepository _findings;
    private readonly IPipelineRunRepository _pipelineRuns;
    private readonly IQualityIssueRepository _qualityIssues;
    private readonly IReproductionAttemptRepository _attempts;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;
    private readonly IQaAdapterRegistry _registry;
    private readonly IRepositoryTechnologyDetector _detector;
    private readonly IReproductionCoordinator _reproduction;
    private readonly IQaWorkspaceResolver _workspace;
    private readonly IBranchQaWorkspaceManager _branchWorkspace;
    private readonly ProjectTruthService _truth;
    private readonly PmEvidenceTranslator _pm;
    private readonly IMediaRegressionRunner _mediaRegression;
    private readonly IncidentAggregationService _incidents;
    private readonly QaGovernanceRunProcessor _governance;
    private readonly IReadOnlyList<ICrossCuttingQaPack> _crossPacks;

    public QaOrchestrationService(
        IProjectRepository projects,
        IQaRunRepository runs,
        IQaFindingRepository findings,
        IPipelineRunRepository pipelineRuns,
        IQualityIssueRepository qualityIssues,
        IReproductionAttemptRepository attempts,
        IEvidenceRepository evidence,
        IUnitOfWork unitOfWork,
        ISystemClock clock,
        IQaAdapterRegistry registry,
        IRepositoryTechnologyDetector detector,
        IReproductionCoordinator reproduction,
        IQaWorkspaceResolver workspace,
        IBranchQaWorkspaceManager branchWorkspace,
        ProjectTruthService truth,
        PmEvidenceTranslator pm,
        IMediaRegressionRunner mediaRegression,
        IncidentAggregationService incidents,
        QaGovernanceRunProcessor governance,
        IEnumerable<ICrossCuttingQaPack> crossPacks)
    {
        _projects=projects;_runs=runs;_findings=findings;_pipelineRuns=pipelineRuns;_qualityIssues=qualityIssues;
        _attempts=attempts;_evidence=evidence;_unitOfWork=unitOfWork;_clock=clock;_registry=registry;_detector=detector;
        _reproduction=reproduction;_workspace=workspace;_branchWorkspace=branchWorkspace;_truth=truth;_pm=pm;_mediaRegression=mediaRegression;_incidents=incidents;_governance=governance;
        _crossPacks=crossPacks.ToArray();
    }

    public async Task<QaRunResultDto> RunAsync(QaExecutionCommand command,CancellationToken cancellationToken)
    {
        if(command.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(command));
        _=await _projects.GetByIdAsync(command.ProjectId,cancellationToken)??throw new KeyNotFoundException($"Project {command.ProjectId} not found.");
        if(!string.IsNullOrWhiteSpace(command.RuntimeExecutable)&&Path.IsPathRooted(command.RuntimeExecutable))
            throw new InvalidOperationException("RuntimeExecutable must be relative to the configured QA workspace.");

        var runId=Guid.NewGuid();
        var artifactRoot=_workspace.QaArtifactRoot(command.ProjectId,runId);
        Directory.CreateDirectory(artifactRoot);
        var sourceWorkspaceRelativePath=command.WorkspaceRelativePath;
        var sourceWorkspace=_workspace.Resolve(sourceWorkspaceRelativePath);
        await using var branchLease=await _branchWorkspace.PrepareAsync(
            sourceWorkspace,sourceWorkspaceRelativePath,command.BranchName,command.CommitSha,command.RefreshRemoteBranch,runId,cancellationToken);
        command=command with
        {
            WorkspaceRelativePath=branchLease.WorkspaceRelativePath,
            CommitSha=branchLease.CommitSha??command.CommitSha,
            BranchName=branchLease.BranchName??command.BranchName
        };
        var workspace=branchLease.WorkspacePath;
        var requested=string.IsNullOrWhiteSpace(command.Language.Value)?LanguageId.Auto:command.Language;
        var profile=await _detector.DetectAsync(workspace,cancellationToken);
        var production=ProductionQaIntelligence.DetectProfile(profile,workspace);
        var selections=_registry.Resolve(requested,profile,workspace);
        if(selections.Count==0)
            throw new NotSupportedException($"No QA adapter can handle '{requested}'. Detected languages: {string.Join(", ",profile.Languages.Select(x=>x.Value))}. Adapters: {string.Join(", ",_registry.AdapterIds)}. Add .dip/qa.json for a custom language.");

        var unsupportedDetectedLanguages=requested.IsAuto
            ?profile.Languages.Where(language=>selections.All(x=>x.Language!=language)).Select(x=>x.Value).ToArray()
            :Array.Empty<string>();
        var runLanguage=requested.IsAuto||selections.Count>1?LanguageId.Multi:selections[0].Language;
        var run=new QaRun(runId,command.ProjectId,command.SubjectKey,runLanguage,sourceWorkspaceRelativePath,command.CommitSha,artifactRoot,_clock.UtcNow);
        await _runs.AddAsync(run,cancellationToken);
        run.Start(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var outputs=new List<(LanguageId Language,QaAdapterResult Result)>();
        var adapterFailures=new List<object>();
        foreach(var selection in selections)
        {
            try
            {
                var child=command with{Language=selection.Language,AutomaticallyReproduce=false};
                var childRoot=Path.Combine(artifactRoot,"languages",selection.Language.Value);
                Directory.CreateDirectory(childRoot);
                var result=await selection.Adapter.RunAsync(child,workspace,childRoot,cancellationToken);
                outputs.Add((selection.Language,result));
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                adapterFailures.Add(new{language=selection.Language.Value,adapter=selection.Adapter.AdapterId,error=ex.Message,type=ex.GetType().FullName});
                var tool=new QaToolExecution($"adapter:{selection.Adapter.AdapterId}",false,false,null,0,"","",ex.Message);
                outputs.Add((selection.Language,new QaAdapterResult(selection.Language,[tool],[],JsonSerializer.Serialize(new{error=ex.Message}))));
            }
        }

        var crossResults=new List<QaCrossCuttingResult>();
        foreach(var pack in _crossPacks.Where(x=>x.Applies(profile,workspace)))
        {
            try
            {
                var packRoot=Path.Combine(artifactRoot,"cross-cutting",pack.PackId);
                Directory.CreateDirectory(packRoot);
                crossResults.Add(await pack.RunAsync(command,profile,workspace,packRoot,cancellationToken));
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                adapterFailures.Add(new{language="cross-cutting",adapter=pack.PackId,error=ex.Message,type=ex.GetType().FullName});
                crossResults.Add(new(pack.PackId,[new QaToolExecution($"pack:{pack.PackId}",false,false,null,0,"","",ex.Message)],[],JsonSerializer.Serialize(new{error=ex.Message})));
            }
        }

        foreach(var spec in ProductionQaIntelligence.ReadMediaRegressionSpecs(workspace))
        {
            if(!Enum.TryParse<MediaRegressionType>(spec.Type,true,out var mediaType))
            {
                crossResults.Add(new QaCrossCuttingResult(
                    "media-regression-config",
                    [new QaToolExecution("dip-media-regression-config",true,false,1,0,"","","Unsupported media regression type in .dip/production.json.")],
                    [new QaFindingCandidate("media-regression-config",Severity.Warning,"dip-media-regression","Unsupported media regression type",$"Type '{spec.Type}' is not Visual, Video or Audio.",".dip/production.json",RuleId:"DIP-MEDIA-TYPE",SourceFormat:"dip-production-profile",SourceFormatVersion:"1.0")],
                    JsonSerializer.Serialize(spec)));
                continue;
            }
            try
            {
                var mediaResult=await _mediaRegression.AnalyzeAsync(new MediaRegressionCommand(
                    command.ProjectId,command.SubjectKey,command.WorkspaceRelativePath,spec.BaselinePath,spec.CurrentPath,mediaType,
                    spec.WarningThresholdPercent,spec.FailureThresholdPercent,spec.VideoSampleFps),cancellationToken);
                var available=!mediaResult.Status.Equals("InfrastructureFailure",StringComparison.OrdinalIgnoreCase);
                var mediaFindings=new List<QaFindingCandidate>();
                if(mediaResult.Status.Equals("Failed",StringComparison.OrdinalIgnoreCase)||mediaResult.Status.Equals("Warning",StringComparison.OrdinalIgnoreCase))
                {
                    var severity=mediaResult.Status.Equals("Failed",StringComparison.OrdinalIgnoreCase)?Severity.Critical:Severity.Warning;
                    mediaFindings.Add(new QaFindingCandidate("media-regression",severity,"dip-media-regression",$"{mediaType} regression {mediaResult.Status}",$"{mediaResult.Summary} Difference {mediaResult.DifferencePercent:0.###}% (confidence {mediaResult.ConfidencePercent:0.##}%).",spec.CurrentPath,RuleId:"DIP-MEDIA-REGRESSION",SourceFormat:"dip-media-regression",SourceFormatVersion:"1.0"));
                }
                if(!available)adapterFailures.Add(new{language="cross-cutting",adapter="media-regression",error=mediaResult.Summary,type="InfrastructureFailure"});
                crossResults.Add(new QaCrossCuttingResult(
                    $"media-regression-{mediaType.ToString().ToLowerInvariant()}",
                    [new QaToolExecution("dip-media-regression",available,available,available?0:null,0,"","",mediaResult.Summary)],
                    mediaFindings,mediaResult.DetailJson));
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                adapterFailures.Add(new{language="cross-cutting",adapter="media-regression",error=ex.Message,type=ex.GetType().FullName});
                crossResults.Add(new QaCrossCuttingResult("media-regression",[new QaToolExecution("dip-media-regression",false,false,null,0,"","",ex.Message)],[],JsonSerializer.Serialize(new{error=ex.Message})));
            }
        }

        var candidates=outputs.SelectMany(o=>o.Result.Findings.Select(f=>(Language:o.Language,Finding:f)))
            .Concat(crossResults.SelectMany(x=>x.Findings.Select(f=>(Language:runLanguage,Finding:f))))
            .ToArray();
        var findingEntities=candidates.Select(x=>new QaFinding(
            Guid.NewGuid(),command.ProjectId,runId,command.SubjectKey,x.Language,x.Finding.Category,x.Finding.Severity,x.Finding.Tool,
            x.Finding.Title,x.Finding.Message,QaFingerprint.Create(x.Language,x.Finding),x.Finding.FilePath,x.Finding.Line,x.Finding.Column,
            x.Finding.Function,x.Finding.ExceptionType,x.Finding.StackTrace,x.Finding.RuleId,_clock.UtcNow,x.Finding.DiagnosticSchemaVersion,x.Finding.SourceFormat,x.Finding.SourceFormatVersion)).ToList();
        await _findings.AddRangeAsync(findingEntities,cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        foreach(var finding in findingEntities.Where(x=>x.Severity is Severity.Critical or Severity.Warning))
            await _incidents.ObserveQaFindingAsync(finding,command.CommitSha,cancellationToken);

        if(command.AutomaticallyReproduce&&!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var executable=ResolveRuntimeExecutable(workspace,command.RuntimeExecutable!);
            foreach(var finding in findingEntities.Where(ShouldReproduce).OrderByDescending(x=>x.Severity).Take(10))
            {
                var intelligence=ProductionQaIntelligence.Classify(finding);
                var scenario=new ReproductionScenario(
                    command.ProjectId,finding.Id,finding.Language,workspace,artifactRoot,executable,command.RuntimeArguments??[],
                    ReproductionEnvironment(finding.Language),finding.Fingerprint,Math.Clamp(command.TimeoutSeconds,5,1800),
                    intelligence.RecommendedAttempts,command.RecordScreen,command.RecordSystemAudio);
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

        var intelligenceRows=findingEntities.Select(f=>
        {
            var candidate=ToCandidate(f);
            var classified=ProductionQaIntelligence.Classify(f);
            var pmDecision=_pm.Decide(candidate,f.ReproductionStatus==ReproductionStatus.Reproduced);
            return new{findingId=f.Id,fingerprint=f.Fingerprint,classification=classified,pmDecision,reproductionStatus=f.ReproductionStatus.ToString()};
        }).ToArray();
        var intelligenceJson=JsonSerializer.Serialize(new
        {
            schema="dip-qa-intelligence-v1",
            projectId=command.ProjectId,
            qaRunId=runId,
            subjectKey=command.SubjectKey,
            commitSha=command.CommitSha,
            branch=new{command.BranchName,branchLease.SourceRef,branchLease.RefreshStatus,branchLease.Warning,branchLease.IsIsolated},
            production,
            findings=intelligenceRows
        },new JsonSerializerOptions{WriteIndented=true});
        await File.WriteAllTextAsync(Path.Combine(artifactRoot,"qa-intelligence.json"),intelligenceJson,cancellationToken);

        var tools=outputs.SelectMany(x=>x.Result.Tools).Concat(crossResults.SelectMany(x=>x.Tools)).ToArray();
        var critical=findingEntities.Count(x=>x.Severity==Severity.Critical);
        var warning=findingEntities.Count(x=>x.Severity==Severity.Warning);
        var unavailableLanguages=outputs.Where(x=>x.Result.Tools.Count==0||x.Result.Tools.All(t=>!t.WasAvailable)).Select(x=>x.Language.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var allAdaptersFailed=unavailableLanguages.Length>=selections.Count;
        var finalStatus=allAdaptersFailed?QaRunStatus.InfrastructureFailure:critical>0?QaRunStatus.Failed:warning>0||adapterFailures.Count>0||unavailableLanguages.Length>0||unsupportedDetectedLanguages.Length>0?QaRunStatus.Partial:QaRunStatus.Succeeded;
        var summary=JsonSerializer.Serialize(new
        {
            schema="dip-universal-qa-v2",
            requestedLanguage=requested.Value,
            runLanguage=runLanguage.Value,
            branch=new{name=command.BranchName,commitSha=command.CommitSha,sourceRef=branchLease.SourceRef,refreshStatus=branchLease.RefreshStatus,warning=branchLease.Warning,isolated=branchLease.IsIsolated},
            detected=new{languages=profile.Languages.Select(x=>x.Value),profile.BuildSystems,profile.Frameworks,profile.GameEngines,profile.Evidence},
            production,
            adapters=selections.Select(x=>new{language=x.Language.Value,adapterId=x.Adapter.AdapterId}),
            crossCuttingPacks=crossResults.Select(x=>x.PackId),adapterFailures,unavailableLanguages,unsupportedDetectedLanguages,
            tools=tools.Select(x=>new{x.Tool,x.WasAvailable,x.Succeeded,x.ExitCode,x.DurationMs,x.Executable,x.Version,x.SandboxJson,x.CpuTimeMs}),
            findingCount=findingEntities.Count,critical,warning,
            releaseBlockers=intelligenceRows.Count(x=>x.pmDecision.BlocksRelease),
            p0Decisions=intelligenceRows.Count(x=>x.pmDecision.Priority=="P0"),
            intelligenceArtifact="qa-intelligence.json"
        });
        run.Complete(finalStatus,findingEntities.Count,critical,warning,summary,_clock.UtcNow);
        var pipelineRunId=await MaterializePipelineAndIssuesAsync(command,run,findingEntities,cancellationToken);
        await _evidence.AddRangeAsync(BuildEvidence(command,run,findingEntities,tools,profile,production,summary,pipelineRunId),cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _governance.ProcessCompletedRunAsync(run,findingEntities,cancellationToken);
        await _truth.RecalculateAsync(command.ProjectId,cancellationToken);
        return Map(run,findingEntities,pipelineRunId);
    }

    public async Task<QaRunResultDto?> GetAsync(Guid qaRunId,CancellationToken cancellationToken)
    {
        var run=await _runs.GetAsync(qaRunId,cancellationToken);if(run is null)return null;
        var findings=await _findings.ListByRunAsync(qaRunId,cancellationToken);
        var pipeline=await _pipelineRuns.GetByIdAsync(qaRunId,cancellationToken);
        return Map(run,findings,pipeline?.Id);
    }

    public async Task<IReadOnlyList<QaCrashClusterDto>> GetCrashClustersAsync(Guid projectId,int take,CancellationToken cancellationToken)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        var requested=Math.Clamp(take,1,200);
        var findings=await _findings.ListRecentByProjectAsync(projectId,Math.Clamp(requested*20,100,4000),cancellationToken);
        return findings.Where(x=>x.Severity==Severity.Critical||x.Category.Contains("crash",StringComparison.OrdinalIgnoreCase)||x.Category.Contains("runtime",StringComparison.OrdinalIgnoreCase)||x.Category.Contains("hang",StringComparison.OrdinalIgnoreCase))
            .GroupBy(x=>x.Fingerprint,StringComparer.OrdinalIgnoreCase)
            .Select(g=>new QaCrashClusterDto(g.Key,g.OrderByDescending(x=>x.ObservedAtUtc).First().Category,g.Max(x=>x.Severity).ToString(),g.OrderByDescending(x=>x.ObservedAtUtc).First().Title,g.Count(),g.Min(x=>x.ObservedAtUtc),g.Max(x=>x.ObservedAtUtc),g.Count(x=>x.ReproductionStatus==ReproductionStatus.Reproduced),g.Select(x=>x.SubjectKey).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).Take(20).ToArray()))
            .OrderByDescending(x=>x.Occurrences).ThenByDescending(x=>x.LastSeenAtUtc).Take(requested).ToArray();
    }

    private static bool ShouldReproduce(QaFinding finding)
    {
        var intelligence=ProductionQaIntelligence.Classify(finding);
        return intelligence.ReleaseBlocker||finding.Category.Contains("runtime",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("crash",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("test",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("hang",StringComparison.OrdinalIgnoreCase)||finding.Category.Contains("network",StringComparison.OrdinalIgnoreCase);
    }

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

    private async Task<Guid?> MaterializePipelineAndIssuesAsync(QaExecutionCommand command,QaRun run,IReadOnlyList<QaFinding> findings,CancellationToken ct)
    {
        if(!TryCommitSha(command.CommitSha,out var commitSha))return null;
        var pipelineBranch=string.IsNullOrWhiteSpace(command.BranchName)?"qa-workspace":command.BranchName.Trim();
        var pipeline=new PipelineRun(run.Id,command.ProjectId,commitSha,$"qa:{run.Id:N}",1,pipelineBranch,"DIP-QA","universal-qa-v2",run.CreatedAtUtc);
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

        foreach(var finding in findings.Where(x=>x.Severity is Severity.Critical or Severity.Warning).GroupBy(x=>x.Fingerprint,StringComparer.OrdinalIgnoreCase).Select(x=>x.OrderByDescending(item=>item.Severity).First()))
        {
            var existing=await _qualityIssues.FindOpenByFingerprintAsync(command.ProjectId,finding.Fingerprint,ct);
            if(existing is not null){existing.Reoccur(pipeline.Id,finding.Severity,_clock.UtcNow);continue;}
            var intelligence=ProductionQaIntelligence.Classify(finding);
            var pmDecision=_pm.Decide(ToCandidate(finding),finding.ReproductionStatus==ReproductionStatus.Reproduced);
            var evidenceJson=TrimPayload(JsonSerializer.Serialize(new
            {
                origin="dip-universal-qa-v2",qaRunId=run.Id,pipelineRunId=pipeline.Id,commitSha=command.CommitSha,branch=command.BranchName,language=finding.Language.Value,
                finding.Tool,finding.FilePath,finding.Line,finding.Column,finding.Function,finding.ExceptionType,finding.ReproductionStatus,finding.ReproducedAttempts,finding.TotalAttempts,
                finding.DiagnosticSchemaVersion,finding.SourceFormat,finding.SourceFormatVersion,intelligence,pmDecision
            }));
            var issue=new QualityIssue(Guid.NewGuid(),command.ProjectId,pipeline.Id,finding.Fingerprint,Limit(finding.Category,80),finding.Severity,Limit(finding.Title,200),Limit(finding.Message,4000),LimitOptional(finding.FilePath,1000),LimitOptional(finding.RuleId,200),evidenceJson,_clock.UtcNow);
            await _qualityIssues.AddAsync(issue,ct);
        }
        return pipeline.Id;
    }

    private IEnumerable<EvidenceRecord> BuildEvidence(
        QaExecutionCommand command,QaRun run,IReadOnlyList<QaFinding> findings,IReadOnlyList<QaToolExecution> tools,
        QaTechnologyProfile profile,ProductionQaProfile production,string summary,Guid? pipelineRunId)
    {
        var confidence=tools.Count>0&&tools.All(x=>x.WasAvailable)?EvidenceConfidence.Verified:EvidenceConfidence.High;
        var outcome=run.Status==QaRunStatus.InfrastructureFailure?EvidenceOutcome.Unknown:run.Status==QaRunStatus.Failed||findings.Any(x=>x.Severity==Severity.Critical)?EvidenceOutcome.Failed:run.Status==QaRunStatus.Partial||findings.Any(x=>x.Severity==Severity.Warning)?EvidenceOutcome.Warning:EvidenceOutcome.Succeeded;
        yield return new EvidenceRecord(Guid.NewGuid(),command.ProjectId,pipelineRunId,command.SubjectKey,EvidenceSourceType.Quality,$"qa.{run.Language.Value}",outcome,$"{run.Language.Value} universal QA {outcome}",$"{findings.Count} finding(s); branch {command.BranchName??"workspace"}; production domain {production.Domain}; detected languages: {string.Join(", ",profile.Languages.Select(x=>x.Value))}.",confidence,null,TrimPayload(summary),_clock.UtcNow,_clock.UtcNow);

        foreach(var finding in findings.Where(x=>x.Severity is Severity.Critical or Severity.Warning).Take(50))
        {
            var reproduced=finding.ReproductionStatus==ReproductionStatus.Reproduced;
            var candidate=ToCandidate(finding);
            var pm=_pm.Explain(candidate);
            var decision=_pm.Decide(candidate,reproduced);
            var intelligence=ProductionQaIntelligence.Classify(finding);
            var payload=TrimPayload(JsonSerializer.Serialize(new
            {
                language=finding.Language.Value,finding.Fingerprint,finding.Tool,finding.Title,finding.Message,finding.FilePath,finding.Line,finding.ExceptionType,
                stackPreview=Preview(finding.StackTrace,4000),finding.ReproductionStatus,finding.ReproducedAttempts,finding.TotalAttempts,
                classification=intelligence,pmDecision=decision,pm.RecommendedAction,productionDomain=production.Domain,branch=command.BranchName,commitSha=command.CommitSha
            }));
            var text=reproduced?$"{pm.Summary} The same failure was automatically reproduced {finding.ReproducedAttempts}/{finding.TotalAttempts} attempts.":pm.Summary;
            yield return new EvidenceRecord(Guid.NewGuid(),command.ProjectId,pipelineRunId,command.SubjectKey,EvidenceSourceType.Quality,$"qa.finding.{finding.Category}",finding.Severity==Severity.Critical?EvidenceOutcome.Failed:EvidenceOutcome.Warning,pm.Title,text,reproduced?EvidenceConfidence.Verified:EvidenceConfidence.High,null,payload,_clock.UtcNow,_clock.UtcNow);
        }
    }

    private static QaFindingCandidate ToCandidate(QaFinding finding)=>new(
        finding.Category,finding.Severity,finding.Tool,finding.Title,finding.Message,finding.FilePath,finding.Line,finding.Column,
        finding.Function,finding.ExceptionType,finding.StackTrace,finding.RuleId,finding.DiagnosticSchemaVersion,finding.SourceFormat,finding.SourceFormatVersion);

    private static QaRunResultDto Map(QaRun run,IReadOnlyCollection<QaFinding> findings,Guid? pipelineRunId)=>new(
        run.Id,run.Status.ToString(),run.Language.Value,run.FindingCount,run.CriticalFindingCount,run.WarningFindingCount,
        findings.Select(x=>new QaFindingDto(x.Id,x.Category,x.Severity.ToString(),x.Tool,x.Title,x.Message,x.Fingerprint,x.FilePath,x.Line,x.ReproductionStatus.ToString(),x.ReproducedAttempts,x.TotalAttempts)).ToArray(),
        $"api/v1/qa/runs/{run.Id}/artifacts",run.SummaryJson,pipelineRunId);

    private static bool TryCommitSha(string? value,out CommitSha sha){var text=value?.Trim()??string.Empty;if(text.Length is>=7 and<=64&&text.All(Uri.IsHexDigit)){sha=new CommitSha(text);return true;}sha=default;return false;}
    private static string Limit(string value,int max){var text=value.Trim();return text.Length<=max?text:text[..max];}
    private static string? LimitOptional(string? value,int max){if(string.IsNullOrWhiteSpace(value))return null;var text=value.Trim();return text.Length<=max?text:text[..max];}
    private static string TrimPayload(string value)=>value.Length<=15900?value:value[..15900];
    private static string? Preview(string? value,int max)=>string.IsNullOrEmpty(value)?value:value.Length<=max?value:value[..max];
}
