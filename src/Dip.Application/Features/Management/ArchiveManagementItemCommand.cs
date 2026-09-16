// Purpose: Defines the ArchiveManagementItemCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record ArchiveManagementItemCommand(Guid ProjectId,string Type,string Key,string Actor,string? Reason=null);
