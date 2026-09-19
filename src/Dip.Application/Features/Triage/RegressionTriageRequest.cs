// Purpose: Defines the input used to perform evidence-backed regression triage.
namespace Dip.Application.Features.Triage;

public sealed record RegressionTriageRequest(
    Guid ProjectId,
    Guid IncidentId,
    string? SuspectedSubsystem = null,
    IReadOnlyList<string>? SuspectedFiles = null,
    string? SuggestedOwner = null);
