// Purpose: Defines the RiskRegisterItemDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record RiskRegisterItemDto(string Key,string Title,string Description,string Status,string Category,decimal ProbabilityPercent,decimal ImpactPercent,decimal ExposureScore,string ExposureLevel,string? OwnerRole,DateTimeOffset? ReviewDueUtc,bool ReviewOverdue,string? Trigger,string? Mitigation,IReadOnlyList<string> AffectedItems,string ManagementAction);
