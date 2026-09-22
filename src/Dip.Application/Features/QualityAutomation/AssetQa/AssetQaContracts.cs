// Purpose: Defines data exchanged across this application boundary.
using Dip.Application.Features.ProductionTools;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation.AssetQa;

public sealed record AssetQaRuleResult(
    string RuleId,
    AssetQaSeverity Severity,
    string Title,
    string Message,
    string? RelativePath,
    IReadOnlyDictionary<string, object?> Details);

public sealed record AssetQaFindingDto(
    Guid Id,
    Guid ProjectId,
    Guid EvidenceId,
    string Source,
    string RuleId,
    AssetQaSeverity Severity,
    string Title,
    string Message,
    string? RelativePath,
    string Fingerprint,
    AssetQaFindingState State,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public sealed record AssetQaEvaluationResult(
    Guid EvidenceId,
    ProductionToolSource Source,
    int RulesEvaluated,
    int FindingsCreated,
    IReadOnlyList<AssetQaFindingDto> Findings);
