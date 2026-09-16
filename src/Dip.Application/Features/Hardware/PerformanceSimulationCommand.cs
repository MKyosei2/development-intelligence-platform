// Purpose: Defines the PerformanceSimulationCommand application contract.
namespace Dip.Application.Features.Hardware;

public sealed record PerformanceSimulationCommand(Guid ProjectId,Guid HardwareProfileId,string SubjectKey,string WorkspaceRelativePath,string Executable,IReadOnlyList<string>? Arguments,int DurationSeconds=60,bool ThermalSimulation=true);
