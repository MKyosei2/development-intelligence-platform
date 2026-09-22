// Purpose: Defines the RootCauseResult application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record RootCauseResult(IReadOnlyList<RootCauseCandidate> Candidates, string GroundingStatus);
