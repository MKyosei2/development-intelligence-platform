// Purpose: Coordinates Project Realtime Event DTO application responsibilities for realtime.
namespace Dip.Application.Features.Realtime;

public sealed record ProjectRealtimeEventDto(
    Guid ProjectId,
    string EventType,
    string Message,
    DateTimeOffset AtUtc);
