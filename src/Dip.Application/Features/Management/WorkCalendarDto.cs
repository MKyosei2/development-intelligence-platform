// Purpose: Defines the WorkCalendarDto application contract.
namespace Dip.Application.Features.Management;

public sealed record WorkCalendarDto(string Key,string Name,string? TeamKey,decimal HoursPerDay,IReadOnlyList<DayOfWeek> WorkingDays,decimal RecurringMeetingHoursPerWeek,decimal SupportDutyHoursPerWeek,IReadOnlyList<CapacityExceptionInput> Exceptions,string TimeZone,string Status);
