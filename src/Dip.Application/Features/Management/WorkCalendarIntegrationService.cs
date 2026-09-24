// Purpose: Coordinates W or kC al en da rI nt eg ra ti on application operations for m an ag em en t.
using Dip.Application.Abstractions.Calendars;
using Dip.Application.Abstractions.Time;

namespace Dip.Application.Features.Management;

public sealed record GoogleCalendarSyncCommand(Guid ProjectId, string WorkCalendarKey, string CalendarId, string AccessToken, DateTimeOffset FromUtc, DateTimeOffset ToUtc, string? TeamKey = null, string TimeZone = "Asia/Tokyo", bool DryRun = false);
public sealed record GoogleCalendarSyncResult(string WorkCalendarKey, int Events, int Pto, int Holidays, int Meetings, int Support, decimal CapacityHoursRemoved, bool DryRun, DateTimeOffset SyncedAtUtc);

public sealed class WorkCalendarIntegrationService(IGoogleCalendarConnector google, ManagementOperationsService operations, ISystemClock clock)
{
    public async Task<GoogleCalendarSyncResult> SyncGoogleAsync(GoogleCalendarSyncCommand c, CancellationToken ct)
    {
        if (c.ProjectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(c));
        if (string.IsNullOrWhiteSpace(c.WorkCalendarKey)) throw new ArgumentException("WorkCalendarKey is required.", nameof(c));
        if (c.ToUtc <= c.FromUtc) throw new ArgumentException("ToUtc must be after FromUtc.", nameof(c));
        var overview = await operations.GetCalendarsAsync(c.ProjectId, ct);
         var current = overview.Calendars.FirstOrDefault(x => x.Key.Equals(c.WorkCalendarKey, StringComparison.OrdinalIgnoreCase));
         var hoursPerDay = current?.HoursPerDay ?? 8m;
         var workingDays = (current?.WorkingDays ?? new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }).ToHashSet();
         var timeZone = ResolveTimeZone(current?.TimeZone ?? c.TimeZone);
         var events = await google.ListEventsAsync(c.CalendarId, c.AccessToken, c.FromUtc, c.ToUtc, ct);
         var prefix = $"[gcal:{Stable(c.CalendarId)}:";
         var retained = (current?.Exceptions ?? Array.Empty<CapacityExceptionInput>()).Where(x => x.Label is null || !x.Label.StartsWith(prefix, StringComparison.Ordinal)).ToList();
         var pto = 0;
         var holidays = 0;
         var meetings = 0;
         var support = 0;
         decimal removed = 0m;
        foreach (var e in events)
        {
            var category = Classify(e.Summary);
            switch (category) { case "PTO": pto++;
             break;
             case "Holiday": holidays++;
             break;
             case "Support": support++;
             break;
             default: meetings++;
             break;
             }
            if (e.AllDay)
            {
                var localStart = TimeZoneInfo.ConvertTime(e.StartsAtUtc, timeZone).Date; var localEnd = TimeZoneInfo.ConvertTime(e.EndsAtUtc, timeZone).Date;
                for (var day = localStart; day < localEnd; day = day.AddDays(1))
                {
                    if (!workingDays.Contains(day.DayOfWeek)) continue;
                     var start = ToUtc(day, timeZone);
                     var end = ToUtc(day.AddDays(1), timeZone);
                     retained.Add(new(start, end, hoursPerDay, category, $"{prefix}{e.Id}:{day:yyyyMMdd}] {e.Summary}"));
                     removed += hoursPerDay;
                }
                continue;
            }
            var duration = (decimal)Math.Max(0, (e.EndsAtUtc - e.StartsAtUtc).TotalHours);
             var hours = Math.Min(duration, 24m * 14m);
             removed += hours;
             retained.Add(new(e.StartsAtUtc, e.EndsAtUtc, hours, category, $"{prefix}{e.Id}] {e.Summary}"));
        }
        if (!c.DryRun) await operations.UpsertWorkCalendarAsync(new(c.ProjectId, c.WorkCalendarKey, current?.Name ?? "Project calendar", string.IsNullOrWhiteSpace(c.TeamKey) ? current?.TeamKey : c.TeamKey, hoursPerDay, current?.WorkingDays, current?.RecurringMeetingHoursPerWeek ?? 0m, current?.SupportDutyHoursPerWeek ?? 0m, retained, current?.TimeZone ?? c.TimeZone, current?.Status ?? "Active"), ct);
        return new(c.WorkCalendarKey, events.Count, pto, holidays, meetings, support, Math.Round(removed, 2), c.DryRun, clock.UtcNow);
    }

    private static string Classify(string summary)
    {
        var s = (summary ?? string.Empty).ToLowerInvariant();
        if (Has(s, "pto", "vacation", "annual leave", "leave", "休暇", "有給", "欠勤")) return "PTO";
        if (Has(s, "holiday", "public holiday", "祝日", "休日")) return "Holiday";
        if (Has(s, "on-call", "on call", "support duty", "当番", "障害対応", "サポート")) return "Support";
         return "Meeting";
    }
    private static bool Has(string value, params string[] terms) => terms.Any(value.Contains);
    private static TimeZoneInfo ResolveTimeZone(string? id) { if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
     try { return TimeZoneInfo.FindSystemTimeZoneById(id);
     }
    catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc;
     }
    catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc;
     } }
    private static DateTimeOffset ToUtc(DateTime localDate, TimeZoneInfo timeZone) { var local = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified); return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime(); }
    private static string Stable(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..12];
}
