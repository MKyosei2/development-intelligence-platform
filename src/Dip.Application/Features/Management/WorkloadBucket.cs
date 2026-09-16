// Purpose: Defines WorkloadBucket.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record WorkloadBucket(DateTimeOffset WeekStartUtc,decimal CapacityHours,decimal PlannedHours,decimal UtilizationPercent,int ActiveTasks,int BlockedTasks,string Status);
