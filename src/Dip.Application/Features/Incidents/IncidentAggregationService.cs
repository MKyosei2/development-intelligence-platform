// Purpose: Coordinates Incident Aggregation Service application behavior for Incidents.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Incidents;

public sealed record IncidentSummaryDto(Guid Id, string Fingerprint, string SubjectKey, string Title, string Severity, string Status, int Occurrences, int Reproduced, DateTimeOffset FirstSeenAtUtc, DateTimeOffset LastSeenAtUtc, string? SuspectedRegressionCommit, string? FixedCommit, string? RootCauseSummary);
public sealed record IncidentTimelineEventDto(Guid Id, string Type, string Summary, string? PayloadJson, DateTimeOffset OccurredAtUtc);
public sealed record IncidentTimelineDto(IncidentSummaryDto Incident, IReadOnlyList<IncidentTimelineEventDto> Events);

public sealed class IncidentAggregationService
{
    private readonly IIncidentRepository _repo; private readonly IArtifactRepository _artifacts; private readonly IUnitOfWork _uow; private readonly ISystemClock _clock;
    public IncidentAggregationService(IIncidentRepository repo, IArtifactRepository artifacts, IUnitOfWork uow, ISystemClock clock) { _repo = repo;
     _artifacts = artifacts;
     _uow = uow;
     _clock = clock;
     }

    public async Task<Incident> ObserveQaFindingAsync(QaFinding finding, string? commitSha, CancellationToken ct)
    {
        var detail = JsonSerializer.Serialize(new { finding.Id, finding.QaRunId, finding.Category, finding.Tool, finding.FilePath, finding.Line, finding.Function, finding.ExceptionType, finding.ReproductionStatus });
        return await ObserveAsync(finding.ProjectId, finding.SubjectKey, finding.Fingerprint, finding.Title, finding.Severity, finding.ObservedAtUtc, finding.Id, finding.QaRunId, null, commitSha, detail, finding.ReproductionStatus == ReproductionStatus.Reproduced, ct);
    }

    public async Task<Incident> ObserveSyntheticAsync(Guid projectId, string subjectKey, string fingerprint, string title, Severity severity, string detailJson, Guid? jobId, CancellationToken ct)
        => await ObserveAsync(projectId, subjectKey, fingerprint, title, severity, _clock.UtcNow, null, null, jobId, null, detailJson, false, ct);

    public async Task AttachReproductionAsync(Guid projectId, string fingerprint, string detail, CancellationToken ct)
    {
        var incident = await _repo.FindByFingerprintAsync(projectId, fingerprint, ct);
        if (incident is null) return;
         incident.MarkReproduced();
         await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, IncidentEventType.Reproduced, detail, null, _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
    }
    public async Task AttachRegressionAsync(Guid projectId, string subjectKey, string commit, string? sourceReference, CancellationToken ct)
    {
        var incident = await _repo.FindLatestOpenForSubjectAsync(projectId, subjectKey, ct);
        if (incident is null) return;
         incident.SetRegressionCommit(commit);
         await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, IncidentEventType.RegressionLocated, $"Regression origin isolated to {commit}.", JsonSerializer.Serialize(new { commit, sourceReference }), _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
    }
    public async Task AttachSymbolicationAsync(Guid projectId, string subjectKey, string status, string summary, string? artifactReference, object frames, CancellationToken ct)
    {
        var incident = await _repo.FindLatestOpenForSubjectAsync(projectId, subjectKey, ct);
        if (incident is null) return;
         await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, IncidentEventType.Symbolicated, summary, JsonSerializer.Serialize(new { status, artifactReference, frames }), _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
    }
    public async Task AttachRootCauseAsync(Guid projectId, string subjectKey, string summary, object result, CancellationToken ct)
    {
        var incident = await _repo.FindLatestOpenForSubjectAsync(projectId, subjectKey, ct);
        if (incident is null) return;
         incident.SetRootCause(summary);
         await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, IncidentEventType.RootCauseUpdated, summary, JsonSerializer.Serialize(result), _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
    }
    public async Task AttachArtifactAsync(Guid incidentId, Guid artifactRecordId, string fileName, CancellationToken ct)
    {
        var incident = await _repo.GetAsync(incidentId, ct);
        if (incident is null) return;
         var artifact = await _artifacts.GetRecordForUpdateAsync(artifactRecordId, ct);
        if (artifact is not null) artifact.LinkIncident(incidentId);
         await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, IncidentEventType.ArtifactAttached, $"Artifact attached: {fileName}", JsonSerializer.Serialize(new { artifactRecordId, fileName }), _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
    }
    public async Task<Incident> ObserveBenchmarkRegressionAsync(Guid projectId, string subjectKey, string fingerprint, string title, Severity severity, Guid jobId, Guid benchmarkRunId, object comparison, CancellationToken ct)
    {
        var incident = await ObserveSyntheticAsync(projectId, subjectKey, fingerprint, title, severity, JsonSerializer.Serialize(new { benchmarkRunId, comparison }), jobId, ct);
        await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, IncidentEventType.BenchmarkRegression, title, JsonSerializer.Serialize(new { benchmarkRunId, comparison }), _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
         return incident;
    }
    public async Task AttachReplayRegressionAsync(Guid incidentId, string summary, object detail, CancellationToken ct)
    {
        var incident = await _repo.GetAsync(incidentId, ct);
        if (incident is null) return;
         await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, IncidentEventType.ReplayRegression, summary, JsonSerializer.Serialize(detail), _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
    }
    public async Task<bool> ResolveAsync(Guid incidentId, string? fixedCommit, CancellationToken ct)
    {
        var incident = await _repo.GetAsync(incidentId, ct);
        if (incident is null) return false;
         incident.Resolve(fixedCommit, _clock.UtcNow);
         await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, IncidentEventType.Resolved, fixedCommit is null ? "Incident marked resolved." : $"Incident resolved by {fixedCommit}.", JsonSerializer.Serialize(new { fixedCommit }), _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
         return true;
    }
    public async Task<IReadOnlyList<IncidentSummaryDto>> ListAsync(Guid projectId, int take, CancellationToken ct) => (await _repo.ListRecentAsync(projectId, take, ct)).Select(Map).ToArray();
    public async Task<IncidentTimelineDto?> GetTimelineAsync(Guid incidentId, CancellationToken ct) { var incident = await _repo.GetAsync(incidentId, ct);
    if (incident is null) return null;
     var events = await _repo.ListEventsAsync(incidentId, ct);
     return new(Map(incident), events.Select(x => new IncidentTimelineEventDto(x.Id, x.Type.ToString(), x.Summary, x.PayloadJson, x.OccurredAtUtc)).ToArray());
     }

    private async Task<Incident> ObserveAsync(Guid projectId, string subjectKey, string fingerprint, string title, Severity severity, DateTimeOffset observedAt, Guid? findingId, Guid? qaRunId, Guid? jobId, string? commitSha, string detailJson, bool reproduced, CancellationToken ct)
    {
        fingerprint = NormalizeFingerprint(fingerprint);
         var incident = await _repo.FindByFingerprintAsync(projectId, fingerprint, ct);
         var wasResolved = incident?.Status == IncidentStatus.Resolved;
        if (incident is null) { incident = new Incident(Guid.NewGuid(), projectId, fingerprint, subjectKey, title, severity, observedAt); await _repo.AddAsync(incident, ct); }
        incident.Observe(severity, observedAt, reproduced);
        await _repo.AddOccurrenceAsync(new IncidentOccurrence(Guid.NewGuid(), incident.Id, projectId, subjectKey, observedAt, findingId, qaRunId, jobId, commitSha, detailJson), ct);
        await _repo.AddEventAsync(new IncidentEvent(Guid.NewGuid(), incident.Id, wasResolved ? IncidentEventType.Reopened : IncidentEventType.Observed, wasResolved ? "Incident reopened by a new matching occurrence." : "Matching incident occurrence observed.", detailJson, observedAt), ct);
        await _uow.SaveChangesAsync(ct); return incident;
    }
    private static string NormalizeFingerprint(string fingerprint)
    {
        var value = fingerprint?.Trim() ?? string.Empty;
        if (value.Length < 1) throw new ArgumentException("Incident fingerprint is required.", nameof(fingerprint));
        if (value.Length <= 128) return value;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant(); return $"sha256:{hash}";
    }
    private static IncidentSummaryDto Map(Incident x) => new(x.Id, x.Fingerprint, x.SubjectKey, x.Title, x.Severity.ToString(), x.Status.ToString(), x.OccurrenceCount, x.ReproducedCount, x.FirstSeenAtUtc, x.LastSeenAtUtc, x.SuspectedRegressionCommit, x.FixedCommit, x.RootCauseSummary);
}
