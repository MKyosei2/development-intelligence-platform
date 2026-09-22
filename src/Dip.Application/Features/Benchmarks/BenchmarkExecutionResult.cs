// Purpose: Defines the BenchmarkExecutionResult application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record BenchmarkExecutionResult(IReadOnlyList<BenchmarkSample> Samples, string EnvironmentFingerprint, string DetailJson);
