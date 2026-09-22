// Purpose: Defines the ProjectManagementDigestDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectManagementDigestDto(Guid ProjectId, string OverallStatus, string ExecutiveSummary, IReadOnlyList<string> NeedsAttention, IReadOnlyList<string> EvidenceGaps, IReadOnlyList<string> SourceContradictions, IReadOnlyList<string> RecentProgress, IReadOnlyList<string> RecommendedActions, DateTimeOffset GeneratedAtUtc);
