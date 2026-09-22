// Purpose: Defines the CrossProjectBenchmarkMetricDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record CrossProjectBenchmarkMetricDto(Guid ProjectId, string ProjectName, Guid RunId, string CommitSha, string EnvironmentFingerprint, decimal Value, string Unit, string Aggregation, decimal DifferenceFromBestPercent);
