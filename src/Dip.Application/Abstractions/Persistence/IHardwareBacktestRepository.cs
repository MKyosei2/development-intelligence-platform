// Purpose: Defines the IHardwareBacktestRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IHardwareBacktestRepository
{
    Task AddAsync(HardwareBacktest backtest,CancellationToken ct);
    Task<IReadOnlyList<HardwareBacktest>> ListAsync(Guid projectId,Guid? hardwareProfileId,int take,CancellationToken ct);
}
