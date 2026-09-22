// Purpose: Defines the NotificationHistoryEntryDto application contract.
namespace Dip.Application.Features.Management;

public sealed record NotificationHistoryEntryDto(Guid Id, string AlertKey, string Action, string Actor, string? Comment, string? DetailJson, DateTimeOffset AtUtc);
