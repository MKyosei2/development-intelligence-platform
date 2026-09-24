// Purpose: Defines H ar dw ar eP ro fi le So ur ce domain behavior and invariants for q ua li ty.
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
