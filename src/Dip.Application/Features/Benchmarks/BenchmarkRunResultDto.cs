// Purpose: Defines the BenchmarkRunResultDto application contract.
namespace Dip.Application.Features.Benchmarks;

public sealed record BenchmarkRunResultDto(Guid RunId, string Status, IReadOnlyList<BenchmarkMetricDto> Metrics, IReadOnlyList<BenchmarkComparisonDto> Comparisons, Guid? SummaryArtifactId, string Summary);
