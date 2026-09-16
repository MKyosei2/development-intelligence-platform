// Purpose: Coordinates B en ch ma rk Or ch es tr at io nS er vi ce application behavior for Benchmarks.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.Jobs;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.Performance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Benchmarks;

public sealed class BenchmarkOrchestrationService
{
    private readonly IBenchmarkRepository _repo;private readonly IBenchmarkExecutor _executor;private readonly DurableJobService _jobs;private readonly ArtifactService _artifacts;private readonly IEvidenceRepository _evidence;private readonly IncidentAggregationService _incidents;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;private readonly ProjectTruthService _truth;private readonly PerformanceBudgetService _budgets;
    public BenchmarkOrchestrationService(IBenchmarkRepository repo,IBenchmarkExecutor executor,DurableJobService jobs,ArtifactService artifacts,IEvidenceRepository evidence,IncidentAggregationService incidents,IUnitOfWork uow,ISystemClock clock,ProjectTruthService truth,PerformanceBudgetService budgets){_repo=repo;_executor=executor;_jobs=jobs;_artifacts=artifacts;_evidence=evidence;_incidents=incidents;_uow=uow;_clock=clock;_truth=truth;_budgets=budgets;}

    public async Task<BenchmarkDefinitionDto> CreateDefinitionAsync(CreateBenchmarkDefinitionCommand command,CancellationToken ct)
    {
        if(await _repo.GetDefinitionByKeyAsync(command.Key,ct) is not null)throw new InvalidOperationException($"Benchmark definition '{command.Key}' already exists.");
        Validate(command.Configuration);var entity=new BenchmarkDefinition(Guid.NewGuid(),command.Key,command.Name,command.Kind,JsonSerializer.Serialize(command.Configuration),command.Enabled,_clock.UtcNow);await _repo.AddDefinitionAsync(entity,ct);await _uow.SaveChangesAsync(ct);return Map(entity);
    }
    public async Task<IReadOnlyList<BenchmarkDefinitionDto>> ListDefinitionsAsync(CancellationToken ct)=>(await _repo.ListDefinitionsAsync(ct)).Select(Map).ToArray();
    public Task<EnqueuedJobDto> EnqueueAsync(BenchmarkJobRequest request,CancellationToken ct)=>_jobs.EnqueueAsync(request.ProjectId,DurableJobType.Benchmark,request.SubjectKey,request,3,ct);

    public async Task<BenchmarkRunResultDto> ExecuteJobAsync(DurableJob job,BenchmarkJobRequest request,CancellationToken ct)
    {
        var definition=await _repo.GetDefinitionAsync(request.DefinitionId,ct)??throw new KeyNotFoundException($"Benchmark definition {request.DefinitionId} was not found.");if(!definition.Enabled)throw new InvalidOperationException("Benchmark definition is disabled.");
        var spec=JsonSerializer.Deserialize<BenchmarkExecutionSpec>(definition.ConfigurationJson)??throw new InvalidDataException("Benchmark configuration is invalid.");Validate(spec);var configHash=Sha256(definition.ConfigurationJson);
        var startedAt=_clock.UtcNow;var execution=await _executor.ExecuteAsync(spec,ct);var env=string.IsNullOrWhiteSpace(request.EnvironmentFingerprint)?execution.EnvironmentFingerprint:request.EnvironmentFingerprint.Trim();
        var run=new BenchmarkRun(Guid.NewGuid(),request.ProjectId,definition.Id,job.Id,request.SubjectKey,request.CommitSha,env,configHash,startedAt);await _repo.AddRunAsync(run,ct);run.Start(startedAt);await _uow.SaveChangesAsync(ct);
        try
        {
            var metrics=BuildMetrics(run.Id,execution.Samples);await _repo.AddMetricsAsync(metrics,ct);var baseline=await _repo.FindBaselineAsync(request.ProjectId,definition.Id,env,configHash,request.CommitSha,ct);var comparisons=baseline is null?new List<BenchmarkComparison>():await CompareAsync(run.Id,baseline.Id,metrics,spec,ct);if(comparisons.Count>0)await _repo.AddComparisonsAsync(comparisons,ct);
            var failed=comparisons.Any(x=>x.Verdict==BenchmarkVerdict.Failed);var warning=comparisons.Any(x=>x.Verdict==BenchmarkVerdict.Warning);var sampleFailure=execution.Samples.Any(x=>!x.Passed);PerformanceBudgetEvaluationDto? budget=null;if(request.PerformanceBudgetProfileId.HasValue)budget=await _budgets.EvaluateAsync(request.ProjectId,request.PerformanceBudgetProfileId.Value,metrics.Select(x=>new PerformanceMetricInput(x.Name,x.Aggregation,x.Value,x.Unit)).ToArray(),null,run.Id,ct);var budgetFailed=budget?.Verdict==PerformanceBudgetVerdict.Failed.ToString();var budgetWarning=budget?.Verdict==PerformanceBudgetVerdict.Warning.ToString();var status=sampleFailure?BenchmarkRunStatus.Failed:BenchmarkRunStatus.Succeeded;var summary=$"Benchmark {definition.Key}: {metrics.First(x=>x.Name=="latency"&&x.Aggregation=="p95").Value:0.###} ms p95, {metrics.First(x=>x.Name=="error_rate").Value:0.###}% errors; comparison {(baseline is null?"baseline unavailable":failed?"failed":warning?"warning":"passed")}; budget {(budget is null?"not selected":budget.Verdict)}.";
            var links=new ArtifactLinkContext(job.Id,null,null,run.Id);
            var artifact=await _artifacts.StoreJsonAsync(request.ProjectId,"benchmark.summary","benchmark-summary.json",new{definition=new{definition.Id,definition.Key,definition.Name},request,runId=run.Id,metrics,comparisons,budget,baselineRunId=baseline?.Id,summary},links,"benchmark",ct);
            var samplesArtifact=await _artifacts.StoreJsonAsync(request.ProjectId,"benchmark.samples","raw-samples.json",execution.Samples,links,"benchmark",ct);
            using var executionDocument=JsonDocument.Parse(execution.DetailJson);var executionArtifact=await _artifacts.StoreJsonAsync(request.ProjectId,"benchmark.execution","execution-context.json",executionDocument.RootElement.Clone(),links,"benchmark",ct);
            var comparisonArtifact=await _artifacts.StoreJsonAsync(request.ProjectId,"benchmark.comparison","comparison.json",new{baselineRunId=baseline?.Id,currentRunId=run.Id,comparisons,budget},links,"benchmark",ct);
            var artifactIds=new[]{artifact.Id,samplesArtifact.Id,executionArtifact.Id,comparisonArtifact.Id};
            var outcome=sampleFailure||failed||budgetFailed?EvidenceOutcome.Failed:warning||budgetWarning?EvidenceOutcome.Warning:EvidenceOutcome.Succeeded;await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),request.ProjectId,null,request.SubjectKey,EvidenceSourceType.Performance,$"benchmark.{definition.Key}",outcome,$"Benchmark {definition.Key}: {outcome}",summary,baseline is null?EvidenceConfidence.Medium:EvidenceConfidence.High,artifact.Id.ToString(),JsonSerializer.Serialize(new{run.Id,artifactIds,comparisons,budget}),_clock.UtcNow,_clock.UtcNow),ct);
            if(failed||warning)
            {
                foreach(var comparison in comparisons.Where(x=>x.Verdict is BenchmarkVerdict.Failed or BenchmarkVerdict.Warning))
                {
                    var incident=await _incidents.ObserveBenchmarkRegressionAsync(request.ProjectId,request.SubjectKey,$"benchmark:{definition.Key}:{comparison.MetricName}",$"Benchmark regression: {definition.Key} / {comparison.MetricName}",comparison.Verdict==BenchmarkVerdict.Failed?Severity.Critical:Severity.Warning,job.Id,run.Id,comparison,ct);
                    foreach(var a in new[]{artifact,samplesArtifact,executionArtifact,comparisonArtifact})await _incidents.AttachArtifactAsync(incident.Id,a.Id,a.FileName,ct);
                }
            }
            if(budgetFailed||budgetWarning)
            {
                var incident=await _incidents.ObserveSyntheticAsync(request.ProjectId,request.SubjectKey,$"performance-budget:benchmark:{request.PerformanceBudgetProfileId}:{definition.Key}",$"Performance budget regression: {definition.Key}",budgetFailed?Severity.Critical:Severity.Warning,JsonSerializer.Serialize(new{run.Id,budget}),job.Id,ct);foreach(var a in new[]{artifact,samplesArtifact,executionArtifact,comparisonArtifact})await _incidents.AttachArtifactAsync(incident.Id,a.Id,a.FileName,ct);
            }
            await _uow.SaveChangesAsync(ct);await _truth.RecalculateAsync(request.ProjectId,ct);run.Complete(status,summary,_clock.UtcNow);await _uow.SaveChangesAsync(ct);return new(run.Id,run.Status.ToString(),metrics.Select(Map).ToArray(),comparisons.Select(Map).ToArray(),artifact.Id,summary);
        }
        catch(OperationCanceledException){if(run.Status==BenchmarkRunStatus.Running){run.Complete(BenchmarkRunStatus.Cancelled,"Benchmark execution was cancelled.",_clock.UtcNow);try{await _uow.SaveChangesAsync(CancellationToken.None);}catch{}}throw;}
        catch(Exception ex){if(run.Status==BenchmarkRunStatus.Running){run.Complete(BenchmarkRunStatus.InfrastructureFailure,ex.Message,_clock.UtcNow);try{await _uow.SaveChangesAsync(CancellationToken.None);}catch{}}throw;}
    }
    public async Task<BenchmarkRunResultDto?> GetRunAsync(Guid runId,CancellationToken ct){var run=await _repo.GetRunAsync(runId,ct);if(run is null)return null;var metrics=await _repo.ListMetricsAsync(runId,ct);var comparisons=await _repo.ListComparisonsAsync(runId,ct);return new(run.Id,run.Status.ToString(),metrics.Select(Map).ToArray(),comparisons.Select(Map).ToArray(),null,run.Summary??string.Empty);}

    private async Task<List<BenchmarkComparison>> CompareAsync(Guid currentRunId,Guid baselineRunId,IReadOnlyList<BenchmarkMetric> current,BenchmarkExecutionSpec spec,CancellationToken ct)
    {
        var baseline=(await _repo.ListMetricsAsync(baselineRunId,ct)).ToDictionary(x=>$"{x.Name}:{x.Aggregation}",StringComparer.OrdinalIgnoreCase);var list=new List<BenchmarkComparison>();foreach(var metric in current.Where(x=>x.Name is "latency" or "error_rate" or "throughput"))
        {if(!baseline.TryGetValue($"{metric.Name}:{metric.Aggregation}",out var old))continue;decimal regression;if(old.Value==0){if(metric.Name!="error_rate")continue;regression=metric.Value<=0?0m:100m;}else{var raw=(metric.Value-old.Value)/Math.Abs(old.Value)*100m;regression=metric.Name=="throughput"?-raw:raw;}var verdict=regression>=spec.FailureRegressionPercent?BenchmarkVerdict.Failed:regression>=spec.WarningRegressionPercent?BenchmarkVerdict.Warning:BenchmarkVerdict.Passed;list.Add(new BenchmarkComparison(Guid.NewGuid(),currentRunId,baselineRunId,$"{metric.Name}.{metric.Aggregation}",old.Value,metric.Value,regression,verdict,$"{metric.Name} changed by {regression:+0.###;-0.###;0}% relative to the matching environment/configuration baseline."));}return list;
    }
    private static List<BenchmarkMetric> BuildMetrics(Guid runId,IReadOnlyList<BenchmarkSample> samples)
    {
        var durations=samples.Where(x=>x.Passed).Select(x=>(decimal)x.DurationMicroseconds/1000m).OrderBy(x=>x).ToArray();var total=Math.Max(1,samples.Count);var errors=(decimal)samples.Count(x=>!x.Passed)/total*100m;var totalMeasuredMs=samples.Sum(x=>(decimal)x.DurationMicroseconds/1000m);var throughput=totalMeasuredMs<=0?0m:decimal.Round(samples.Count*1000m/totalMeasuredMs,4);return[new(Guid.NewGuid(),runId,"latency",Percentile(durations,.50m),"ms","p50"),new(Guid.NewGuid(),runId,"latency",Percentile(durations,.95m),"ms","p95"),new(Guid.NewGuid(),runId,"latency",Percentile(durations,.99m),"ms","p99"),new(Guid.NewGuid(),runId,"error_rate",decimal.Round(errors,4),"percent","rate"),new(Guid.NewGuid(),runId,"throughput",throughput,"ops/s","observed")];
    }
    private static decimal Percentile(decimal[] sorted,decimal p){if(sorted.Length==0)return 0m;var index=(sorted.Length-1)*p;var lo=(int)Math.Floor(index);var hi=(int)Math.Ceiling(index);if(lo==hi)return decimal.Round(sorted[lo],4);var fraction=index-lo;return decimal.Round(sorted[lo]+(sorted[hi]-sorted[lo])*fraction,4);}
    private static void Validate(BenchmarkExecutionSpec spec){if(string.IsNullOrWhiteSpace(spec.Target))throw new ArgumentException("Benchmark target is required.");if(spec.Mode is null||(!spec.Mode.Equals("http",StringComparison.OrdinalIgnoreCase)&&!spec.Mode.Equals("process",StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Benchmark mode must be http or process.");if(spec.MeasurementIterations<3)throw new ArgumentOutOfRangeException(nameof(spec.MeasurementIterations));if(spec.FailureRegressionPercent<spec.WarningRegressionPercent)throw new ArgumentException("Failure regression threshold must be >= warning threshold.");}
    private static string Sha256(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static BenchmarkDefinitionDto Map(BenchmarkDefinition x)=>new(x.Id,x.Key,x.Name,x.Kind,x.Enabled,JsonSerializer.Deserialize<BenchmarkExecutionSpec>(x.ConfigurationJson)!);
    private static BenchmarkMetricDto Map(BenchmarkMetric x)=>new(x.Name,x.Value,x.Unit,x.Aggregation);private static BenchmarkComparisonDto Map(BenchmarkComparison x)=>new(x.MetricName,x.BaselineValue,x.CurrentValue,x.DifferencePercent,x.Verdict.ToString(),x.Explanation);
}
