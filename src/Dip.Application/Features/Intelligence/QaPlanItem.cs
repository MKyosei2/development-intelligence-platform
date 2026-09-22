// Purpose: Defines the QaPlanItem application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record QaPlanItem(string Kind, string Reason, int EstimatedMinutes, decimal RiskWeight, bool Required);
