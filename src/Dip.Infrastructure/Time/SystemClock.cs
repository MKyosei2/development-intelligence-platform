// Purpose: Implements System Clock infrastructure behavior for this repository area.
using Dip.Application.Abstractions.Time;
namespace Dip.Infrastructure.Time; public sealed class SystemClock : ISystemClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
