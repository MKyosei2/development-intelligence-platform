// Purpose: Defines the SystemStatusDto application contract.
namespace Dip.Application.Features.SystemStatus;

public sealed record SystemStatusDto(
    string Overall,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<SystemComponentStatusDto> Components)
{
    public ContinuousOperationsDto? Operations { get; init; }
}
