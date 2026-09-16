// Purpose: Defines the PortfolioManagementDto application contract.
namespace Dip.Application.Features.Management;

public sealed record PortfolioManagementDto(int Projects,int AtRisk,int Critical,IReadOnlyList<PortfolioProjectDto> Items,IReadOnlyList<SharedCapacityConflictDto> SharedCapacityConflicts,IReadOnlyList<string> ExecutiveActions,DateTimeOffset GeneratedAtUtc);
