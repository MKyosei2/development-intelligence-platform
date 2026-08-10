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
public interface IQaRunRepository
{
    Task AddAsync(QaRun run,CancellationToken ct);
    Task<QaRun?> GetAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<QaRun>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IQaFindingRepository
{
    Task AddRangeAsync(IEnumerable<QaFinding> findings,CancellationToken ct);
    Task<QaFinding?> GetAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<QaFinding>> ListByRunAsync(Guid runId,CancellationToken ct);
}
public interface IReproductionAttemptRepository
{
    Task AddAsync(ReproductionAttempt attempt,CancellationToken ct);
    Task<IReadOnlyList<ReproductionAttempt>> ListByFindingAsync(Guid findingId,CancellationToken ct);
}
public interface IHardwareProfileRepository
{
    Task AddAsync(HardwareProfile profile,CancellationToken ct);
    Task<HardwareProfile?> GetAsync(Guid id,CancellationToken ct);
    Task<HardwareProfile?> FindAsync(string name,string platform,CancellationToken ct);
    Task<IReadOnlyList<HardwareProfile>> ListAsync(CancellationToken ct);
}
public interface IHardwareCalibrationRepository
{
    Task AddAsync(HardwareCalibration calibration,CancellationToken ct);
    Task<HardwareCalibration?> GetLatestAsync(Guid projectId,CancellationToken ct);
}
public interface IPerformanceSimulationRepository
{
    Task AddAsync(PerformanceSimulationRun run,CancellationToken ct);
    Task<PerformanceSimulationRun?> GetAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<PerformanceSimulationRun>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
}
public interface IUnitOfWork{Task<int> SaveChangesAsync(CancellationToken ct);}
