// Purpose: Defines the IBenchmarkRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IBenchmarkRepository
{
    Task AddDefinitionAsync(BenchmarkDefinition definition,CancellationToken ct);
    Task<BenchmarkDefinition?> GetDefinitionAsync(Guid id,CancellationToken ct);
    Task<BenchmarkDefinition?> GetDefinitionByKeyAsync(string key,CancellationToken ct);
    Task<IReadOnlyList<BenchmarkDefinition>> ListDefinitionsAsync(CancellationToken ct);
    Task AddRunAsync(BenchmarkRun run,CancellationToken ct);
    Task<BenchmarkRun?> GetRunAsync(Guid id,CancellationToken ct);
    Task<BenchmarkRun?> FindBaselineAsync(Guid projectId,Guid definitionId,string environmentFingerprint,string configurationHash,string excludingCommit,CancellationToken ct);
    Task<IReadOnlyList<BenchmarkRun>> ListSucceededRunsAsync(Guid definitionId,int take,CancellationToken ct);
    Task AddMetricsAsync(IEnumerable<BenchmarkMetric> metrics,CancellationToken ct);
    Task<IReadOnlyList<BenchmarkMetric>> ListMetricsAsync(Guid runId,CancellationToken ct);
    Task AddComparisonsAsync(IEnumerable<BenchmarkComparison> comparisons,CancellationToken ct);
    Task<IReadOnlyList<BenchmarkComparison>> ListComparisonsAsync(Guid runId,CancellationToken ct);
}
