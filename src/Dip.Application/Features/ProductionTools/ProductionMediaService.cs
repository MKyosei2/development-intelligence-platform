// Purpose: Orchestrates the DIP workflow for P ro du ct io nM ed ia Se rv ic e.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Policies;
using Dip.Application.Features.Evidence;
using Dip.Application.Features.QualityAutomation.VisualQa;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProductionTools;

public sealed class ProductionMediaService
{
    private readonly IProjectRepository _projects;
    private readonly ITrackedArtifactRepository _tracked;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ArtifactService _artifacts;
    private readonly IUnitOfWork _uow;
    private readonly PolicyResolver _policies;
    private readonly EvidenceIngestionService _evidence;
    private readonly VisualQaService _visualQa;

    public ProductionMediaService(
        IProjectRepository projects,
        ITrackedArtifactRepository tracked,
        IArtifactRepository artifactRepository,
        ArtifactService artifacts,
        IUnitOfWork uow,
        PolicyResolver policies,
        EvidenceIngestionService evidence,
        VisualQaService visualQa)
    {
        _projects = projects;
        _tracked = tracked;
        _artifactRepository = artifactRepository;
        _artifacts = artifacts;
        _uow = uow;
        _policies = policies;
        _evidence = evidence;
        _visualQa = visualQa;
    }

    public async Task<StoredProductionMediaDto> StoreAsync(
        Guid projectId,
        string agentId,
        string eventId,
        ProductionToolSource source,
        ProductionMediaKind kind,
        string relativePath,
        Stream content,
        string contentType,
        long sizeBytes,
        string? expectedSha256,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("AgentId is required.", nameof(agentId));
        if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("EventId is required.", nameof(eventId));
        if (!Enum.IsDefined(source) || source == ProductionToolSource.Unknown) throw new ArgumentOutOfRangeException(nameof(source));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (await _projects.GetByIdAsync(projectId, ct) is null)
            throw new KeyNotFoundException($"Project {projectId} was not found.");

        var path = ProductionToolEventService.NormalizeOptionalPath(relativePath)
                   ?? throw new ArgumentException("RelativePath is required.", nameof(relativePath));
        var effective = await _policies.ResolveAsync(projectId, agentId.Trim(), ct);
        var sourceAllowed = source switch
        {
            ProductionToolSource.Maya => effective.Agent.MayaMonitoringEnabled,
            ProductionToolSource.Unity => effective.Agent.UnityMonitoringEnabled,
            ProductionToolSource.Unreal => effective.Agent.UnrealMonitoringEnabled,
            _ => false
        };
        if (!sourceAllowed)
            throw new InvalidOperationException($"{source} monitoring is disabled by effective policy.");
        if ((kind is ProductionMediaKind.Screenshot or ProductionMediaKind.Render) && !effective.Privacy.AllowScreenshots)
            throw new InvalidOperationException("Screenshot upload is disabled by effective privacy policy.");
        if ((kind is ProductionMediaKind.Video or ProductionMediaKind.Playblast) && !effective.Privacy.AllowVideo)
            throw new InvalidOperationException("Video upload is disabled by effective privacy policy.");
        if (!effective.Evidence.AllowRawArtifactUpload)
            throw new InvalidOperationException("Raw artifact upload is disabled by effective evidence policy.");

        var maximum = kind == ProductionMediaKind.Screenshot ? 64L * 1024 * 1024 : 1024L * 1024 * 1024;
        if (sizeBytes > maximum)
            throw new ArgumentException($"Media exceeds the Phase-3 size limit of {maximum} bytes.", nameof(sizeBytes));

        var temp = Path.Combine(Path.GetTempPath(), "dip-production-media-" + Guid.NewGuid().ToString("N") + Path.GetExtension(path));
        string actualSha;
        long actualSize;
        try
        {
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true))
                await content.CopyToAsync(output, ct);

            actualSize = new FileInfo(temp).Length;
            if (actualSize != sizeBytes)
                throw new InvalidDataException($"Uploaded media length mismatch. Expected {sizeBytes}, actual {actualSize}.");

            await using (var hashStream = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true))
            using (var sha = System.Security.Cryptography.SHA256.Create())
                actualSha = Convert.ToHexString(await sha.ComputeHashAsync(hashStream, ct)).ToLowerInvariant();

            var expected = ProductionToolEventService.NormalizeHash(expectedSha256);
            if (expected is not null && !actualSha.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Uploaded media SHA-256 does not match the agent-provided hash.");

            await using var storedStream = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
            var record = await _artifacts.StoreAsync(
                projectId,
                kind switch
                {
                    ProductionMediaKind.Screenshot => "production.screenshot",
                    ProductionMediaKind.Render => "production.render",
                    ProductionMediaKind.Playblast => "production.playblast",
                    _ => "production.video"
                },
                Path.GetFileName(path),
                storedStream,
                NormalizeContentType(contentType, path),
                new ArtifactLinkContext(),
                retentionClass: kind == ProductionMediaKind.Screenshot ? "production-image" : "production-video",
                retention: TimeSpan.FromDays(kind == ProductionMediaKind.Screenshot
                    ? effective.Retention.ArtifactRetentionDays
                    : effective.Retention.VideoRetentionDays),
                ct: ct);

            var blob = await _artifactRepository.GetBlobAsync(record.ArtifactBlobId, ct)
                       ?? throw new InvalidOperationException("Stored media blob could not be resolved.");

            var tracked = await _tracked.FindByPathKeyAsync(projectId, path.ToUpperInvariant(), ct);
            ArtifactPreview? preview = null;
            TrackedArtifactVersion? version = null;
            if (tracked is not null)
            {
                version = await _tracked.GetLatestVersionAsync(tracked.Id, ct);
                if (version is not null)
                {
                    preview = new ArtifactPreview(
                        Guid.NewGuid(),
                        version.Id,
                        record.Id,
                        kind switch
                        {
                            ProductionMediaKind.Screenshot => ArtifactPreviewKind.Screenshot,
                            ProductionMediaKind.Render => ArtifactPreviewKind.Render,
                            _ => ArtifactPreviewKind.Video
                        },
                        DateTimeOffset.UtcNow);
                    await _tracked.AddPreviewAsync(preview, ct);
                    await _uow.SaveChangesAsync(ct);
                }
            }

            var mediaEventType = kind switch
            {
                ProductionMediaKind.Screenshot => EvidenceEventType.ScreenshotGenerated,
                ProductionMediaKind.Video => EvidenceEventType.VideoGenerated,
                ProductionMediaKind.Render => EvidenceEventType.RenderCompleted,
                ProductionMediaKind.Playblast => EvidenceEventType.MayaPlayblast,
                _ => EvidenceEventType.ToolEvent
            };
            var metadata = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                source = source.ToString(),
                eventKind = mediaEventType.ToString(),
                mediaKind = kind.ToString(),
                relativePath = path,
                artifactRecordId = record.Id,
                artifactPreviewId = preview?.Id,
                trackedArtifactId = tracked?.Id,
                trackedArtifactVersionId = version?.Id,
                contentType = blob.ContentType,
                sizeBytes = blob.SizeBytes
            });
            await _evidence.IngestAsync(
                new IngestEvidenceCommand(
                    projectId,
                    ProductionToolEventMapping.SourceType(source),
                    $"agent:{agentId.Trim()}:media:{source.ToString().ToLowerInvariant()}",
                    eventId.Trim() + ":media",
                    mediaEventType,
                    $"media:{path}"[..Math.Min(200, $"media:{path}".Length)],
                    $"production.media.{kind.ToString().ToLowerInvariant()}",
                    $"{source}: {kind} stored",
                    $"Stored policy-approved {kind.ToString().ToLowerInvariant()} '{path}' as DIP artifact {record.Id}.",
                    EvidenceOutcome.Succeeded,
                    EvidenceConfidence.Verified,
                    DateTimeOffset.UtcNow,
                    SourceReference: $"/api/v1/artifacts/{record.Id}",
                    MachineId: agentId.Trim(),
                    ArtifactId: record.Id.ToString("N"),
                    ContentHash: blob.Sha256,
                    PrivacyClass: EvidencePrivacyClass.ProjectRestricted,
                    Metadata: metadata),
                ct);

            VisualComparisonDto? visual = null;
            if (kind is ProductionMediaKind.Screenshot or ProductionMediaKind.Render)
            {
                try { visual = await _visualQa.TryAutoCompareAsync(projectId, path, record.Id, ct); }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Media evidence remains durable even if an optional visual comparison cannot run.
                    // The manual Visual QA endpoint can retry after configuration is corrected.
                    System.Diagnostics.Debug.WriteLine($"Phase-4 visual auto-compare skipped: {exception.Message}");
                }
            }

            return new StoredProductionMediaDto(
                record.Id,
                preview?.Id,
                tracked?.Id,
                version?.Id,
                blob.Sha256,
                blob.SizeBytes,
                blob.ContentType,
                kind,
                path,
                visual?.Id,
                visual?.Verdict.ToString());
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static string NormalizeContentType(string? value, string path)
    {
        var contentType = string.IsNullOrWhiteSpace(value)
            ? Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            }
            : value.Trim().ToLowerInvariant();

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "image/webp",
            "video/mp4", "video/quicktime", "video/webm"
        };
        if (!allowed.Contains(contentType))
            throw new ArgumentException($"Unsupported production media content type '{contentType}'.");
        return contentType;
    }
}
