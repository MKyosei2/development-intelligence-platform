// Purpose: Defines the BenchmarkMetricDto application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record BenchmarkMetricDto(string Name,decimal Value,string Unit,string Aggregation);
