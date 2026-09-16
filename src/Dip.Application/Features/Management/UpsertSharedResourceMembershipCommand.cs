// Purpose: Defines the UpsertSharedResourceMembershipCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record UpsertSharedResourceMembershipCommand(Guid ProjectId,string TeamKey,string? SharedResourceKey,decimal? SharedPoolCapacityHoursPerWeek);
