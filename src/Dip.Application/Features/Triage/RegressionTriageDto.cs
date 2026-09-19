// Purpose: Defines the regression-triage result returned by the application layer.
namespace Dip.Application.Features.Triage;

public sealed record RegressionTriageDto(
    Guid Id,
    Guid IncidentId,
    string? SuspectedCommit,
    string? SuspectedSubsystem,
    IReadOnlyList<string> SuspectedFiles,
    string? SuggestedOwner,
    decimal Confidence,
    IReadOnlyList<Guid> EvidenceIds,
    string Explanation,
    DateTimeOffset CreatedAtUtc);
