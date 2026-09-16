// Purpose: Defines the CrossProjectBenchmarkComparisonDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record CrossProjectBenchmarkComparisonDto(string DefinitionKey,string MetricName,IReadOnlyList<CrossProjectBenchmarkMetricDto> Projects,string Caution);
