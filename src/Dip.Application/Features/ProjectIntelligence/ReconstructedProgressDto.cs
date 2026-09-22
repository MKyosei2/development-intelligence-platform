// Purpose: Defines the ReconstructedProgressDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ReconstructedProgressDto(string PlanKey, string Name, string? ParentKey, string? Owner, DateTimeOffset? PlannedStartUtc, DateTimeOffset? DueAtUtc, decimal? PlannedPercent, decimal? OperationalPercent, decimal? EvidenceReadinessPercent, string ReadinessConfidence, string Status, string? CanonicalTarget, bool IsMilestone, IReadOnlyList<string> Dependencies, IReadOnlyList<string> Contradictions, IReadOnlyList<string> StaleSignals, IReadOnlyList<ProgressEvidenceDto> Evidence, IReadOnlyList<SourceProvenanceDto> Sources);
