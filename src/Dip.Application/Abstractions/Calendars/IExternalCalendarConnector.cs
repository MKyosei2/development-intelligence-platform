// Purpose: Defines the application boundary represented by I Ex te rn al Ca le nd ar Co nn ec to r.
namespace Dip.Application.Abstractions.Calendars;

public sealed record ExternalCalendarEvent(string Id,string Summary,DateTimeOffset StartsAtUtc,DateTimeOffset EndsAtUtc,bool AllDay,string Status);

public interface IGoogleCalendarConnector
{
    Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(string calendarId,string accessToken,DateTimeOffset fromUtc,DateTimeOffset toUtc,CancellationToken ct);
}
