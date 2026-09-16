// Purpose: Defines the IDecisionTraceRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IDecisionTraceRepository
{
    Task AddAsync(DecisionTrace trace,CancellationToken ct);
    Task<IReadOnlyList<DecisionTrace>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
}
