// Purpose: Defines the IQualityIssueRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IQualityIssueRepository
{
    Task<QualityIssue?> GetAsync(Guid id,CancellationToken ct);
    Task<QualityIssue?> FindOpenByFingerprintAsync(Guid projectId,string fingerprint,CancellationToken ct);
    Task<IReadOnlyList<QualityIssue>> ListByProjectAsync(Guid projectId,int take,CancellationToken ct);
    async Task<IReadOnlyList<QualityIssue>> ListByPipelineRunsAsync(Guid projectId,IReadOnlyCollection<Guid> pipelineRunIds,CancellationToken ct)
    {
        if(pipelineRunIds.Count==0)return Array.Empty<QualityIssue>();
        var ids=pipelineRunIds.ToHashSet();
        return (await ListByProjectAsync(projectId,500,ct))
            .Where(x=>ids.Contains(x.PipelineRunId)||ids.Contains(x.FirstSeenPipelineRunId)||ids.Contains(x.LastSeenPipelineRunId))
            .ToArray();
    }
    Task AddAsync(QualityIssue issue,CancellationToken ct);
}
