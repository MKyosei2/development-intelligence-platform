// Purpose: Defines the RootCauseCandidate application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record RootCauseCandidate(string Cause,decimal ConfidencePercent,IReadOnlyList<string> Evidence,string? SuggestedAction);
