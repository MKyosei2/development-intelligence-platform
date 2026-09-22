// Purpose: Defines the TeamCapacityDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record TeamCapacityDto(string Key, string Name, decimal CapacityHours, decimal PlannedHours, decimal RemainingHours, decimal UtilizationPercent, int Workstreams, int ActiveTasks, int BlockedTasks, string Status, IReadOnlyList<WorkloadBucket> Weekly, string? SharedResourceKey = null, decimal? SharedPoolCapacityHoursPerWeek = null);
