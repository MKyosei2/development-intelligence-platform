// Purpose: Coordinates P ro je ct Re al ti me Ev en tD to application behavior for Realtime.
namespace Dip.Application.Features.Realtime;

public sealed record ProjectRealtimeEventDto(
    Guid ProjectId,
    string EventType,
    string Message,
    DateTimeOffset AtUtc);
