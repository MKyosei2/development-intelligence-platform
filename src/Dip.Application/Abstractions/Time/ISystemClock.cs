// Purpose: Defines a focused responsibility within the development intelligence platform.
namespace Dip.Application.Abstractions.Time; public interface ISystemClock { DateTimeOffset UtcNow { get; } }
