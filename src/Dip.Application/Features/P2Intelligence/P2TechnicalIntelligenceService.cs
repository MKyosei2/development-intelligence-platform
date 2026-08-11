using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

public sealed class P2TechnicalIntelligenceService
{
    private readonly IP2ExperimentRunner _experiments;
    private readonly IMediaRegressionRunner _media;
    private readonly ITestImpactAnalyzer _testImpact;
    private readonly INativeSymbolicator _symbolicator;
    private readonly GameRegressionEvaluator _gameRegression;
    private readonly IP2IntelligenceRepository _records;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthService _truth;

    public P2TechnicalIntelligenceService(
        IP2ExperimentRunner experiments,
        IMediaRegressionRunner media,
        ITestImpactAnalyzer testImpact,
        INativeSymbolicator symbolicator,
        GameRegressionEvaluator gameRegression,
        IP2IntelligenceRepository records,
        IEvidenceRepository evidence,
        IUnitOfWork uow,
        ISystemClock clock,
        ProjectTruthService truth)
    {
        _experiments=experiments;_media=media;_testImpact=testImpact;_symbolicator=symbolicator;_gameRegression=gameRegression;
        _records=records;_evidence=evidence;_uow=uow;_clock=clock;_truth=truth;
    }

    public async Task<FlakyResult> RunFlakyAsync(RepeatedRunCommand command,bool runtime,CancellationToken ct)
    {
        var result=await _experiments.RunRepeatedAsync(command,ct);
        var kind=runtime?P2IntelligenceKind.RuntimeReliability:P2IntelligenceKind.FlakyTest;
        var summary=runtime
            ?$"Runtime reliability: {result.Passed}/{result.Attempts} successful run(s), failure rate {result.FailureRatePercent:0.##}%."
            :$"Flaky-test analysis: {result.Passed}/{result.Attempts} successful run(s), failure rate {result.FailureRatePercent:0.##}%.";
        await StoreMeasuredAsync(command.Target.ProjectId,command.Target.SubjectKey,kind,result.Status,
            result.FailureRatePercent,result.ConfidencePercent,summary,JsonSerializer.Serialize(result),result.ArtifactReference,
            runtime?EvidenceSourceType.Runtime:EvidenceSourceType.Test,
            runtime?"runtime.reliability":"test.flakiness",ct);
        return result;
    }

    public async Task<SoakResult> RunSoakAsync(SoakCommand command,CancellationToken ct)
    {
        var result=await _experiments.RunSoakAsync(command,ct);
        var summary=$"Soak result {result.Status}: memory {result.StartMemoryMb}→{result.EndMemoryMb} MB ({result.MemoryGrowthPercent:+0.##;-0.##;0}%), "
                    +$"performance drift {(result.PerformanceDriftPercent?.ToString("0.##")??"n/a")}%.";
        await StoreMeasuredAsync(command.Target.ProjectId,command.Target.SubjectKey,P2IntelligenceKind.SoakTest,result.Status,
            result.MemoryGrowthPercent,85m,summary,JsonSerializer.Serialize(result),result.ArtifactReference,
            EvidenceSourceType.Runtime,"runtime.soak",ct);

        if(result.PerformanceDriftPercent is not null)
        {
            var driftStatus=result.Crashed?"Failed"
                :Math.Abs(result.PerformanceDriftPercent.Value)>=command.PerformanceDriftWarningPercent?"Warning":"Passed";
            await StoreMeasuredAsync(command.Target.ProjectId,command.Target.SubjectKey,P2IntelligenceKind.PerformanceDrift,driftStatus,
                Math.Clamp(Math.Abs(result.PerformanceDriftPercent.Value),0m,100m),85m,
                $"Sustained performance drift was {result.PerformanceDriftPercent:0.##}% over the soak window.",
                JsonSerializer.Serialize(new{result.StartFps,result.EndFps,result.PerformanceDriftPercent,result.ArtifactReference}),
                result.ArtifactReference,EvidenceSourceType.Performance,"performance.drift",ct,recalculate:false);
        }
        await _truth.RecalculateAsync(command.Target.ProjectId,ct);
        return result;
    }

    public async Task<GitBisectResult> FindRegressionCommitAsync(GitBisectCommand command,CancellationToken ct)
    {
        var result=await _experiments.FindRegressionCommitAsync(command,ct);
        var status=result.Status=="Found"?P2IntelligenceStatus.Informational:P2IntelligenceStatus.InfrastructureFailure;
        var summary=result.FirstBadCommit is null
            ?"Regression origin could not be isolated."
            :$"First failing revision isolated to {result.FirstBadCommit}; last known passing revision {result.LastGoodCommit}.";
        var json=JsonSerializer.Serialize(result);
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),command.Target.ProjectId,command.Target.SubjectKey,
            P2IntelligenceKind.RegressionBisect,status,null,result.FirstBadCommit is null?EvidenceConfidence.Unverified:EvidenceConfidence.High,
            summary,json,result.ArtifactReference,_clock.UtcNow),ct);
        if(result.FirstBadCommit is not null)
        {
            await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),command.Target.ProjectId,command.Target.SubjectKey,
                P2IntelligenceKind.RegressionOrigin,P2IntelligenceStatus.Informational,null,EvidenceConfidence.High,
                $"Regression origin: {result.FirstBadCommit}.",json,result.ArtifactReference,_clock.UtcNow),ct);
            await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),command.Target.ProjectId,null,command.Target.SubjectKey,
                EvidenceSourceType.Analysis,"regression.origin",EvidenceOutcome.Changed,
                "Regression origin isolated",summary,EvidenceConfidence.High,result.ArtifactReference,json,_clock.UtcNow,_clock.UtcNow),ct);
        }
        await _uow.SaveChangesAsync(ct);await _truth.RecalculateAsync(command.Target.ProjectId,ct);
        return result;
    }

    public async Task<RegressionOriginResult> DetectRegressionOriginAsync(
        Guid projectId,string subjectKey,IReadOnlyList<RegressionPoint> points,bool lowerIsBetter,decimal thresholdPercent,CancellationToken ct)
    {
        if(points.Count<2)throw new ArgumentException("At least two regression points are required.",nameof(points));
        var baseline=points[0].Value;string? revision=null;decimal bad=baseline,change=0m;
        foreach(var point in points.Skip(1))
        {
            if(baseline==0m)continue;
            var raw=(point.Value-baseline)/Math.Abs(baseline)*100m;
            var regression=lowerIsBetter?raw:-raw;
            if(regression>=thresholdPercent){revision=point.Revision;bad=point.Value;change=regression;break;}
        }
        var result=new RegressionOriginResult(revision is null?"NotDetected":"Detected",revision,baseline,bad,decimal.Round(change,2),lowerIsBetter?"increase-is-regression":"decrease-is-regression");
        var json=JsonSerializer.Serialize(new{result,points,thresholdPercent,lowerIsBetter});
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),projectId,subjectKey,P2IntelligenceKind.RegressionOrigin,
            revision is null?P2IntelligenceStatus.Passed:P2IntelligenceStatus.Warning,
            revision is null?0m:Math.Clamp(change,0m,100m),EvidenceConfidence.Medium,
            revision is null?"No material regression origin was detected in the supplied series.":$"Material regression begins at {revision}.",
            json,null,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);return result;
    }

    public async Task<MutationResult> RunMutationAsync(MutationCommand command,CancellationToken ct)
    {
        var result=await _experiments.RunMutationAsync(command,ct);
        var summary=$"Mutation score {result.MutationScorePercent:0.##}%: {result.Killed}/{result.Applied} applied mutation(s) were detected by tests.";
        var evidenceStatus=result.Status=="Failed"?"Warning":result.Status;
        await StoreMeasuredAsync(command.Target.ProjectId,command.Target.SubjectKey,P2IntelligenceKind.MutationTest,evidenceStatus,
            100m-result.MutationScorePercent,85m,summary,JsonSerializer.Serialize(result),result.ArtifactReference,
            EvidenceSourceType.Test,"test.mutation-strength",ct);
        return result;
    }

    public async Task<FuzzResult> RunFuzzAsync(FuzzCommand command,CancellationToken ct)
    {
        var result=await _experiments.RunFuzzAsync(command,ct);
        var score=Math.Clamp((decimal)result.Crashes/Math.Max(1,result.Cases)*100m+(decimal)result.Timeouts/Math.Max(1,result.Cases)*50m,0m,100m);
        var summary=$"Fuzz run: {result.Cases} case(s), {result.Crashes} crash(es), {result.Timeouts} timeout(s), {result.UniqueFingerprints} unique failure fingerprint(s).";
        await StoreMeasuredAsync(command.Target.ProjectId,command.Target.SubjectKey,P2IntelligenceKind.FuzzTest,result.Status,
            score,90m,summary,JsonSerializer.Serialize(result),result.ArtifactReference,
            EvidenceSourceType.Runtime,"runtime.fuzz",ct);
        return result;
    }

    public async Task<TestImpactResult> AnalyzeTestImpactAsync(TestImpactCommand command,CancellationToken ct)
    {
        var result=await _testImpact.AnalyzeAsync(command,ct);
        var summary=result.SelectedTests==0
            ?$"Test impact analysis found {result.CandidateTests} candidate test(s) but could not safely narrow the suite."
            :$"Test impact selected {result.SelectedTests}/{result.CandidateTests} test(s), estimated execution reduction {result.EstimatedReductionPercent:0.##}%.";
        await _records.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.TestImpact,
            result.Status=="Selected"||result.Status=="Partial"?P2IntelligenceStatus.Informational:P2IntelligenceStatus.Warning,
            result.EstimatedReductionPercent,EvidenceConfidence.High,summary,JsonSerializer.Serialize(result),null,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
        return result;
    }

    public async Task<NativeSymbolicationResult> SymbolicateAsync(NativeSymbolicationCommand command,CancellationToken ct)
    {
        var result=await _symbolicator.SymbolicateAsync(command,ct);
        var status=result.Status=="InfrastructureFailure"?P2IntelligenceStatus.InfrastructureFailure:P2IntelligenceStatus.Informational;
        await _records.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.NativeSymbolication,status,
            null,result.Status=="Resolved"?EvidenceConfidence.Verified:EvidenceConfidence.High,
            result.Summary,JsonSerializer.Serialize(result),result.ArtifactReference,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
        return result;
    }

    public async Task<GameRegressionResult> AnalyzeGameRegressionAsync(GameRegressionCommand command,CancellationToken ct)
    {
        var result=_gameRegression.Evaluate(command);
        await StoreMeasuredAsync(
            command.ProjectId,command.SubjectKey,P2IntelligenceKind.GamePerformanceRegression,result.Status,
            result.RiskScore,95m,result.Summary,JsonSerializer.Serialize(result),null,
            EvidenceSourceType.Performance,"game.performance-regression",ct);
        return result;
    }

    public async Task<MediaRegressionResult> AnalyzeMediaAsync(MediaRegressionCommand command,CancellationToken ct)
    {
        var result=await _media.AnalyzeAsync(command,ct);
        var kind=command.Type switch
        {
            MediaRegressionType.Visual=>P2IntelligenceKind.VisualRegression,
            MediaRegressionType.Video=>P2IntelligenceKind.VideoRegression,
            _=>P2IntelligenceKind.AudioRegression
        };
        var evidenceKind=command.Type switch
        {
            MediaRegressionType.Visual=>"visual.regression",
            MediaRegressionType.Video=>"video.regression",
            _=>"audio.regression"
        };
        await StoreMeasuredAsync(command.ProjectId,command.SubjectKey,kind,result.Status,result.DifferencePercent,result.ConfidencePercent,
            result.Summary,result.DetailJson,result.ArtifactReference,EvidenceSourceType.Quality,evidenceKind,ct);
        return result;
    }

    public async Task<IReadOnlyList<P2IntelligenceDto>> ListAsync(Guid projectId,int take,CancellationToken ct)=>
        (await _records.ListRecentAsync(projectId,take,ct)).Select(Map).ToArray();

    private async Task StoreMeasuredAsync(
        Guid projectId,string subjectKey,P2IntelligenceKind kind,string statusText,decimal? score,decimal confidencePercent,
        string summary,string payload,string? artifact,EvidenceSourceType sourceType,string evidenceKind,CancellationToken ct,bool recalculate=true)
    {
        var status=ParseStatus(statusText);
        var confidence=confidencePercent>=90m?EvidenceConfidence.Verified:confidencePercent>=70m?EvidenceConfidence.High:confidencePercent>=45m?EvidenceConfidence.Medium:EvidenceConfidence.Low;
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),projectId,subjectKey,kind,status,score,confidence,summary,payload,artifact,_clock.UtcNow),ct);

        var outcome=status switch
        {
            P2IntelligenceStatus.Failed=>EvidenceOutcome.Failed,
            P2IntelligenceStatus.Warning=>EvidenceOutcome.Warning,
            P2IntelligenceStatus.Passed=>EvidenceOutcome.Succeeded,
            _=>EvidenceOutcome.Unknown
        };
        // Infrastructure failures are explicitly unknown and cannot make Project Truth accuse product code.
        if(status==P2IntelligenceStatus.InfrastructureFailure){outcome=EvidenceOutcome.Unknown;confidence=EvidenceConfidence.High;}
        await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),projectId,null,subjectKey,sourceType,evidenceKind,outcome,
            $"{kind}: {statusText}",summary,confidence,artifact,payload,_clock.UtcNow,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
        if(recalculate)await _truth.RecalculateAsync(projectId,ct);
    }

    private static P2IntelligenceStatus ParseStatus(string value)=>value.ToLowerInvariant() switch
    {
        "passed" or "pass"=>P2IntelligenceStatus.Passed,
        "warning"=>P2IntelligenceStatus.Warning,
        "failed" or "fail"=>P2IntelligenceStatus.Failed,
        "infrastructurefailure"=>P2IntelligenceStatus.InfrastructureFailure,
        _=>P2IntelligenceStatus.Informational
    };
    private static P2IntelligenceDto Map(P2IntelligenceRecord x)=>new(
        x.Id,x.SubjectKey,x.Kind.ToString(),x.Status.ToString(),x.Score,x.Confidence.ToString(),x.Summary,x.PayloadJson,x.SourceReference,x.CreatedAtUtc);
}
