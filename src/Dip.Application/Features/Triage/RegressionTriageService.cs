// Purpose: Triages regressions by correlating incident, reproduction, risk, and evidence signals.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;

namespace Dip.Application.Features.Triage;

public sealed class RegressionTriageService
{
    private readonly IIncidentRepository _incidents;
    private readonly IRegressionTriageRepository _repo;
    private readonly IEvidenceRepository _evidence;
    private readonly IChangeRiskRepository _risk;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public RegressionTriageService(
        IIncidentRepository incidents,
        IRegressionTriageRepository repo,
        IEvidenceRepository evidence,
        IChangeRiskRepository risk,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _incidents = incidents;
        _repo = repo;
        _evidence = evidence;
        _risk = risk;
        _uow = uow;
        _clock = clock;
    }

    public async Task<RegressionTriageDto> TriageAsync(
        RegressionTriageRequest request,
        CancellationToken ct)
    {
        var incident = await _incidents.GetAsync(request.IncidentId, ct)
                       ?? throw new KeyNotFoundException(
                           $"Incident {request.IncidentId} was not found.");

        if (incident.ProjectId != request.ProjectId)
            throw new InvalidOperationException(
                "Incident does not belong to the requested project.");

        var events = await _incidents.ListEventsAsync(incident.Id, ct);
        var occurrences = await _incidents.ListOccurrencesAsync(incident.Id, 50, ct);
        var evidence = (await _evidence.ListForProjectAsync(
                request.ProjectId,
                _clock.UtcNow.AddDays(-90),
                ct))
            .Where(x => x.SubjectKey == incident.SubjectKey)
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(20)
            .ToArray();

        var risk = await _risk.GetLatestAsync(
            request.ProjectId,
            incident.SuspectedRegressionCommit,
            ct);

        decimal confidence = 25m;
        var reasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(incident.SuspectedRegressionCommit))
        {
            confidence += 25m;
            reasons.Add("Regression-origin evidence identifies a suspected commit.");
        }

        if (incident.ReproducedCount > 0)
        {
            confidence += 20m;
            reasons.Add($"Incident reproduced {incident.ReproducedCount} time(s).");
        }

        if (!string.IsNullOrWhiteSpace(incident.RootCauseSummary))
        {
            confidence += 15m;
            reasons.Add("Evidence-grounded root-cause context exists.");
        }

        if (events.Any(x =>
                x.Type == Dip.Domain.Enums.IncidentEventType.Symbolicated))
        {
            confidence += 10m;
            reasons.Add("A symbolicated stack is attached.");
        }

        if (risk is not null)
        {
            confidence += Math.Min(5m, risk.RiskScore / 20m);
            reasons.Add(
                $"Latest deterministic change-risk score is {risk.RiskScore:0.#}/100.");
        }

        confidence = Math.Clamp(confidence, 0m, 100m);

        var supplied = (request.SuspectedFiles ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var observed = occurrences.SelectMany(ExtractFiles);
        var files = supplied
            .Concat(observed)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        var subsystem = string.IsNullOrWhiteSpace(request.SuspectedSubsystem)
            ? InferSubsystem(files, incident.SubjectKey)
            : request.SuspectedSubsystem;

        var explanation = reasons.Count == 0
            ? "Insufficient evidence exists to rank a regression cause with confidence."
            : string.Join(" ", reasons);

        var entity = new RegressionTriage(
            Guid.NewGuid(),
            request.ProjectId,
            request.IncidentId,
            incident.SuspectedRegressionCommit,
            subsystem,
            JsonSerializer.Serialize(files),
            request.SuggestedOwner,
            confidence,
            JsonSerializer.Serialize(evidence.Select(x => x.Id)),
            explanation,
            _clock.UtcNow);

        await _repo.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<RegressionTriageDto?> GetLatestAsync(
        Guid incidentId,
        CancellationToken ct)
    {
        var item = await _repo.GetLatestForIncidentAsync(incidentId, ct);
        return item is null ? null : Map(item);
    }

    private static IEnumerable<string> ExtractFiles(IncidentOccurrence occurrence)
    {
        if (string.IsNullOrWhiteSpace(occurrence.DetailJson))
            yield break;

        using var document = JsonDocument.Parse(occurrence.DetailJson);
        var root = document.RootElement;

        if (root.TryGetProperty("FilePath", out var path)
            || root.TryGetProperty("filePath", out path))
        {
            var value = path.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static string InferSubsystem(
        IReadOnlyList<string> files,
        string subjectKey)
    {
        var text = (string.Join("|", files) + "|" + subjectKey).ToLowerInvariant();

        if (text.Contains("render") || text.Contains("shader") || text.Contains("gpu"))
            return "Rendering";
        if (text.Contains("network") || text.Contains("server") || text.Contains("socket"))
            return "Networking";
        if (text.Contains("audio") || text.Contains("sound"))
            return "Audio";
        if (text.Contains("ui") || text.Contains("hud"))
            return "UI";
        if (text.Contains("asset") || text.Contains("content"))
            return "Assets/Content";
        if (text.Contains("database") || text.Contains("migration") || text.Contains("persistence"))
            return "Persistence";

        return "Unclassified";
    }

    private static RegressionTriageDto Map(RegressionTriage item) =>
        new(
            item.Id,
            item.IncidentId,
            item.SuspectedCommit,
            item.SuspectedSubsystem,
            JsonSerializer.Deserialize<string[]>(item.SuspectedFilesJson) ?? [],
            item.SuggestedOwner,
            item.Confidence,
            JsonSerializer.Deserialize<Guid[]>(item.EvidenceIdsJson) ?? [],
            item.Explanation,
            item.CreatedAtUtc);
}
