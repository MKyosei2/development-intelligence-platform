// Purpose: Defines the IBenchmarkExecutor application boundary.
namespace Dip.Application.Features.Benchmarks;

public interface IBenchmarkExecutor{Task<BenchmarkExecutionResult> ExecuteAsync(BenchmarkExecutionSpec spec,CancellationToken cancellationToken);}
