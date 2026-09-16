// Purpose: Defines the IReproductionAttemptRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IReproductionAttemptRepository
{
    Task AddAsync(ReproductionAttempt attempt,CancellationToken ct);
    Task<IReadOnlyList<ReproductionAttempt>> ListByFindingAsync(Guid findingId,CancellationToken ct);
}
