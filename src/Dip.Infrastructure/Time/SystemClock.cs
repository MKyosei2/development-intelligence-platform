// Purpose: Implements S ys te mC lo ck infrastructure integration for t im e.
using Dip.Application.Abstractions.Time;
namespace Dip.Infrastructure.Time; public sealed class SystemClock : ISystemClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
