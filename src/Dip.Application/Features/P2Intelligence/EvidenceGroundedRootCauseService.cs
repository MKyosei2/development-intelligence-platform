// Purpose: Builds bounded root-cause candidates from project truth, incident, and change evidence.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

/// <summary>
/// Evidence-grounded root-cause ranking implemented entirely inside DIP.
/// No external model/API is consulted. Results are deterministic for the same
/// evidence set and remain hypotheses until independently reproduced/verified.
/// </summary>
public sealed class EvidenceGroundedRootCauseService
{
    private readonly IEvidenceRepository _evidence;
    private readonly IP2IntelligenceRepository _intelligence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly IncidentAggregationService _incidents;

    public EvidenceGroundedRootCauseService(
        IEvidenceRepository evidence,
        IP2IntelligenceRepository intelligence,
        IUnitOfWork uow,
        ISystemClock clock,
        IncidentAggregationService incidents)
    {
        _evidence = evidence;
        _intelligence = intelligence;
        _uow = uow;
        _clock = clock;
        _incidents = incidents;
    }

    public async Task<RootCauseResult> AnalyzeAsync(RootCauseCommand command, CancellationToken ct)
    {
        var all = await _evidence.ListForProjectAsync(command.ProjectId, _clock.UtcNow.AddDays(-90), ct);
        var selected = all
            .Where(x => string.Equals(x.SubjectKey, command.SubjectKey, StringComparison.OrdinalIgnoreCase)
                || x.Outcome is EvidenceOutcome.Failed or EvidenceOutcome.Warning)
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(Math.Clamp(command.EvidenceLimit, 5, 100))
            .ToArray();

        if (selected.Length == 0)
        {
            var empty = new RootCauseResult(
                "DeterministicEvidenceRanking",
                "InsufficientEvidence",
                [],
                ["Collect runtime/build/test evidence for the affected subject."],
                [],
                "There is not enough objective evidence for root-cause analysis.");
            await StoreAsync(command, empty, ct);
            return empty;
        }

        var result = Deterministic(selected);
        await StoreAsync(command, result, ct);
        if (result.Candidates.Count > 0)
            await _incidents.AttachRootCauseAsync(command.ProjectId, command.SubjectKey, result.Summary, result, ct);
        return result;
    }

    public async Task<IReadOnlyList<SimilarIncidentDto>> FindSimilarIncidentsAsync(
        Guid projectId, string subjectKey, string query, int take, CancellationToken ct)
    {
        var records = await _intelligence.ListRecentAsync(projectId, 200, ct);
        var queryTokens = Tokens(subjectKey + " " + query);
        return records
            .Where(x => x.Kind is P2IntelligenceKind.RootCause or P2IntelligenceKind.RegressionOrigin
                or P2IntelligenceKind.FlakyTest or P2IntelligenceKind.SoakTest or P2IntelligenceKind.PerformanceDrift)
            .Select(x => new
            {
                Record = x,
                Similarity = Similarity(queryTokens, Tokens(x.SubjectKey + " " + x.Summary + " " + x.Kind))
            })
            .Where(x => x.Similarity >= .15m)
            .OrderByDescending(x => x.Similarity)
            .Take(Math.Clamp(take, 1, 20))
            .Select(x => new SimilarIncidentDto(
                x.Record.Id,
                x.Record.SubjectKey,
                x.Record.Kind.ToString(),
                x.Record.Summary,
                decimal.Round(x.Similarity * 100m, 2),
                x.Record.CreatedAtUtc))
            .ToArray();
    }

    private async Task StoreAsync(RootCauseCommand command, RootCauseResult result, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(result);
        await _intelligence.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),
            command.ProjectId,
            command.SubjectKey,
            P2IntelligenceKind.RootCause,
            P2IntelligenceStatus.Informational,
            result.Candidates.Count == 0 ? null : result.Candidates.Max(x => x.ConfidencePercent),
            EvidenceConfidence.Medium,
            result.Summary,
            payload,
            null,
            _clock.UtcNow), ct);

        if (result.SuggestedActions.Count > 0)
        {
            await _intelligence.AddAsync(new P2IntelligenceRecord(
                Guid.NewGuid(),
                command.ProjectId,
                command.SubjectKey,
                P2IntelligenceKind.SuggestedFix,
                P2IntelligenceStatus.Informational,
                null,
                EvidenceConfidence.Low,
                "Deterministic evidence-grounded suggested actions are available for human review.",
                JsonSerializer.Serialize(new { actions = result.SuggestedActions, automaticChangeApplied = false }),
                null,
                _clock.UtcNow), ct);
        }

        await _evidence.AddAsync(new EvidenceRecord(
            Guid.NewGuid(),
            command.ProjectId,
            null,
            command.SubjectKey,
            EvidenceSourceType.Analysis,
            "root-cause.analysis",
            EvidenceOutcome.Informational,
            "Root-cause candidates updated",
            result.Summary,
            EvidenceConfidence.Medium,
            null,
            payload,
            _clock.UtcNow,
            _clock.UtcNow), ct);

        await _uow.SaveChangesAsync(ct);
    }

    private static RootCauseResult Deterministic(IReadOnlyCollection<EvidenceRecord> evidence)
    {
        var failures = evidence
            .Where(x => x.Outcome is EvidenceOutcome.Failed or EvidenceOutcome.Warning)
            .GroupBy(x => new { x.EvidenceKind, x.SourceReference })
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Max(x => x.ObservedAtUtc))
            .Take(3)
            .ToArray();

        var candidates = failures.Select(g =>
        {
            var ids = g.Select(x => x.Id).Take(10).ToArray();
            var representative = g.OrderByDescending(x => x.ObservedAtUtc).First();
            var baseConfidence = representative.Confidence switch
            {
                EvidenceConfidence.Verified => 82m,
                EvidenceConfidence.High => 72m,
                EvidenceConfidence.Medium => 55m,
                _ => 35m
            };
            var confidence = Math.Min(92m, baseConfidence + (g.Count() - 1) * 4m);
            var location = string.IsNullOrWhiteSpace(g.Key.SourceReference)
                ? "the affected subsystem"
                : g.Key.SourceReference;
            return new RootCauseCandidate(
                $"{g.Key.EvidenceKind} near {location}",
                confidence,
                $"{g.Count()} related warning/failure evidence item(s) point to this area. This is a candidate, not a proven cause.",
                ids);
        }).ToArray();

        var actions = candidates
            .Select(c => $"Review {c.Title} and reproduce it against the linked evidence before changing code.")
            .Take(3)
            .ToArray();
        var summary = candidates.Length == 0
            ? "Evidence exists, but no warning/failure cluster is strong enough to rank a root-cause candidate."
            : $"{candidates.Length} deterministic evidence-grounded root-cause candidate(s) were ranked. No code change was applied automatically.";

        return new(
            "DeterministicEvidenceRanking",
            candidates.Length == 0 ? "InsufficientEvidence" : "CandidatesAvailable",
            candidates,
            actions,
            candidates.SelectMany(x => x.EvidenceIds).Distinct().ToArray(),
            summary);
    }

    private static HashSet<string> Tokens(string text) => text
        .ToLowerInvariant()
        .Split([' ', '/', '\\', '.', ':', ';', '-', '_', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
        .Where(x => x.Length >= 3)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static decimal Similarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0m;
        var intersection = a.Count(x => b.Contains(x));
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0m : (decimal)intersection / union;
    }
}
