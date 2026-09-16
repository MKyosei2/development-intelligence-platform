// Purpose: Defines the RiskRegisterDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record RiskRegisterDto(Guid ProjectId,int Open,int Critical,int ReviewOverdue,decimal TotalExposure,IReadOnlyList<RiskRegisterItemDto> Items,DateTimeOffset GeneratedAtUtc);
