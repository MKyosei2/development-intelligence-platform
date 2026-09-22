// Purpose: Defines the WorkCalendarOverviewDto application contract.
namespace Dip.Application.Features.Management;

public sealed record WorkCalendarOverviewDto(Guid ProjectId, IReadOnlyList<WorkCalendarDto> Calendars, DateTimeOffset GeneratedAtUtc);
