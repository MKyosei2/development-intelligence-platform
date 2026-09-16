// Purpose: Defines the BlockerOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record BlockerOverviewDto(Guid ProjectId,int Open,int Critical,int AgingOverFiveDays,IReadOnlyList<BlockerImpactDto> Items,DateTimeOffset GeneratedAtUtc);
