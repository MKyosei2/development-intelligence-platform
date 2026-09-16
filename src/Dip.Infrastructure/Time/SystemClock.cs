// Purpose: Provides the S ys te mC lo ck infrastructure implementation.
using Dip.Application.Abstractions.Time; namespace Dip.Infrastructure.Time; public sealed class SystemClock : ISystemClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
