// Purpose: Defines the ForecastResult application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record ForecastResult(decimal ReleaseRiskPercent, string Confidence, DateTimeOffset? EarliestCompletionUtc, DateTimeOffset? LatestCompletionUtc, IReadOnlyList<string> Reasons);
