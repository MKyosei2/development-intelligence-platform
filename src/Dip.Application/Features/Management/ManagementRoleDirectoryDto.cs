// Purpose: Defines the ManagementRoleDirectoryDto application contract.
namespace Dip.Application.Features.Management;

public sealed record ManagementRoleDirectoryDto(Guid ProjectId, string CurrentActor, IReadOnlyList<ManagementRoleBindingDto> Bindings, DateTimeOffset GeneratedAtUtc);
