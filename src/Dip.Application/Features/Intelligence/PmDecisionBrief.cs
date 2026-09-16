// Purpose: Defines the PmDecisionBrief application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record PmDecisionBrief(IReadOnlyList<PmDecisionItem> Priorities,IReadOnlyList<string> Changes,IReadOnlyList<string> HealthChangeReasons,string Summary);
