// Purpose: Defines the IHardwareCommunityRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IHardwareCommunityRepository
{
    Task AddAsync(HardwareCommunitySample sample,CancellationToken ct);
    Task<IReadOnlyList<HardwareCommunitySample>> ListAsync(string profileKey,string platform,int take,CancellationToken ct);
}
