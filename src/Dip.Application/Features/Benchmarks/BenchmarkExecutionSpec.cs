// Purpose: Defines the BenchmarkExecutionSpec application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record BenchmarkExecutionSpec(string Mode,string Target,IReadOnlyList<string>? Arguments=null,string WorkspaceRelativePath=".",int WarmupIterations=3,int MeasurementIterations=20,int TimeoutSeconds=30,decimal WarningRegressionPercent=10m,decimal FailureRegressionPercent=20m);
