// Purpose: Defines the ManagementScheduleItem application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementScheduleItem(Guid NodeId,string Type,string Key,string Name,string WorkstreamKey,DateTimeOffset PlannedStartUtc,DateTimeOffset DueAtUtc,DateTimeOffset ForecastFinishUtc,int DurationDays,decimal CompletionPercent,string Status,int SlackDays,bool IsCritical,int BlockedByCount);
