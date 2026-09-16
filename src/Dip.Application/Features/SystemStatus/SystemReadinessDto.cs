// Purpose: Defines the SystemReadinessDto application contract.
namespace Dip.Application.Features.SystemStatus;

public sealed record SystemReadinessDto(
    bool Ready,
    string State,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<string> UnavailableComponents,
    IReadOnlyList<string> DegradedComponents);
