// Purpose: Defines the IAdaptiveQaPlanRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IAdaptiveQaPlanRepository
{
    Task AddAsync(AdaptiveQaPlanSnapshot plan,CancellationToken ct);
    Task<AdaptiveQaPlanSnapshot?> GetLatestAsync(Guid projectId,CancellationToken ct);
}
