// Purpose: Defines the PmDecisionItem application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record PmDecisionItem(int Priority, string Subject, string Situation, string Why, string NextAction, string Confidence);
