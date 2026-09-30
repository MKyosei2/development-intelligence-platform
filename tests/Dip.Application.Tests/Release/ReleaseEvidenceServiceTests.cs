// Purpose: Verifies release evidence enqueueing, provenance capture, SBOM/ZIP creation, hashing, persistence, and latest-bundle mapping.
using System.IO.Compression;
using System.Security.Cryptography;
using Dip.Application.Abstractions.Artifacts;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Provenance;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Jobs;
using Dip.Application.Features.Release;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.Release;

public sealed class ReleaseEvidenceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string Sha = new('a', 40);

    [Fact]
    public async Task EnqueueAsync_PersistsDurableJobAndOutboxRequest()
    {
        var fixture = new Fixture();
        var request = fixture.Request();

        var result = await fixture.Service.EnqueueAsync(request, CancellationToken.None);

        result.JobId.Should().NotBeEmpty();
        result.Status.Should().Be(DurableJobStatus.Queued.ToString());
        fixture.Jobs.Items.Should().ContainSingle(x => x.Id == result.JobId);
        fixture.Outbox.Items.Should().ContainSingle();
        fixture.Outbox.Items[0].MessageType.Should().Be(DurableJobService.RequestedMessageType);
        fixture.UnitOfWork.Saves.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteJobAsync_CreatesManifestSbomBundleHashAndEvidence()
    {
        var fixture = new Fixture();
        var request = fixture.Request();
        var job = new DurableJob(
            Guid.NewGuid(),
            request.ProjectId,
            DurableJobType.ReleaseEvidence,
            request.SubjectKey,
            "{}",
            2,
            Now);

        var result = await fixture.Service.ExecuteJobAsync(job, request, CancellationToken.None);

        result.CommitSha.Should().Be(Sha);
        result.BuildId.Should().Be("build-42");
        result.Status.Should().Be(ReleaseEvidenceBundleStatus.Succeeded.ToString());
        result.ManifestArtifactId.Should().NotBeNullOrWhiteSpace();
        result.SbomArtifactId.Should().NotBeNullOrWhiteSpace();
        result.BundleArtifactId.Should().NotBeNullOrWhiteSpace();
        result.ArtifactSha256.Should().MatchRegex("^[0-9a-f]{64}$");

        fixture.ReleaseEvidence.Items.Should().ContainSingle();
        fixture.Evidence.Items.Should().ContainSingle();
        fixture.Evidence.Items[0].EvidenceKind.Should().Be("release-provenance");
        fixture.Artifacts.Records.Should().HaveCount(3);
        fixture.Artifacts.Records.Select(x => x.Kind).Should().BeEquivalentTo(
            ["release-manifest", "sbom", "release-evidence-bundle"]);

        var bundleRecord = fixture.Artifacts.Records.Single(x => x.Kind == "release-evidence-bundle");
        var blob = fixture.Artifacts.Blobs.Single(x => x.Id == bundleRecord.ArtifactBlobId);
        var bundleBytes = fixture.Store.Bytes[blob.StorageKey];
        Convert.ToHexString(SHA256.HashData(bundleBytes)).ToLowerInvariant().Should().Be(result.ArtifactSha256);

        using var ms = new MemoryStream(bundleBytes, writable: false);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        zip.Entries.Select(x => x.FullName).Should().BeEquivalentTo(["manifest.json", "sbom.cdx.json"]);

        var manifestText = ReadEntry(zip, "manifest.json");
        manifestText.Should().Contain(Sha);
        manifestText.Should().Contain("build-42");
        manifestText.Should().Contain(".NET 8");

        var sbomText = ReadEntry(zip, "sbom.cdx.json");
        sbomText.Should().Contain("CycloneDX");
        sbomText.Should().Contain("Example.Component");
        sbomText.Should().Contain("1.2.3");
        fixture.UnitOfWork.Saves.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task GetLatestAsync_NoBundle_ReturnsNull()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.GetLatestAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestAsync_MapsPersistedBundle()
    {
        var fixture = new Fixture();
        var projectId = Guid.NewGuid();
        var entity = new ReleaseEvidenceBundle(
            Guid.NewGuid(),
            projectId,
            Sha,
            "build-existing",
            ReleaseEvidenceBundleStatus.Succeeded,
            new string('1', 32),
            new string('2', 32),
            new string('3', 32),
            new string('b', 64),
            Now);
        fixture.ReleaseEvidence.Items.Add(entity);

        var result = await fixture.Service.GetLatestAsync(projectId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
        result.CommitSha.Should().Be(Sha);
        result.BuildId.Should().Be("build-existing");
        result.ManifestArtifactId.Should().Be(new string('1', 32));
        result.SbomArtifactId.Should().Be(new string('2', 32));
        result.BundleArtifactId.Should().Be(new string('3', 32));
        result.ArtifactSha256.Should().Be(new string('b', 64));
    }

    private static string ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        entry.Should().NotBeNull();
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    private sealed class Fixture
    {
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeClock Clock { get; } = new();
        public FakeJobs Jobs { get; } = new();
        public FakeOutbox Outbox { get; } = new();
        public FakeArtifactStore Store { get; } = new();
        public FakeArtifactRepository Artifacts { get; } = new();
        public FakeReleaseEvidenceRepository ReleaseEvidence { get; } = new();
        public FakeEvidenceRepository Evidence { get; } = new();
        public FakeProvenanceCollector Provenance { get; } = new();

        public ReleaseEvidenceService Service
        {
            get
            {
                var jobs = new DurableJobService(Jobs, Outbox, UnitOfWork, Clock);
                var artifacts = new ArtifactService(Store, Artifacts, UnitOfWork, Clock);
                return new ReleaseEvidenceService(
                    jobs,
                    Provenance,
                    artifacts,
                    Artifacts,
                    ReleaseEvidence,
                    Evidence,
                    UnitOfWork,
                    Clock);
            }
        }

        public ReleaseEvidenceJobRequest Request() =>
            new(Guid.NewGuid(), "release", Sha, "build-42", "/repo");
    }

    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { Saves++; return Task.FromResult(1); }
    }

    private sealed class FakeJobs : IDurableJobRepository
    {
        public List<DurableJob> Items { get; } = [];
        public Task AddAsync(DurableJob job, CancellationToken ct) { Items.Add(job); return Task.CompletedTask; }
        public Task<DurableJob?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DurableJob>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<DurableJob>>(Items.Where(x => x.ProjectId == projectId).Take(take).ToArray());
        public Task<DurableJob?> TryClaimAsync(Guid id, string owner, DateTimeOffset now, TimeSpan lease, CancellationToken ct) => Task.FromResult<DurableJob?>(null);
        public Task<IReadOnlyList<DurableJob>> ListExpiredLeasesAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<DurableJob>>([]);
        public Task<long> CountQueuedAsync(CancellationToken ct) => Task.FromResult((long)Items.Count(x => x.Status == DurableJobStatus.Queued));
    }

    private sealed class FakeOutbox : IOutboxRepository
    {
        public List<OutboxMessage> Items { get; } = [];
        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) { Items.Add(message); return Task.CompletedTask; }
        public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OutboxMessage>>(Items.Take(batchSize).ToArray());
        public Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    }

    private sealed class FakeArtifactStore : IArtifactStore
    {
        private int _next;
        public string ProviderName => "memory";
        public Dictionary<string, byte[]> Bytes { get; } = [];

        public async Task<StoredArtifactContent> PutAsync(Stream content, string contentType, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var key = $"artifact-{Interlocked.Increment(ref _next)}";
            Bytes[key] = bytes;
            return new StoredArtifactContent(key, sha, bytes.LongLength, contentType);
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(Bytes[storageKey], writable: false));

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) { Bytes.Remove(storageKey); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(Bytes.ContainsKey(storageKey));
    }

    private sealed class FakeArtifactRepository : IArtifactRepository
    {
        public List<ArtifactBlob> Blobs { get; } = [];
        public List<ArtifactRecord> Records { get; } = [];

        public Task<ArtifactBlob?> FindBlobAsync(string provider, string sha256, CancellationToken ct) => Task.FromResult(Blobs.FirstOrDefault(x => x.StorageProvider == provider && x.Sha256 == sha256));
        public Task<ArtifactBlob> GetOrCreateBlobAsync(ArtifactBlob candidate, CancellationToken ct)
        {
            var existing = Blobs.FirstOrDefault(x => x.StorageProvider == candidate.StorageProvider && x.Sha256 == candidate.Sha256);
            if (existing is not null) return Task.FromResult(existing);
            Blobs.Add(candidate);
            return Task.FromResult(candidate);
        }
        public Task<ArtifactRecord?> GetRecordAsync(Guid id, CancellationToken ct) => Task.FromResult(Records.FirstOrDefault(x => x.Id == id));
        public Task<ArtifactRecord?> GetRecordForUpdateAsync(Guid id, CancellationToken ct) => GetRecordAsync(id, ct);
        public Task<ArtifactBlob?> GetBlobAsync(Guid id, CancellationToken ct) => Task.FromResult(Blobs.FirstOrDefault(x => x.Id == id));
        public Task<ArtifactBlob?> GetBlobForUpdateAsync(Guid id, CancellationToken ct) => GetBlobAsync(id, ct);
        public Task AddBlobAsync(ArtifactBlob blob, CancellationToken ct) { Blobs.Add(blob); return Task.CompletedTask; }
        public Task AddRecordAsync(ArtifactRecord record, CancellationToken ct) { Records.Add(record); return Task.CompletedTask; }
        public Task<IReadOnlyList<ArtifactRecord>> ListForJobAsync(Guid jobId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ArtifactRecord>>(Records.Where(x => x.JobId == jobId).ToArray());
        public Task<IReadOnlyList<ArtifactRecord>> ListExpiredAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<ArtifactRecord>>(Records.Where(x => x.ExpiresAtUtc <= now).Take(take).ToArray());
        public Task<bool> HasRecordsForBlobAsync(Guid blobId, CancellationToken ct) => Task.FromResult(Records.Any(x => x.ArtifactBlobId == blobId));
        public void RemoveRecord(ArtifactRecord record) => Records.Remove(record);
        public void RemoveBlob(ArtifactBlob blob) => Blobs.Remove(blob);
    }

    private sealed class FakeReleaseEvidenceRepository : IReleaseEvidenceRepository
    {
        public List<ReleaseEvidenceBundle> Items { get; } = [];
        public Task AddAsync(ReleaseEvidenceBundle bundle, CancellationToken ct) { Items.Add(bundle); return Task.CompletedTask; }
        public Task<ReleaseEvidenceBundle?> GetLatestAsync(Guid projectId, CancellationToken ct) => Task.FromResult(Items.LastOrDefault(x => x.ProjectId == projectId));
        public Task<IReadOnlyList<ReleaseEvidenceBundle>> ListAsync(Guid projectId, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<ReleaseEvidenceBundle>>(Items.Where(x => x.ProjectId == projectId).TakeLast(take).ToArray());
    }

    private sealed class FakeEvidenceRepository : IEvidenceRepository
    {
        public List<EvidenceRecord> Items { get; } = [];
        public Task AddAsync(EvidenceRecord evidence, CancellationToken ct) { Items.Add(evidence); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence, CancellationToken ct) { Items.AddRange(evidence); return Task.CompletedTask; }
        public Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(Guid projectId, DateTimeOffset? observedSince, CancellationToken ct) => Task.FromResult<IReadOnlyList<EvidenceRecord>>(Items.Where(x => x.ProjectId == projectId).ToArray());
    }

    private sealed class FakeProvenanceCollector : IReleaseProvenanceCollector
    {
        public Task<ProvenanceSnapshot> CollectAsync(string repositoryRoot, CancellationToken ct) =>
            Task.FromResult(new ProvenanceSnapshot(
                repositoryRoot,
                ".NET 8",
                "unit-test-os",
                [new ProvenanceComponent("library", "Example.Component", "1.2.3", "src/example", new string('c', 64))],
                new Dictionary<string, string> { ["README.md"] = new string('d', 64) }));
    }
}
