// Purpose: Defines the QaRunResultDto application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaRunResultDto(Guid QaRunId, string Status, string Language, int FindingCount, int CriticalFindingCount, int WarningFindingCount, IReadOnlyList<QaFindingDto> Findings, string ArtifactReference, string? SummaryJson = null, Guid? PipelineRunId = null);
