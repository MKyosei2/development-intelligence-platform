// Purpose: Defines the WorkstreamCapacityDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record WorkstreamCapacityDto(string Key,string Name,string TeamKey,string TeamName,decimal CapacityHours,decimal PlannedHours,decimal RemainingHours,decimal UtilizationPercent,int ActiveTasks,int BlockedTasks,string Status,IReadOnlyList<WorkloadBucket> Weekly);
