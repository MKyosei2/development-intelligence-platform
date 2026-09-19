// Purpose: Defines the request used to evaluate release readiness for an exact project revision.
namespace Dip.Application.Features.Release;

public sealed record EvaluateReleaseReadinessRequest(
    Guid ProjectId,
    string CommitSha,
    int EvidenceFreshnessDays = 14);
