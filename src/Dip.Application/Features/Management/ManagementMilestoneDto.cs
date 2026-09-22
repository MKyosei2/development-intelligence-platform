// Purpose: Defines the ManagementMilestoneDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementMilestoneDto(string Key, string Name, string State, DateTimeOffset PlannedStartUtc, DateTimeOffset DueAtUtc, decimal RecordedCompletionPercent, decimal LinkedTaskCompletionPercent, int LinkedTasks, int BlockedTasks, bool IsPastDue, string Readiness);
