// Purpose: Defines the IChangeRiskRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IChangeRiskRepository
{
    Task AddAsync(ChangeRiskAssessment assessment, CancellationToken ct);
    Task<ChangeRiskAssessment?> GetLatestAsync(Guid projectId, string? commitSha, CancellationToken ct);
    Task<IReadOnlyList<ChangeRiskAssessment>> ListAsync(Guid projectId, int take, CancellationToken ct);
}
