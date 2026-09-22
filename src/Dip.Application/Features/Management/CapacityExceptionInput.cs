// Purpose: Defines the CapacityExceptionInput application contract.
namespace Dip.Application.Features.Management;

public sealed record CapacityExceptionInput(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, decimal Hours, string Category, string? Label = null);
