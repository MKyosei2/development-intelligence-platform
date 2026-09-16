// Purpose: Defines the SystemLivenessDto application contract.
namespace Dip.Application.Features.SystemStatus;

public sealed record SystemLivenessDto(
    bool Live,
    string State,
    DateTimeOffset CheckedAtUtc);
