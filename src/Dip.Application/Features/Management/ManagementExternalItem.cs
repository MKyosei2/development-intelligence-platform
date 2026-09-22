// Purpose: Defines the ManagementExternalItem application contract.
namespace Dip.Application.Features.Management;

public sealed record ManagementExternalItem(string Provider, string ExternalId, string Key, string Title, string Status, string? OwnerRole, DateTimeOffset? DueAtUtc, DateTimeOffset UpdatedAtUtc, string Revision, string Url, IReadOnlyDictionary<string, string>? Fields = null);
