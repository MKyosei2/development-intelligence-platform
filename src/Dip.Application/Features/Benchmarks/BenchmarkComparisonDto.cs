// Purpose: Defines the BenchmarkComparisonDto application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record BenchmarkComparisonDto(string MetricName,decimal BaselineValue,decimal CurrentValue,decimal DifferencePercent,string Verdict,string Explanation);
