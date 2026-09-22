// Purpose: Defines the IReproductionCoordinator application boundary.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public interface IReproductionCoordinator { Task<ReproductionResult> ReproduceAsync(ReproductionScenario scenario, CancellationToken cancellationToken); }
