// Purpose: Defines the ResolveSyncConflictCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record ResolveSyncConflictCommand(Guid ProjectId, string ConflictKey, string Resolution, string ResolvedBy);
