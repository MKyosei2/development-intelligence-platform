// Purpose: Defines a focused responsibility within the development intelligence platform.
namespace Dip.Shared.Contracts.Events;

public sealed record NotificationRequested(
    Guid ProjectId,
    string Channel,
    string Destination,
    string Subject,
    string Body);
