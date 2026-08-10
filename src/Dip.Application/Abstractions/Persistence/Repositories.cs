using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct);
    Task AddAsync(Project project,CancellationToken ct);
}
public interface IPipelineRunRepository
{
    Task<PipelineRun?> GetByIdAsync(Guid id,CancellationToken ct);
    Task<PipelineRun?> FindByExternalRunAsync(Guid projectId,string externalRunId,string collectorType,int attemptNumber,CancellationToken ct);
    Task AddAsync(PipelineRun run,CancellationToken ct);
}
public interface IMetricRepository{Task AddRangeAsync(IEnumerable<Metric> metrics,CancellationToken ct);}
public interface IQualityIssueRepository
{
    Task<QualityIssue?> FindOpenByFingerprintAsync(Guid projectId,string fingerprint,CancellationToken ct);
    Task AddAsync(QualityIssue issue,CancellationToken ct);
}
public interface IEvidenceRepository
{
    Task AddAsync(EvidenceRecord evidence,CancellationToken ct);
    Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence,CancellationToken ct);
    Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(Guid projectId,DateTimeOffset? observedSince,CancellationToken ct);
}
public interface IProjectClaimRepository
{
    Task<ProjectClaim?> GetByIdAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<ProjectClaim>> ListByProjectAsync(Guid projectId,CancellationToken ct);
    Task AddAsync(ProjectClaim claim,CancellationToken ct);
}
public interface IClaimEvidenceLinkRepository
{
    Task ReplaceForClaimAsync(Guid claimId,IReadOnlyCollection<ClaimEvidenceLink> links,CancellationToken ct);
}
public interface IProjectTruthRepository
{
    Task<ProjectTruthSnapshot?> GetLatestAsync(Guid projectId,CancellationToken ct);
    Task AddAsync(ProjectTruthSnapshot snapshot,CancellationToken ct);
}
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry,CancellationToken ct);
}
public interface IUnitOfWork{Task<int> SaveChangesAsync(CancellationToken ct);}
