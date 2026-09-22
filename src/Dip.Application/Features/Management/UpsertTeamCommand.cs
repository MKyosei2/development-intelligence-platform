// Purpose: Defines the UpsertTeamCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertTeamCommand(Guid ProjectId, string Key, string Name, decimal CapacityHoursPerWeek, decimal BufferPercent = 10m, string Status = "Active", string? ExternalReference = null, string? SharedResourceKey = null, decimal? SharedPoolCapacityHoursPerWeek = null);
