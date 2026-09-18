// Purpose: Defines the allowed H ar dw ar eP ro fi le So ur ce states used by the DIP domain.
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
