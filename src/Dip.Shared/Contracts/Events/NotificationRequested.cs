// Purpose: Defines N ot if ic at io nR eq ue st ed repository behavior.
namespace Dip.Shared.Contracts.Events;

public sealed record NotificationRequested(
    Guid ProjectId,
    string Channel,
    string Destination,
    string Subject,
    string Body);
