// Purpose: Defines the CreateApprovalRequestCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record CreateApprovalRequestCommand(Guid ProjectId,string Key,string ApprovalType,string Title,ManagementTarget Target,IReadOnlyList<string> RequiredRoles,string RequestedBy,string? Rationale=null,DateTimeOffset? DueAtUtc=null,string Status="Pending",bool AllowSameActorMultipleRoles=false);
