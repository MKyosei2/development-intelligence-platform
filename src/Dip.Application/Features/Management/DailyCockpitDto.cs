// Purpose: Defines the DailyCockpitDto application contract.
namespace Dip.Application.Features.Management;

public sealed record DailyCockpitDto(Guid ProjectId,string OverallStatus,decimal HealthScore,IReadOnlyList<CockpitDecisionDto> Now,IReadOnlyList<OperationalAlertDto> Alerts,IReadOnlyList<ManagementActionDto> MyOpenActions,IReadOnlyList<ManagementDecisionDto> PendingDecisions,IReadOnlyList<ApprovalRequestDto> PendingApprovals,IReadOnlyList<string> NextActions,DateTimeOffset GeneratedAtUtc);
