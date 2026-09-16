// Purpose: Defines the IHardwareProfileRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IHardwareProfileRepository
{
    Task AddAsync(HardwareProfile profile,CancellationToken ct);
    Task<HardwareProfile?> GetAsync(Guid id,CancellationToken ct);
    Task<HardwareProfile?> FindAsync(string name,string platform,CancellationToken ct);
    Task<IReadOnlyList<HardwareProfile>> ListAsync(CancellationToken ct);
}
