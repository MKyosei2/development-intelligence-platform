// Purpose: Defines the GanttIntelligenceItemDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record GanttIntelligenceItemDto(string Key,string Name,string? ParentKey,string? Owner,DateTimeOffset? StartUtc,DateTimeOffset? DueAtUtc,decimal? PlannedPercent,decimal? ActualReadinessPercent,string Status,string Confidence,bool IsMilestone,IReadOnlyList<string> Dependencies,IReadOnlyList<string> Flags);
