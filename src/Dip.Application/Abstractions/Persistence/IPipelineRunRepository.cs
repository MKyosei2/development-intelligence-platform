// Purpose: Defines the IPipelineRunRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IPipelineRunRepository
{
    Task<PipelineRun?> GetByIdAsync(Guid id,CancellationToken ct);
    Task<PipelineRun?> FindByExternalRunAsync(Guid projectId,string externalRunId,string collectorType,int attemptNumber,CancellationToken ct);
    Task<IReadOnlyList<PipelineRun>> ListByProjectAsync(Guid projectId,int take,CancellationToken ct);
    async Task<IReadOnlyList<PipelineRun>> ListByCommitShasAsync(Guid projectId,IReadOnlyCollection<string> commitShas,CancellationToken ct)
    {
        if(commitShas.Count==0)return Array.Empty<PipelineRun>();
        var revisions=commitShas.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (await ListByProjectAsync(projectId,500,ct)).Where(x=>revisions.Contains(x.CommitSha.Value)).ToArray();
    }
    Task AddAsync(PipelineRun run,CancellationToken ct);
}
