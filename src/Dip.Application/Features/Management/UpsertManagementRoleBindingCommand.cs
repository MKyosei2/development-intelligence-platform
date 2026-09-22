// Purpose: Defines the UpsertManagementRoleBindingCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record UpsertManagementRoleBindingCommand(Guid ProjectId, string Key, string ActorIdentity, string Role, string? DisplayName = null, string Status = "Active");
