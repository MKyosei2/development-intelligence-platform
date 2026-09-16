// Purpose: Defines the ApprovalRequestDto application contract.
namespace Dip.Application.Features.Management;

public sealed record ApprovalRequestDto(string Key,string ApprovalType,string Title,string TargetType,string TargetKey,string Status,string RequestedBy,string? Rationale,DateTimeOffset RequestedAtUtc,DateTimeOffset? DueAtUtc,IReadOnlyList<string> RequiredRoles,IReadOnlyList<ApprovalResponseDto> Responses,IReadOnlyList<string> PendingRoles,bool Overdue,bool AllowSameActorMultipleRoles=false);
