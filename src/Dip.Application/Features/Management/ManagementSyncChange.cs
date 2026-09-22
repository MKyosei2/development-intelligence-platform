// Purpose: Defines the ManagementSyncChange application contract.
namespace Dip.Application.Features.Management;

public sealed record ManagementSyncChange(string Operation, string Key, string Provider, string? ExternalId, string Reason, string? DipValue, string? ExternalValue, string? Field = null);
