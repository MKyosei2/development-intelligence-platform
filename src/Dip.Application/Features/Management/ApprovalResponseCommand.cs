// Purpose: Defines the ApprovalResponseCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record ApprovalResponseCommand(Guid ProjectId, string Key, string Actor, string ActorRole, string Decision, string? Comment = null);
