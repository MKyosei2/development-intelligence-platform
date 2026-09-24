// Purpose: Implements I System Clock responsibilities for time.
namespace Dip.Application.Abstractions.Time; public interface ISystemClock { DateTimeOffset UtcNow { get; } }
