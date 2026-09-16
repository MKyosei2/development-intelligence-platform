// Purpose: Defines the AlertDispositionCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record AlertDispositionCommand(Guid ProjectId,string AlertKey,string Actor,string Disposition,DateTimeOffset? SnoozeUntilUtc=null,string? Comment=null);
