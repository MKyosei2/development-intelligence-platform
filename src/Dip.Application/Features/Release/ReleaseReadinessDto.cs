// Purpose: Defines the immutable release-readiness result exposed by the application layer.
namespace Dip.Application.Features.Release;

public sealed record ReleaseReadinessDto(
    Guid Id,
    string CommitSha,
    string Decision,
    decimal Confidence,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> RequiredActions,
    string Summary,
    DateTimeOffset EvaluatedAtUtc);
