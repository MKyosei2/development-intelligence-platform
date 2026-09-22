// Purpose: Defines the WbsGanttIntelligenceDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record WbsGanttIntelligenceDto(Guid ProjectId, DateTimeOffset? RangeStartUtc, DateTimeOffset? RangeEndUtc, IReadOnlyList<GanttIntelligenceItemDto> Items, DateTimeOffset GeneratedAtUtc);
