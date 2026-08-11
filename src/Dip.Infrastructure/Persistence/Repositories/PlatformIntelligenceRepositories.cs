using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ArtifactRepository(DipDbContext db):IArtifactRepository
{
    public Task<ArtifactBlob?> FindBlobAsync(string provider,string sha256,CancellationToken ct)=>db.ArtifactBlobs.SingleOrDefaultAsync(x=>x.StorageProvider==provider&&x.Sha256==sha256,ct);
    public async Task<ArtifactBlob> GetOrCreateBlobAsync(ArtifactBlob candidate,CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO artifact_blobs (id,sha256,size_bytes,content_type,storage_provider,storage_key,created_at_utc) VALUES ({candidate.Id},{candidate.Sha256},{candidate.SizeBytes},{candidate.ContentType},{candidate.StorageProvider},{candidate.StorageKey},{candidate.CreatedAtUtc}) ON CONFLICT (storage_provider,sha256) DO NOTHING",ct);
        return await db.ArtifactBlobs.SingleAsync(x=>x.StorageProvider==candidate.StorageProvider&&x.Sha256==candidate.Sha256,ct);
    }
    public Task<ArtifactRecord?> GetRecordAsync(Guid id,CancellationToken ct)=>db.ArtifactRecords.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<ArtifactRecord?> GetRecordForUpdateAsync(Guid id,CancellationToken ct)=>db.ArtifactRecords.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<ArtifactBlob?> GetBlobAsync(Guid id,CancellationToken ct)=>db.ArtifactBlobs.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<ArtifactBlob?> GetBlobForUpdateAsync(Guid id,CancellationToken ct)=>db.ArtifactBlobs.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task AddBlobAsync(ArtifactBlob blob,CancellationToken ct)=>db.ArtifactBlobs.AddAsync(blob,ct).AsTask();
    public Task AddRecordAsync(ArtifactRecord record,CancellationToken ct)=>db.ArtifactRecords.AddAsync(record,ct).AsTask();
    public async Task<IReadOnlyList<ArtifactRecord>> ListForJobAsync(Guid jobId,CancellationToken ct)=>await db.ArtifactRecords.AsNoTracking().Where(x=>x.JobId==jobId).OrderBy(x=>x.CreatedAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<ArtifactRecord>> ListExpiredAsync(DateTimeOffset now,int take,CancellationToken ct)=>await db.ArtifactRecords.Where(x=>x.ExpiresAtUtc!=null&&x.ExpiresAtUtc<=now).OrderBy(x=>x.ExpiresAtUtc).Take(Math.Clamp(take,1,1000)).ToListAsync(ct);
    public Task<bool> HasRecordsForBlobAsync(Guid blobId,CancellationToken ct)=>db.ArtifactRecords.AnyAsync(x=>x.ArtifactBlobId==blobId,ct);
    public void RemoveRecord(ArtifactRecord record)=>db.ArtifactRecords.Remove(record);
    public void RemoveBlob(ArtifactBlob blob)=>db.ArtifactBlobs.Remove(blob);
}

public sealed class DurableJobRepository(DipDbContext db):IDurableJobRepository
{
    public Task AddAsync(DurableJob job,CancellationToken ct)=>db.DurableJobs.AddAsync(job,ct).AsTask();
    public Task<DurableJob?> GetAsync(Guid id,CancellationToken ct)=>db.DurableJobs.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<DurableJob>> ListRecentAsync(Guid projectId,int take,CancellationToken ct)=>await db.DurableJobs.AsNoTracking().Where(x=>x.ProjectId==projectId).OrderByDescending(x=>x.CreatedAtUtc).Take(Math.Clamp(take,1,200)).ToListAsync(ct);
    public async Task<DurableJob?> TryClaimAsync(Guid id,string owner,DateTimeOffset now,TimeSpan lease,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var job=await db.DurableJobs.FromSqlInterpolated($"SELECT * FROM durable_jobs WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(job is null||!job.CanClaim(now)){await tx.CommitAsync(ct);return null;}
        job.Claim(owner,now,lease);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return job;
    }
    public async Task<IReadOnlyList<DurableJob>> ListExpiredLeasesAsync(DateTimeOffset now,int take,CancellationToken ct)=>await db.DurableJobs.Where(x=>x.Status==DurableJobStatus.Running&&x.LeaseExpiresAtUtc!=null&&x.LeaseExpiresAtUtc<=now).OrderBy(x=>x.LeaseExpiresAtUtc).Take(Math.Clamp(take,1,500)).ToListAsync(ct);
}

public sealed class IncidentRepository(DipDbContext db):IIncidentRepository
{
    public Task<Incident?> FindByFingerprintAsync(Guid projectId,string fingerprint,CancellationToken ct)=>db.Incidents.SingleOrDefaultAsync(x=>x.ProjectId==projectId&&x.Fingerprint==fingerprint,ct);
    public Task<Incident?> GetAsync(Guid id,CancellationToken ct)=>db.Incidents.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<Incident?> FindLatestOpenForSubjectAsync(Guid projectId,string subjectKey,CancellationToken ct)=>db.Incidents.Where(x=>x.ProjectId==projectId&&x.SubjectKey==subjectKey&&x.Status!=IncidentStatus.Resolved).OrderByDescending(x=>x.LastSeenAtUtc).FirstOrDefaultAsync(ct);
    public Task AddAsync(Incident incident,CancellationToken ct)=>db.Incidents.AddAsync(incident,ct).AsTask();
    public Task AddOccurrenceAsync(IncidentOccurrence occurrence,CancellationToken ct)=>db.IncidentOccurrences.AddAsync(occurrence,ct).AsTask();
    public Task AddEventAsync(IncidentEvent incidentEvent,CancellationToken ct)=>db.IncidentEvents.AddAsync(incidentEvent,ct).AsTask();
    public async Task<IReadOnlyList<Incident>> ListRecentAsync(Guid projectId,int take,CancellationToken ct)=>await db.Incidents.AsNoTracking().Where(x=>x.ProjectId==projectId).OrderByDescending(x=>x.LastSeenAtUtc).Take(Math.Clamp(take,1,200)).ToListAsync(ct);
    public async Task<IReadOnlyList<IncidentEvent>> ListEventsAsync(Guid incidentId,CancellationToken ct)=>await db.IncidentEvents.AsNoTracking().Where(x=>x.IncidentId==incidentId).OrderBy(x=>x.OccurredAtUtc).ToListAsync(ct);
}

public sealed class BenchmarkRepository(DipDbContext db):IBenchmarkRepository
{
    public Task AddDefinitionAsync(BenchmarkDefinition definition,CancellationToken ct)=>db.BenchmarkDefinitions.AddAsync(definition,ct).AsTask();
    public Task<BenchmarkDefinition?> GetDefinitionAsync(Guid id,CancellationToken ct)=>db.BenchmarkDefinitions.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<BenchmarkDefinition?> GetDefinitionByKeyAsync(string key,CancellationToken ct)=>db.BenchmarkDefinitions.SingleOrDefaultAsync(x=>x.Key==key,ct);
    public async Task<IReadOnlyList<BenchmarkDefinition>> ListDefinitionsAsync(CancellationToken ct)=>await db.BenchmarkDefinitions.AsNoTracking().OrderBy(x=>x.Key).ToListAsync(ct);
    public Task AddRunAsync(BenchmarkRun run,CancellationToken ct)=>db.BenchmarkRuns.AddAsync(run,ct).AsTask();
    public Task<BenchmarkRun?> GetRunAsync(Guid id,CancellationToken ct)=>db.BenchmarkRuns.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<BenchmarkRun?> FindBaselineAsync(Guid projectId,Guid definitionId,string env,string hash,string excludingCommit,CancellationToken ct)=>db.BenchmarkRuns.AsNoTracking().Where(x=>x.ProjectId==projectId&&x.BenchmarkDefinitionId==definitionId&&x.EnvironmentFingerprint==env&&x.ConfigurationHash==hash&&x.Status==BenchmarkRunStatus.Succeeded&&x.CommitSha!=excludingCommit).OrderByDescending(x=>x.FinishedAtUtc).FirstOrDefaultAsync(ct);
    public Task AddMetricsAsync(IEnumerable<BenchmarkMetric> metrics,CancellationToken ct)=>db.BenchmarkMetrics.AddRangeAsync(metrics,ct);
    public async Task<IReadOnlyList<BenchmarkMetric>> ListMetricsAsync(Guid runId,CancellationToken ct)=>await db.BenchmarkMetrics.AsNoTracking().Where(x=>x.BenchmarkRunId==runId).OrderBy(x=>x.Name).ThenBy(x=>x.Aggregation).ToListAsync(ct);
    public Task AddComparisonsAsync(IEnumerable<BenchmarkComparison> comparisons,CancellationToken ct)=>db.BenchmarkComparisons.AddRangeAsync(comparisons,ct);
    public async Task<IReadOnlyList<BenchmarkComparison>> ListComparisonsAsync(Guid runId,CancellationToken ct)=>await db.BenchmarkComparisons.AsNoTracking().Where(x=>x.CurrentRunId==runId).OrderBy(x=>x.MetricName).ToListAsync(ct);
}
