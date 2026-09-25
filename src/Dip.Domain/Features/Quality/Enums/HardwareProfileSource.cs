// Purpose: Defines Hardware Profile Source domain behavior and invariants for quality.
namespace Dip.Domain.Enums;

public enum HardwareProfileSource
{
    LocalCalibration,
    BuiltInProfile,
    ImportedCalibration,
    SimulationTarget,
    RealDevice,
    CommunityAggregate
}
