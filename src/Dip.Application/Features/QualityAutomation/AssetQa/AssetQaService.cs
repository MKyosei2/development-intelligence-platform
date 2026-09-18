// Purpose: Orchestrates the DIP workflow for A ss et QA Se rv ic e.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Evidence;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.ProductionTools;
using Dip.Application.Features.QualityAutomation.VisualQa;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation.AssetQa;

public sealed class AssetQaService
{
    private readonly IQualityAutomationRepository _repository;
    private readonly VisualQaService _profiles;
    private readonly AssetQaRuleEngine _rules;
    private readonly EvidenceIngestionService _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly ProductionGraphProjectionService _graph;

    public AssetQaService(
        IQualityAutomationRepository repository,
        VisualQaService profiles,
        AssetQaRuleEngine rules,
        EvidenceIngestionService evidence,
        IUnitOfWork uow,
        ISystemClock clock,
        ProductionGraphProjectionService graph)
    {
        _repository = repository;
        _profiles = profiles;
        _rules = rules;
        _evidence = evidence;
        _uow = uow;
        _clock = clock;
        _graph = graph;
    }

    public async Task<AssetQaEvaluationResult> EvaluateAsync(
        Guid projectId,
        Guid sourceEvidenceId,
        ProductionToolObservation observation,
        CancellationToken ct)
    {
        var profile = await _profiles.GetProfileAsync(projectId, ct);
        if (!profile.Enabled) return new(sourceEvidenceId, observation.Source, 0, 0, Array.Empty<AssetQaFindingDto>());
        var results = _rules.Evaluate(observation, profile.AssetRulesJson);
        var created = new List<AssetQaFindingDto>();
        foreach (var result in results)
        {
            var fingerprint = Fingerprint(projectId, observation.Source, result.RuleId, result.RelativePath);
            if (await _repository.FindOpenFindingAsync(projectId, fingerprint, ct) is not null) continue;
            var finding = new AssetQaFindingRecord(
                Guid.NewGuid(), projectId, sourceEvidenceId, observation.Source.ToString(), result.RuleId,
                result.Severity, result.Title, result.Message, result.RelativePath, fingerprint,
                JsonSerializer.Serialize(result.Details), observation.ObservedAtUtc == default ? _clock.UtcNow : observation.ObservedAtUtc);
            await _repository.AddFindingAsync(finding, ct);
            await _uow.SaveChangesAsync(ct);

            await _evidence.IngestAsync(new IngestEvidenceCommand(
                projectId,
                EvidenceSourceType.Quality,
                $"assetqa:{observation.Source.ToString().ToLowerInvariant()}",
                finding.Id.ToString("N"),
                EvidenceEventType.AssetQaFinding,
                $"asset:{NormalizeSubject(result.RelativePath)}",
                $"qa.asset.{result.RuleId}",
                result.Title,
                result.Message,
                result.Severity switch
                {
                    AssetQaSeverity.Critical => EvidenceOutcome.Failed,
                    AssetQaSeverity.Warning => EvidenceOutcome.Warning,
                    _ => EvidenceOutcome.Informational
                },
                EvidenceConfidence.Verified,
                finding.ObservedAtUtc,
                SourceReference: result.RelativePath,
                ArtifactId: observation.ArtifactRelativePath,
                ContentHash: observation.ContentHash,
                PrivacyClass: EvidencePrivacyClass.ProjectRestricted,
                Metadata: JsonSerializer.SerializeToElement(new
                {
                    findingId = finding.Id,
                    sourceEvidenceId,
                    ruleId = result.RuleId,
                    severity = result.Severity.ToString(),
                    relativePath = result.RelativePath,
                    details = result.Details
                })), ct);
            await _graph.ProjectAssetFindingAsync(
                projectId, finding.Id, sourceEvidenceId, result.RuleId, result.Title, result.RelativePath,
                result.Severity == AssetQaSeverity.Critical, ct);
            created.Add(Map(finding));
        }
        return new(sourceEvidenceId, observation.Source, results.Count, created.Count, created);
    }

    public async Task<IReadOnlyList<AssetQaFindingDto>> ListAsync(Guid projectId, AssetQaFindingState? state, int take, CancellationToken ct)
        => (await _repository.ListFindingsAsync(projectId, state, Math.Clamp(take, 1, 1000), ct)).Select(Map).ToArray();

    private static string Fingerprint(Guid projectId, ProductionToolSource source, string ruleId, string? path)
    {
        var text = $"{projectId:N}|{source}|{ruleId}|{path?.Trim().ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static string NormalizeSubject(string? path)
    {
        var text = string.IsNullOrWhiteSpace(path) ? "project" : path.Trim().Replace('\\', '/');
        return text[..Math.Min(190, text.Length)];
    }

    private static AssetQaFindingDto Map(AssetQaFindingRecord x)
        => new(x.Id, x.ProjectId, x.EvidenceId, x.Source, x.RuleId, x.Severity, x.Title, x.Message, x.RelativePath, x.Fingerprint, x.State, x.ObservedAtUtc, x.ResolvedAtUtc);
}
