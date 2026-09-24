// Purpose: Defines P ro je ct Re al ti me Ev en t data exchanged by r ea lt im e.
namespace Dip.Application.Features.Realtime;

public sealed record ProjectRealtimeEventDto(
    Guid ProjectId,
    string EventType,
    string Message,
    DateTimeOffset AtUtc);
