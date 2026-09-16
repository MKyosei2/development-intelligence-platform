// Purpose: Defines the ApprovalOverviewDto application contract.
namespace Dip.Application.Features.Management;

public sealed record ApprovalOverviewDto(Guid ProjectId,int Pending,int Approved,int Rejected,int Overdue,IReadOnlyList<ApprovalRequestDto> Items,DateTimeOffset GeneratedAtUtc);
