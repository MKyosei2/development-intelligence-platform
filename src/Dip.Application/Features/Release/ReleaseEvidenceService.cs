// Purpose: Builds exact-revision release evidence with CycloneDX SBOM, provenance attestation, detached bundle payload signatures, and durable hashing.
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
    private static readonly JsonSerializerOptions ArtifactJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly DurableJobService _jobs;
    private readonly IReleaseProvenanceCollector _collector;
    private readonly ArtifactService _artifacts;
    private readonly IArtifactRepository _artifactRepo;
    private readonly IReleaseEvidenceRepository _repo;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public ReleaseEvidenceService(
        DurableJobService jobs,
        IReleaseProvenanceCollector collector,
        ArtifactService artifacts,
        IArtifactRepository artifactRepo,
        IReleaseEvidenceRepository repo,
        IEvidenceRepository evidence,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _jobs = jobs;
        _collector = collector;
        _artifacts = artifacts;
        _artifactRepo = artifactRepo;
        _repo = repo;
        _evidence = evidence;
        _uow = uow;
        _clock = clock;
    }

    public Task<EnqueuedJobDto> EnqueueAsync(ReleaseEvidenceJobRequest request, CancellationToken ct)
        => _jobs.EnqueueAsync(request.ProjectId, DurableJobType.ReleaseEvidence, request.SubjectKey, request, 2, ct);

    public async Task<ReleaseEvidenceBundleDto> ExecuteJobAsync(
        DurableJob job,
        ReleaseEvidenceJobRequest request,
        CancellationToken ct)
    {
        ValidateRequest(request);
        var snapshot = await _collector.CollectAsync(request.RepositoryRoot, ct);
        BindSnapshotToRequestedRevision(request, snapshot);
        var generatedAt = _clock.UtcNow;
        var sbom = BuildCycloneDx(request, snapshot, generatedAt);
        var sbomBytes = JsonSerializer.SerializeToUtf8Bytes(sbom, ArtifactJson);
        var sbomSha256 = Convert.ToHexString(SHA256.HashData(sbomBytes)).ToLowerInvariant();

        var sourceCommit = Value(snapshot, "@dip/source-commit");
        var sourceTreeRoot = Value(snapshot, "@dip/source-tree-root-sha256");
        var provenanceChain = Value(snapshot, "@dip/provenance-chain-sha256");
        var attestation = new
        {
            _type = "https://in-toto.io/Statement/v1",
            predicateType = "https://slsa.dev/provenance/v1",
            subject = new object[]
            {
                new
                {
                    name = $"dip-release:{request.BuildId}",
                    digest = new Dictionary<string, string>
                    {
                        ["gitCommit"] = request.CommitSha,
                        ["sourceTreeSha256"] = sourceTreeRoot ?? "not-evaluated",
                        ["provenanceChainSha256"] = provenanceChain ?? "not-evaluated"
                    }
                },
                new
                {
                    name = "sbom.cdx.json",
                    digest = new Dictionary<string, string> { ["sha256"] = sbomSha256 }
                }
            },
            predicate = new
            {
                buildDefinition = new
                {
                    buildType = "https://dip.dev/buildtypes/release-evidence/v2",
                    externalParameters = new
                    {
                        projectId = request.ProjectId,
                        request.SubjectKey,
                        request.CommitSha,
                        request.BuildId
                    },
                    resolvedDependencies = snapshot.Files
                        .Where(x => !x.Key.StartsWith("@dip/", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x.Key, StringComparer.Ordinal)
                        .Select(x => new { uri = "repo:" + x.Key, digest = new { sha256 = x.Value } })
                        .ToArray()
                },
                runDetails = new
                {
                    builder = new { id = "dip:release-evidence-service" },
                    metadata = new
                    {
                        invocationId = job.Id,
                        startedOn = generatedAt,
                        sourceCommitObserved = sourceCommit,
                        sourceRevisionMatched = sourceCommit is null || RevisionEquals(sourceCommit, request.CommitSha)
                    }
                }
            }
        };

        var manifestPayload = new
        {
            schemaVersion = "2.0",
            request.ProjectId,
            request.CommitSha,
            request.BuildId,
            generatedAtUtc = generatedAt,
            snapshot.Runtime,
            snapshot.OperatingSystem,
            sourceCommit,
            sourceTreeRootSha256 = sourceTreeRoot,
            provenanceChainSha256 = provenanceChain,
            sbomSha256,
            files = snapshot.Files,
            componentCount = snapshot.Components.Count,
            attestation
        };

        var signedPayloadBytes = JsonSerializer.SerializeToUtf8Bytes(manifestPayload, ArtifactJson);
        var signature = ReleaseBundleSigner.Sign(signedPayloadBytes);
        if (ReleaseBundleSigner.SignatureRequired && !signature.Signed)
            throw new InvalidOperationException("Release evidence signature is required but no valid release signing key is configured.");

        var manifest = new
        {
            schemaVersion = "2.0",
            payload = manifestPayload,
            signedPayloadSha256 = signature.PayloadSha256,
            signedPayloadBase64 = Convert.ToBase64String(signedPayloadBytes),
            signature = new
            {
                status = signature.Signed ? "Signed" : "NotConfigured",
                algorithm = signature.Algorithm,
                keyId = signature.KeyId,
                signatureBase64 = signature.SignatureBase64
            }
        };

        var links = new ArtifactLinkContext(job.Id);
        var manifestRecord = await _artifacts.StoreJsonAsync(
            request.ProjectId,
            "release-manifest",
            "manifest.json",
            manifest,
            links,
            "release",
            ct);
        var sbomRecord = await _artifacts.StoreJsonAsync(
            request.ProjectId,
            "sbom",
            "sbom.cdx.json",
            sbom,
            links,
            "release",
            ct);

        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, ArtifactJson);
        byte[] bundleBytes;
        await using (var ms = new MemoryStream())
        {
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                // Preserve the two-entry release bundle contract. The attestation and
                // detached signature are embedded in manifest.json so older consumers
                // that expect manifest+SBOM remain compatible.
                WriteEntry(zip, "manifest.json", manifestBytes);
                WriteEntry(zip, "sbom.cdx.json", sbomBytes);
            }
            bundleBytes = ms.ToArray();
        }

        await using var bundleStream = new MemoryStream(bundleBytes, writable: false);
        var bundleRecord = await _artifacts.StoreAsync(
            request.ProjectId,
            "release-evidence-bundle",
            $"release-evidence-{request.CommitSha[..Math.Min(12, request.CommitSha.Length)]}.zip",
            bundleStream,
            "application/zip",
            links,
            "release",
            TimeSpan.FromDays(3650),
            ct);
        var hash = Convert.ToHexString(SHA256.HashData(bundleBytes)).ToLowerInvariant();
        var bundleSignature = ReleaseBundleSigner.Sign(bundleBytes);
        if (ReleaseBundleSigner.SignatureRequired && !bundleSignature.Signed)
            throw new InvalidOperationException("Release bundle signature is required but no valid release signing key is configured.");
        ArtifactRecord? bundleSignatureRecord = null;
        if (bundleSignature.Signed)
        {
            bundleSignatureRecord = await _artifacts.StoreJsonAsync(
                request.ProjectId,
                "release-bundle-signature",
                "bundle.sig.json",
                new
                {
                    schema = "dip.release-bundle-signature.v1",
                    bundleSha256 = hash,
                    bundleSignature.Algorithm,
                    bundleSignature.KeyId,
                    bundleSignature.SignatureBase64,
                    signedAtUtc = generatedAt
                },
                links,
                "release",
                ct);
        }

        var entity = new ReleaseEvidenceBundle(
            Guid.NewGuid(),
            request.ProjectId,
            request.CommitSha,
            request.BuildId,
            ReleaseEvidenceBundleStatus.Succeeded,
            manifestRecord.Id.ToString("N"),
            sbomRecord.Id.ToString("N"),
            bundleRecord.Id.ToString("N"),
            hash,
            generatedAt);
        await _repo.AddAsync(entity, ct);

        var signingSummary = bundleSignature.Signed
            ? $"signed with {bundleSignature.Algorithm} key '{bundleSignature.KeyId}'"
            : "attested but unsigned because release signing is not configured";
        await _evidence.AddAsync(
            new EvidenceRecord(
                Guid.NewGuid(),
                request.ProjectId,
                null,
                request.SubjectKey,
                EvidenceSourceType.Build,
                "release-provenance",
                EvidenceOutcome.Succeeded,
                $"Release evidence bundle {request.BuildId}",
                $"Release provenance, CycloneDX SBOM and SLSA-style attestation were captured for {request.CommitSha}; bundle is {signingSummary}.",
                EvidenceConfidence.High,
                bundleRecord.Id.ToString(),
                JsonSerializer.Serialize(new
                {
                    releaseEvidenceId = entity.Id,
                    hash,
                    manifestArtifactId = manifestRecord.Id,
                    sbomArtifactId = sbomRecord.Id,
                    bundleArtifactId = bundleRecord.Id,
                    signedPayloadSha256 = signature.PayloadSha256,
                    manifestSignatureStatus = signature.Signed ? "Signed" : "NotConfigured",
                    manifestSignatureAlgorithm = signature.Algorithm,
                    manifestSigningKeyId = signature.KeyId,
                    bundleSignatureStatus = bundleSignature.Signed ? "Signed" : "NotConfigured",
                    bundleSignatureAlgorithm = bundleSignature.Algorithm,
                    bundleSigningKeyId = bundleSignature.KeyId,
                    bundleSignatureArtifactId = bundleSignatureRecord?.Id,
                    sourceCommit,
                    sourceTreeRootSha256 = sourceTreeRoot,
                    provenanceChainSha256 = provenanceChain
                }),
                generatedAt,
                generatedAt),
            ct);
        await _uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ReleaseEvidenceBundleDto?> GetLatestAsync(Guid projectId, CancellationToken ct)
    {
        var e = await _repo.GetLatestAsync(projectId, ct);
        return e is null ? null : Map(e);
    }

    private static void ValidateRequest(ReleaseEvidenceJobRequest request)
    {
        if (request.ProjectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.CommitSha) || request.CommitSha.Length > 64 || !request.CommitSha.All(Uri.IsHexDigit))
            throw new ArgumentException("CommitSha must be a hexadecimal git revision.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.BuildId)) throw new ArgumentException("BuildId is required.", nameof(request));
    }

    private static void BindSnapshotToRequestedRevision(ReleaseEvidenceJobRequest request, ProvenanceSnapshot snapshot)
    {
        var observed = Value(snapshot, "@dip/source-commit");
        if (!string.IsNullOrWhiteSpace(observed) && !RevisionEquals(observed, request.CommitSha))
            throw new InvalidOperationException($"Release provenance source revision '{observed}' does not match requested release revision '{request.CommitSha}'.");
    }

    private static bool RevisionEquals(string a, string b)
        => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? Value(ProvenanceSnapshot snapshot, string key)
        => snapshot.Files.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static object BuildCycloneDx(ReleaseEvidenceJobRequest request, ProvenanceSnapshot snapshot, DateTimeOffset generatedAt)
        => new
        {
            bomFormat = "CycloneDX",
            specVersion = "1.5",
            serialNumber = $"urn:uuid:{Guid.NewGuid()}",
            version = 1,
            metadata = new
            {
                timestamp = generatedAt,
                component = new
                {
                    type = "application",
                    name = "Development Intelligence Platform target",
                    version = request.CommitSha
                }
            },
            components = snapshot.Components.Select(x => new
            {
                type = x.Type,
                name = x.Name,
                version = x.Version,
                properties = new[]
                {
                    new { name = "dip:path", value = x.Path ?? string.Empty },
                    new { name = "dip:sha256", value = x.Sha256 ?? string.Empty }
                }
            }).ToArray()
        };

    private static void WriteEntry(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static ReleaseEvidenceBundleDto Map(ReleaseEvidenceBundle x)
        => new(x.Id, x.CommitSha, x.BuildId, x.Status.ToString(), x.ManifestArtifactId, x.SbomArtifactId, x.BundleArtifactId, x.ArtifactSha256, x.CreatedAtUtc);

    private sealed record ReleaseSignature(
        bool Signed,
        string PayloadSha256,
        string? Algorithm,
        string? KeyId,
        string? SignatureBase64);

    private static class ReleaseBundleSigner
    {
        internal static bool SignatureRequired
            => string.Equals(Environment.GetEnvironmentVariable("DIP_RELEASE_REQUIRE_SIGNATURE"), "1", StringComparison.Ordinal);

        internal static ReleaseSignature Sign(byte[] payload)
        {
            var payloadHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            var pem = ReadPem();
            if (!string.IsNullOrWhiteSpace(pem))
            {
                try
                {
                    using var ecdsa = ECDsa.Create();
                    ecdsa.ImportFromPem(pem);
                    var signature = ecdsa.SignData(payload, HashAlgorithmName.SHA256);
                    return new(true, payloadHash, "ecdsa-sha256", ResolveKeyId(ecdsa.ExportSubjectPublicKeyInfo()), Convert.ToBase64String(signature));
                }
                catch (Exception ex) when (ex is CryptographicException or ArgumentException)
                {
                    try
                    {
                        using var rsa = RSA.Create();
                        rsa.ImportFromPem(pem);
                        var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                        return new(true, payloadHash, "rsa-pss-sha256", ResolveKeyId(rsa.ExportSubjectPublicKeyInfo()), Convert.ToBase64String(signature));
                    }
                    catch (Exception rsaEx) when (rsaEx is CryptographicException or ArgumentException)
                    {
                        throw new InvalidOperationException("Configured release signing PEM could not be imported as ECDSA or RSA.", rsaEx);
                    }
                }
            }

            var hmacText = Environment.GetEnvironmentVariable("DIP_RELEASE_SIGNING_HMAC_KEY_BASE64");
            if (!string.IsNullOrWhiteSpace(hmacText))
            {
                byte[] key;
                try { key = Convert.FromBase64String(hmacText.Trim()); }
                catch (FormatException ex) { throw new InvalidOperationException("DIP_RELEASE_SIGNING_HMAC_KEY_BASE64 is not valid base64.", ex); }
                if (key.Length < 32)
                    throw new InvalidOperationException("Release HMAC signing key must contain at least 256 bits.");
                var signature = HMACSHA256.HashData(key, payload);
                return new(true, payloadHash, "hmac-sha256", Environment.GetEnvironmentVariable("DIP_RELEASE_SIGNING_KEY_ID")?.Trim() ?? "hmac-local", Convert.ToBase64String(signature));
            }

            return new(false, payloadHash, null, null, null);
        }

        private static string? ReadPem()
        {
            var inline = Environment.GetEnvironmentVariable("DIP_RELEASE_SIGNING_PRIVATE_KEY_PEM");
            if (!string.IsNullOrWhiteSpace(inline)) return inline;
            var path = Environment.GetEnvironmentVariable("DIP_RELEASE_SIGNING_PRIVATE_KEY_FILE")?.Trim();
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!Path.IsPathRooted(path) || !File.Exists(path))
                throw new InvalidOperationException("DIP_RELEASE_SIGNING_PRIVATE_KEY_FILE must be an existing absolute path outside the release repository payload.");
            return File.ReadAllText(path);
        }

        private static string ResolveKeyId(byte[] subjectPublicKeyInfo)
        {
            var configured = Environment.GetEnvironmentVariable("DIP_RELEASE_SIGNING_KEY_ID")?.Trim();
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            var digest = Convert.ToHexString(SHA256.HashData(subjectPublicKeyInfo)).ToLowerInvariant();
            return "sha256:" + digest[..24];
        }
    }
}
