// Purpose: Defines the ManagementMilestoneOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementMilestoneOverviewDto(Guid ProjectId,int Total,int AtRisk,int Blocked,int Completed,IReadOnlyList<ManagementMilestoneDto> Milestones,DateTimeOffset GeneratedAtUtc);
