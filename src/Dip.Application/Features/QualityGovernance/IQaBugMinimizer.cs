// Purpose: Defines the IQaBugMinimizer application boundary.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public interface IQaBugMinimizer { Task<BugMinimizationDto> MinimizeAsync(BugMinimizationCommand command, CancellationToken ct); }
