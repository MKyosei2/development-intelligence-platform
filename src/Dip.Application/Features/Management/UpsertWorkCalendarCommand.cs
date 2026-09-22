// Purpose: Defines the UpsertWorkCalendarCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record UpsertWorkCalendarCommand(Guid ProjectId, string Key, string Name, string? TeamKey, decimal HoursPerDay = 8m, IReadOnlyList<DayOfWeek>? WorkingDays = null, decimal RecurringMeetingHoursPerWeek = 0m, decimal SupportDutyHoursPerWeek = 0m, IReadOnlyList<CapacityExceptionInput>? Exceptions = null, string TimeZone = "UTC", string Status = "Active");
