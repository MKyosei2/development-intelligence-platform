// Purpose: Defines the RaciOverviewDto application contract.
namespace Dip.Application.Features.Management;

public sealed record RaciOverviewDto(Guid ProjectId,int Assignments,int MissingAccountable,IReadOnlyList<ResponsibilityDto> Items,DateTimeOffset GeneratedAtUtc);
