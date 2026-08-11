namespace Dip.Application.Features.Benchmarks;

public sealed record CreateBenchmarkDefinitionCommand(string Key,string Name,string Kind,BenchmarkExecutionSpec Configuration,bool Enabled=true);
public sealed record BenchmarkExecutionSpec(
    string Mode,
    string Target,
    IReadOnlyList<string>? Arguments=null,
    string WorkspaceRelativePath=".",
    int WarmupIterations=3,
    int MeasurementIterations=20,
    int TimeoutSeconds=30,
    decimal WarningRegressionPercent=10m,
    decimal FailureRegressionPercent=20m);
public sealed record BenchmarkJobRequest(Guid ProjectId,Guid DefinitionId,string SubjectKey,string CommitSha,string? EnvironmentFingerprint=null,Guid? PerformanceBudgetProfileId=null);
public sealed record BenchmarkDefinitionDto(Guid Id,string Key,string Name,string Kind,bool Enabled,BenchmarkExecutionSpec Configuration);
public sealed record BenchmarkSample(int Iteration,bool Passed,bool TimedOut,int? StatusCode,long DurationMicroseconds,string? Error);
public sealed record BenchmarkExecutionResult(IReadOnlyList<BenchmarkSample> Samples,string EnvironmentFingerprint,string DetailJson);
public interface IBenchmarkExecutor{Task<BenchmarkExecutionResult> ExecuteAsync(BenchmarkExecutionSpec spec,CancellationToken cancellationToken);}
public sealed record BenchmarkMetricDto(string Name,decimal Value,string Unit,string Aggregation);
public sealed record BenchmarkComparisonDto(string MetricName,decimal BaselineValue,decimal CurrentValue,decimal DifferencePercent,string Verdict,string Explanation);
public sealed record BenchmarkRunResultDto(Guid RunId,string Status,IReadOnlyList<BenchmarkMetricDto> Metrics,IReadOnlyList<BenchmarkComparisonDto> Comparisons,Guid? SummaryArtifactId,string Summary);
