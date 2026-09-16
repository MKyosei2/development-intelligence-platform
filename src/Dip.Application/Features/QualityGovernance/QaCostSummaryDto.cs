// Purpose: Defines the QaCostSummaryDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaCostSummaryDto(long Runs,long TotalWallDurationMs,long TotalToolDurationMs,long TotalArtifactBytes,decimal EstimatedCpuSeconds,long OverBudgetRuns);
