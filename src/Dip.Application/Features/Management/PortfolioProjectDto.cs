// Purpose: Defines the PortfolioProjectDto application contract.
namespace Dip.Application.Features.Management;

public sealed record PortfolioProjectDto(Guid ProjectId, string Name, string OverallStatus, decimal HealthScore, string ScheduleStatus, int DelayDays, decimal PeakUtilizationPercent, int CriticalRisks, int OpenBlockers, int PendingDecisions, int OverdueActions, int PendingApprovals, int OpenSyncConflicts);
