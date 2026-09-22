// Purpose: Defines the ProjectIntelligenceLayerDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectIntelligenceLayerDto(Guid ProjectId, ProjectProgressOverviewDto Progress, WbsGanttIntelligenceDto Gantt, EntityResolutionOverviewDto EntityResolution, ProjectManagementDigestDto Digest, DateTimeOffset GeneratedAtUtc);
