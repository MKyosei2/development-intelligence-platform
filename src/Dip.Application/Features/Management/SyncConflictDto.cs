// Purpose: Defines the SyncConflictDto application contract.
namespace Dip.Application.Features.Management;

public sealed record SyncConflictDto(string Key,string Provider,string DataType,string ItemKey,string DipValue,string ExternalValue,string Status,string Resolution,string? ResolvedBy,DateTimeOffset DetectedAtUtc,DateTimeOffset? ResolvedAtUtc);
