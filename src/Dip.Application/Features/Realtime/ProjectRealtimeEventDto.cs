// Purpose: Defines data exchanged for P ro je ct Re al ti me Ev en tD to.
namespace Dip.Application.Features.Realtime;

public sealed record ProjectRealtimeEventDto(
    Guid ProjectId,
    string EventType,
    string Message,
    DateTimeOffset AtUtc);
