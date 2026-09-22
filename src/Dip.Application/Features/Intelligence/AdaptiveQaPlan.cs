// Purpose: Defines AdaptiveQaPlan.
namespace Dip.Application.Features.Intelligence;

public sealed record AdaptiveQaPlan(IReadOnlyList<QaPlanItem> Items, int EstimatedMinutes, decimal HistoricalRisk, string Rationale);
