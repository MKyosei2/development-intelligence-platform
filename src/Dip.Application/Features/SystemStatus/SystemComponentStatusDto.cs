// Purpose: Defines the SystemComponentStatusDto application contract.
namespace Dip.Application.Features.SystemStatus;

public sealed record SystemComponentStatusDto(
    string Key,
    string Name,
    string State,
    string Detail,
    DateTimeOffset CheckedAtUtc,
    double? DurationMs = null);
