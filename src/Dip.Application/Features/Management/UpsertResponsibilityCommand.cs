// Purpose: Defines the UpsertResponsibilityCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record UpsertResponsibilityCommand(Guid ProjectId,string Key,ManagementTarget Target,string? ResponsibleRole,string? AccountableRole,IReadOnlyList<string>? ConsultedRoles=null,IReadOnlyList<string>? InformedRoles=null,string Status="Active");
