// Purpose: Defines the I Sy st em Cl oc k application boundary.
namespace Dip.Application.Abstractions.Time; public interface ISystemClock { DateTimeOffset UtcNow { get; } }
