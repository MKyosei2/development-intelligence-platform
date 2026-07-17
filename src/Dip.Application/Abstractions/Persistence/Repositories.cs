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
public interface IUnitOfWork{Task<int> SaveChangesAsync(CancellationToken ct);}
