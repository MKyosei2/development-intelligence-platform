// Purpose: Coordinates Release Evidence application responsibilities for release.
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Provenance;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Jobs;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;
public sealed record ReleaseEvidenceJobRequest(Guid ProjectId, string SubjectKey, string CommitSha, string BuildId, string RepositoryRoot);
public sealed record ReleaseEvidenceBundleDto(Guid Id, string CommitSha, string BuildId, string Status, string ManifestArtifactId, string? SbomArtifactId, string? BundleArtifactId, string ArtifactSha256, DateTimeOffset CreatedAtUtc);

public sealed class ReleaseEvidenceService
{
    private readonly DurableJobService _jobs;
     private readonly IReleaseProvenanceCollector _collector;
     private readonly ArtifactService _artifacts;
     private readonly IArtifactRepository _artifactRepo;
     private readonly IReleaseEvidenceRepository _repo;
     private readonly IEvidenceRepository _evidence;
     private readonly IUnitOfWork _uow;
     private readonly ISystemClock _clock;
    public ReleaseEvidenceService(DurableJobService jobs, IReleaseProvenanceCollector collector, ArtifactService artifacts, IArtifactRepository artifactRepo, IReleaseEvidenceRepository repo, IEvidenceRepository evidence, IUnitOfWork uow, ISystemClock clock) { _jobs = jobs;
     _collector = collector;
     _artifacts = artifacts;
     _artifactRepo = artifactRepo;
     _repo = repo;
     _evidence = evidence;
     _uow = uow;
     _clock = clock;
     }
    public Task<EnqueuedJobDto> EnqueueAsync(ReleaseEvidenceJobRequest request, CancellationToken ct) => _jobs.EnqueueAsync(request.ProjectId, DurableJobType.ReleaseEvidence, request.SubjectKey, request, 2, ct);
    public async Task<ReleaseEvidenceBundleDto> ExecuteJobAsync(DurableJob job, ReleaseEvidenceJobRequest request, CancellationToken ct)
    {
        var snapshot = await _collector.CollectAsync(request.RepositoryRoot, ct);
         var sbom = BuildCycloneDx(request, snapshot);
         var manifest = new { schemaVersion = "1.0", request.ProjectId, request.CommitSha, request.BuildId, generatedAtUtc = _clock.UtcNow, snapshot.Runtime, snapshot.OperatingSystem, files = snapshot.Files, componentCount = snapshot.Components.Count };
         var links = new ArtifactLinkContext(job.Id);
        var manifestRecord = await _artifacts.StoreJsonAsync(request.ProjectId, "release-manifest", "manifest.json", manifest, links, "release", ct); var sbomRecord = await _artifacts.StoreJsonAsync(request.ProjectId, "sbom", "sbom.cdx.json", sbom, links, "release", ct);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
         var sbomBytes = JsonSerializer.SerializeToUtf8Bytes(sbom, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
         byte[] bundleBytes;
         await using (var ms = new MemoryStream()) { using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) { WriteEntry(zip, "manifest.json", manifestBytes);
         WriteEntry(zip, "sbom.cdx.json", sbomBytes);
         } bundleBytes = ms.ToArray();
         }
        await using var bundleStream = new MemoryStream(bundleBytes, false);
         var bundleRecord = await _artifacts.StoreAsync(request.ProjectId, "release-evidence-bundle", $"release-evidence-{request.CommitSha[..Math.Min(12, request.CommitSha.Length)]}.zip", bundleStream, "application/zip", links, "release", TimeSpan.FromDays(3650), ct);
         var hash = Convert.ToHexString(SHA256.HashData(bundleBytes)).ToLowerInvariant();
         var entity = new ReleaseEvidenceBundle(Guid.NewGuid(), request.ProjectId, request.CommitSha, request.BuildId, ReleaseEvidenceBundleStatus.Succeeded, manifestRecord.Id.ToString("N"), sbomRecord.Id.ToString("N"), bundleRecord.Id.ToString("N"), hash, _clock.UtcNow);
         await _repo.AddAsync(entity, ct);
         await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.Build, "release-provenance", EvidenceOutcome.Succeeded, $"Release evidence bundle {request.BuildId}", $"Release provenance and CycloneDX SBOM were captured for {request.CommitSha}.", EvidenceConfidence.High, bundleRecord.Id.ToString(), JsonSerializer.Serialize(new { releaseEvidenceId = entity.Id, hash, manifestArtifactId = manifestRecord.Id, sbomArtifactId = sbomRecord.Id, bundleArtifactId = bundleRecord.Id }), _clock.UtcNow, _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
         return Map(entity);
    }
    public async Task<ReleaseEvidenceBundleDto?> GetLatestAsync(Guid projectId, CancellationToken ct) { var e = await _repo.GetLatestAsync(projectId, ct); return e is null ? null : Map(e); }
    private static object BuildCycloneDx(ReleaseEvidenceJobRequest request, ProvenanceSnapshot snapshot) => new { bomFormat = "CycloneDX", specVersion = "1.5", serialNumber = $"urn:uuid:{Guid.NewGuid()}", version = 1, metadata = new { timestamp = DateTimeOffset.UtcNow, component = new { type = "application", name = "Development Intelligence Platform target", version = request.CommitSha } }, components = snapshot.Components.Select(x => new { type = x.Type, name = x.Name, version = x.Version, properties = new[] { new { name = "dip:path", value = x.Path ?? string.Empty }, new { name = "dip:sha256", value = x.Sha256 ?? string.Empty } } }).ToArray() };
    private static void WriteEntry(ZipArchive zip, string name, byte[] bytes) { var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
     using var stream = entry.Open();
     stream.Write(bytes, 0, bytes.Length);
     }
    private static ReleaseEvidenceBundleDto Map(ReleaseEvidenceBundle x) => new(x.Id, x.CommitSha, x.BuildId, x.Status.ToString(), x.ManifestArtifactId, x.SbomArtifactId, x.BundleArtifactId, x.ArtifactSha256, x.CreatedAtUtc);
}
