// Purpose: Defines the RootCauseCorrelationDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record RootCauseCorrelationDto(string Fingerprint, string? LastKnownGoodCommit, string? FirstBadCommit, string? LastObservedCommit, string? Team, string? Workstream, int Occurrences, IReadOnlyList<string> CandidateCommits, string Explanation);
