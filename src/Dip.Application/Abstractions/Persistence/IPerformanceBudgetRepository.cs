// Purpose: Defines the IPerformanceBudgetRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IPerformanceBudgetRepository
{
    Task AddProfileAsync(PerformanceBudgetProfile profile, CancellationToken ct);
    Task<PerformanceBudgetProfile?> GetProfileAsync(Guid id, CancellationToken ct);
    Task<PerformanceBudgetProfile?> GetProfileByKeyAsync(Guid? projectId, string key, CancellationToken ct);
    Task<IReadOnlyList<PerformanceBudgetProfile>> ListProfilesAsync(Guid? projectId, CancellationToken ct);
    Task AddEvaluationAsync(PerformanceBudgetEvaluation evaluation, CancellationToken ct);
    Task<IReadOnlyList<PerformanceBudgetEvaluation>> ListEvaluationsAsync(Guid projectId, int take, CancellationToken ct);
}
