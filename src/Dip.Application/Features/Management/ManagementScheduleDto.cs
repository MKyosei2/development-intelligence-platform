// Purpose: Defines the ManagementScheduleDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementScheduleDto(Guid ProjectId,string Status,DateTimeOffset? PlannedFinishUtc,DateTimeOffset? ForecastFinishUtc,int DelayDays,int CriticalItems,int AtRiskItems,IReadOnlyList<ManagementScheduleItem> Items,IReadOnlyList<string> CriticalPath,IReadOnlyList<string> Warnings,DateTimeOffset GeneratedAtUtc);
