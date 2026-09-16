// Purpose: Defines the PerformanceSimulationDto application contract.
namespace Dip.Application.Features.Hardware;

public sealed record PerformanceSimulationDto(Guid Id,string Status,decimal ConfidencePercent,string ResultJson,string ArtifactReference);
