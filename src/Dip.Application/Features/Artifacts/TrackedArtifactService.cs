// Purpose: Orchestrates the DIP workflow for T ra ck ed Ar ti fa ct Se rv ic e.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Evidence;
using Dip.Application.Features.Policies;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Artifacts;

public sealed class TrackedArtifactService
{
    private readonly IProjectRepository _projects;
    private readonly ITrackedArtifactRepository _artifacts;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly EvidenceIngestionService _evidence;
    private readonly PolicyResolver _policies;

    public TrackedArtifactService(
        IProjectRepository projects,
        ITrackedArtifactRepository artifacts,
        IUnitOfWork uow,
        ISystemClock clock,
        EvidenceIngestionService evidence,
        PolicyResolver policies)
    {
        _projects = projects;
        _artifacts = artifacts;
        _uow = uow;
        _clock = clock;
        _evidence = evidence;
        _policies = policies;
    }

    public async Task<LocalFileBatchResult> IngestAsync(IngestLocalFileBatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ProjectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.AgentId)) throw new ArgumentException("AgentId is required.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.RootId)) throw new ArgumentException("RootId is required.", nameof(command));
        if (command.Observations.Count > 500) throw new ArgumentException("At most 500 observations are accepted per batch.", nameof(command));
        if (await _projects.GetByIdAsync(command.ProjectId, ct) is null)
            throw new KeyNotFoundException($"Project {command.ProjectId} was not found.");

        var effective = await _policies.ResolveAsync(command.ProjectId, command.AgentId.Trim(), ct);
        var results = new List<FileObservationResult>();
        var warnings = new List<string>();
        var duplicate = 0;
        var rejected = 0;

        foreach (var observation in command.Observations)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var normalized = NormalizeRelativePath(observation.RelativePath);
                if (IsExcluded(normalized, effective.Privacy.ExcludedPaths))
                {
                    rejected++;
                    warnings.Add($"Excluded by privacy policy: {normalized}");
                    continue;
                }

                var existingVersion = await _artifacts.FindVersionBySourceEventAsync(
                    command.ProjectId,
                    Required(observation.EventId, 300, nameof(observation.EventId)),
                    ct);

                if (existingVersion is not null)
                {
                    duplicate++;
                    var existingArtifact = await _artifacts.GetAsync(existingVersion.ArtifactId, ct)
                        ?? throw new InvalidOperationException("Existing artifact version references a missing artifact.");
                    var evidenceResult = await IngestEvidenceAsync(command, observation, existingArtifact, effective, ct);
                    results.Add(new FileObservationResult(
                        observation.EventId,
                        existingArtifact.Id,
                        existingVersion.Id,
                        existingVersion.VersionNumber,
                        false,
                        evidenceResult.Created,
                        existingArtifact.DisplayPath));
                    continue;
                }

                TrackedArtifact? artifact = null;
                if (observation.Kind == FileObservationKind.Renamed && !string.IsNullOrWhiteSpace(observation.PreviousRelativePath))
                {
                    var previous = NormalizeRelativePath(observation.PreviousRelativePath);
                    artifact = await _artifacts.FindByPathKeyAsync(command.ProjectId, PathKey(previous), ct);
                }
                artifact ??= await _artifacts.FindByPathKeyAsync(command.ProjectId, PathKey(normalized), ct);
                artifact ??= new TrackedArtifact(
                    Guid.NewGuid(),
                    command.ProjectId,
                    PathKey(normalized),
                    normalized,
                    NormalizeExtension(observation.Extension, normalized),
                    _clock.UtcNow);

                var isNewArtifact = artifact.CurrentVersionNumber == 0;
                if (isNewArtifact)
                    await _artifacts.AddArtifactAsync(artifact, ct);

                var versionNumber = observation.Kind == FileObservationKind.Deleted
                    ? artifact.RecordDeletion(_clock.UtcNow)
                    : artifact.RecordContent(
                        normalized,
                        PathKey(normalized),
                        NormalizeExtension(observation.Extension, normalized),
                        ValidateSha(observation.Sha256),
                        observation.SizeBytes ?? throw new ArgumentException("SizeBytes is required for non-deleted observations."),
                        observation.FileModifiedAtUtc,
                        _clock.UtcNow);

                var version = new TrackedArtifactVersion(
                    Guid.NewGuid(),
                    artifact.Id,
                    versionNumber,
                    observation.Kind,
                    observation.EventId,
                    command.AgentId,
                    command.RootId,
                    normalized,
                    observation.PreviousRelativePath is null ? null : NormalizeRelativePath(observation.PreviousRelativePath),
                    observation.Kind == FileObservationKind.Deleted ? null : observation.Sha256,
                    observation.Kind == FileObservationKind.Deleted ? null : observation.SizeBytes,
                    observation.FileModifiedAtUtc,
                    observation.ObservedAtUtc,
                    _clock.UtcNow);

                await _artifacts.AddVersionAsync(version, ct);
                await _uow.SaveChangesAsync(ct);

                var evidence = await IngestEvidenceAsync(command, observation, artifact, effective, ct);
                results.Add(new FileObservationResult(
                    observation.EventId,
                    artifact.Id,
                    version.Id,
                    version.VersionNumber,
                    true,
                    evidence.Created,
                    artifact.DisplayPath));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                rejected++;
                warnings.Add($"{observation.EventId}: {Safe(exception.Message)}");
            }
        }

        return new LocalFileBatchResult(
            command.Observations.Count,
            results.Count(item => item.Created),
            duplicate,
            rejected,
            results,
            warnings);
    }

    public async Task<IReadOnlyList<TrackedArtifactDto>> ListAsync(Guid projectId, int take, CancellationToken ct)
    {
        if (await _projects.GetByIdAsync(projectId, ct) is null)
            throw new KeyNotFoundException($"Project {projectId} was not found.");
        return (await _artifacts.ListByProjectAsync(projectId, Math.Clamp(take, 1, 1000), ct))
            .Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<TrackedArtifactVersionDto>> ListVersionsAsync(Guid projectId, Guid artifactId, int take, CancellationToken ct)
    {
        var artifact = await _artifacts.GetAsync(artifactId, ct);
        if (artifact is null || artifact.ProjectId != projectId)
            throw new KeyNotFoundException($"Artifact {artifactId} was not found in project {projectId}.");
        return (await _artifacts.ListVersionsAsync(artifactId, Math.Clamp(take, 1, 500), ct))
            .Select(Map).ToArray();
    }

    private async Task<IngestEvidenceResult> IngestEvidenceAsync(
        IngestLocalFileBatchCommand command,
        LocalFileObservation observation,
        TrackedArtifact artifact,
        EffectivePolicy effective,
        CancellationToken ct)
    {
        var metadata = effective.Evidence.CollectMetadata
            ? JsonSerializer.SerializeToElement(new
            {
                path = artifact.DisplayPath,
                previousPath = observation.PreviousRelativePath,
                extension = artifact.Extension,
                sizeBytes = observation.SizeBytes,
                agentId = command.AgentId,
                rootId = command.RootId,
                change = observation.Kind.ToString()
            })
            : (JsonElement?)null;

        return await _evidence.IngestAsync(
            new IngestEvidenceCommand(
                command.ProjectId,
                EvidenceSourceType.Collector,
                $"agent:{command.AgentId.Trim()}:root:{command.RootId.Trim()}",
                observation.EventId,
                EventType(observation.Kind),
                $"artifact:{artifact.Id:N}",
                "collector.file",
                $"{observation.Kind}: {artifact.DisplayPath}",
                Summary(observation, artifact.DisplayPath),
                EvidenceOutcome.Changed,
                EvidenceConfidence.High,
                observation.ObservedAtUtc,
                SourceReference: artifact.DisplayPath,
                ActorId: command.AgentId.Trim(),
                MachineId: command.AgentId.Trim(),
                ArtifactId: artifact.Id.ToString("N"),
                ContentHash: observation.Kind == FileObservationKind.Deleted ? null : observation.Sha256,
                PrivacyClass: EvidencePrivacyClass.ProjectRestricted,
                Metadata: metadata),
            ct);
    }

    private static EvidenceEventType EventType(FileObservationKind kind) => kind switch
    {
        FileObservationKind.Created => EvidenceEventType.FileCreated,
        FileObservationKind.Modified => EvidenceEventType.FileModified,
        FileObservationKind.Deleted => EvidenceEventType.FileDeleted,
        FileObservationKind.Renamed => EvidenceEventType.FileRenamed,
        _ => EvidenceEventType.Unknown
    };

    private static string Summary(LocalFileObservation item, string path) => item.Kind switch
    {
        FileObservationKind.Renamed => $"Local project artifact renamed from '{item.PreviousRelativePath}' to '{path}'.",
        FileObservationKind.Deleted => $"Local project artifact '{path}' was deleted.",
        _ => $"Local project artifact '{path}' was {item.Kind.ToString().ToLowerInvariant()} with SHA-256 {item.Sha256}."
    };

    private static TrackedArtifactDto Map(TrackedArtifact item) => new(
        item.Id, item.ProjectId, item.DisplayPath, item.Extension, item.LifecycleState,
        item.CurrentVersionNumber, item.CurrentSha256, item.CurrentSizeBytes,
        item.CurrentFileModifiedAtUtc, item.UpdatedAtUtc);

    private static TrackedArtifactVersionDto Map(TrackedArtifactVersion item) => new(
        item.Id, item.ArtifactId, item.VersionNumber, item.ChangeKind, item.DisplayPath,
        item.PreviousDisplayPath, item.Sha256, item.SizeBytes, item.FileModifiedAtUtc,
        item.ObservedAtUtc, item.SourceAgentId, item.SourceRootId);

    private static string NormalizeRelativePath(string value)
    {
        var path = Required(value, 1000, nameof(value)).Replace('\\', '/').Trim('/');
        if (Path.IsPathRooted(path) || path.Split('/').Any(part => part is "." or ".."))
            throw new ArgumentException("Artifact path must be a normalized project-relative path.", nameof(value));
        return path;
    }

    private static string PathKey(string normalized) => normalized.ToUpperInvariant();

    private static string NormalizeExtension(string? value, string path)
    {
        var ext = string.IsNullOrWhiteSpace(value) ? Path.GetExtension(path) : value;
        ext = (ext ?? string.Empty).Trim().ToLowerInvariant();
        if (ext.Length > 40) throw new ArgumentException("Extension is too long.");
        return ext;
    }

    private static string ValidateSha(string? value)
    {
        var sha = Required(value, 64, nameof(value)).ToLowerInvariant();
        if (sha.Length != 64 || sha.Any(ch => !Uri.IsHexDigit(ch)))
            throw new ArgumentException("Sha256 must be a 64-character hexadecimal value.", nameof(value));
        return sha;
    }

    private static bool IsExcluded(string relativePath, IReadOnlyList<string> excluded)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        return excluded.Any(raw =>
        {
            var rule = (raw ?? string.Empty).Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(rule) || Path.IsPathRooted(rule)) return false;
            return normalized.Equals(rule, StringComparison.OrdinalIgnoreCase)
                   || normalized.StartsWith(rule + "/", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string Required(string? value, int max, string name)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > max)
            throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return normalized;
    }

    private static string Safe(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized[..Math.Min(1000, normalized.Length)];
    }
}
