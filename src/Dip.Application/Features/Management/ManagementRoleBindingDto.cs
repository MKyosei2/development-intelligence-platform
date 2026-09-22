// Purpose: Defines the ManagementRoleBindingDto application contract.
namespace Dip.Application.Features.Management;

public sealed record ManagementRoleBindingDto(string Key, string ActorIdentity, string Role, string? DisplayName, string Status);
