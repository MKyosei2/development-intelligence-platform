namespace Dip.Shared.Contracts.Events;

public sealed record NotificationRequested(
    Guid ProjectId,
    string Channel,
    string Destination,
    string Subject,
    string Body);
