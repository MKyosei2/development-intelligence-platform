// Purpose: Defines the application boundary represented by I Sy st em Cl oc k.
namespace Dip.Application.Abstractions.Time; public interface ISystemClock { DateTimeOffset UtcNow { get; } }
