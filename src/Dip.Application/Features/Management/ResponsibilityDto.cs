// Purpose: Defines the ResponsibilityDto application contract.
namespace Dip.Application.Features.Management;

public sealed record ResponsibilityDto(string Key,string TargetType,string TargetKey,string? ResponsibleRole,string? AccountableRole,IReadOnlyList<string> ConsultedRoles,IReadOnlyList<string> InformedRoles,string Status);
