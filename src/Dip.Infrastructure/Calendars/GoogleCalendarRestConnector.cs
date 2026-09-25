// Purpose: Implements G oo gl eC al en da rR es tC on ne ct or infrastructure behavior for c al en da rs.
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Dip.Application.Abstractions.Calendars;

namespace Dip.Infrastructure.Calendars;

public sealed class GoogleCalendarRestConnector : IGoogleCalendarConnector
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(45) };

    public async Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(string calendarId, string accessToken, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(calendarId)) throw new ArgumentException("CalendarId is required.", nameof(calendarId));
        if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("A Google OAuth access token is required.", nameof(accessToken));
        if (toUtc <= fromUtc) throw new ArgumentException("Calendar sync end must be after start.", nameof(toUtc));

        var result = new List<ExternalCalendarEvent>();
        string? pageToken = null;
        do
        {
            var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events?singleEvents=true&orderBy=startTime&maxResults=2500&timeMin={Uri.EscapeDataString(fromUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}&timeMax={Uri.EscapeDataString(toUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}"
                + (string.IsNullOrWhiteSpace(pageToken) ? string.Empty : $"&pageToken={Uri.EscapeDataString(pageToken)}");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await Http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Google Calendar API returned {(int)response.StatusCode}: {body[..Math.Min(body.Length, 1200)]}");

            using var doc = JsonDocument.Parse(body);
            var calendarZone = ResolveTimeZone(String(doc.RootElement, "timeZone"));
            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var status = String(item, "status");
                    if (status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)) continue;
                    var id = String(item, "id");
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var summary = String(item, "summary");
                    var eventZone = EventTimeZone(item, calendarZone);
                    if (!TryEndpoint(item, "start", eventZone, out var start, out var allDay) || !TryEndpoint(item, "end", eventZone, out var end, out _)) continue;
                    if (end <= start) continue;
                    result.Add(new(id, summary, start.ToUniversalTime(), end.ToUniversalTime(), allDay, status));
                }
            }
            pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString() : null;
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return result;
    }

    private static TimeZoneInfo EventTimeZone(JsonElement item, TimeZoneInfo fallback)
    {
        foreach (var endpointName in new[] { "start", "end" })
        {
            if (item.TryGetProperty(endpointName, out var endpoint) && endpoint.ValueKind == JsonValueKind.Object && endpoint.TryGetProperty("timeZone", out var tz) && tz.ValueKind == JsonValueKind.String)
            {
                var resolved = ResolveTimeZone(tz.GetString());
                if (resolved != TimeZoneInfo.Utc || string.Equals(tz.GetString(), "UTC", StringComparison.OrdinalIgnoreCase)) return resolved;
            }
        }
        return fallback;
    }

    private static bool TryEndpoint(JsonElement item, string name, TimeZoneInfo timeZone, out DateTimeOffset value, out bool allDay)
    {
        value = default;
        allDay = false;
        if (!item.TryGetProperty(name, out var endpoint) || endpoint.ValueKind != JsonValueKind.Object) return false;
        if (endpoint.TryGetProperty("dateTime", out var dt) && dt.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(dt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value)) return true;
        if (endpoint.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String && DateTime.TryParseExact(d.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            var local = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
            value = new DateTimeOffset(local, timeZone.GetUtcOffset(local));
            allDay = true;
            return true;
        }
        return false;
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    private static string String(JsonElement e, string name) => e.TryGetProperty(name, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() ?? string.Empty : string.Empty;
}
