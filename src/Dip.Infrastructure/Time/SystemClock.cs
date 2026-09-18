// Purpose: Provides the infrastructure capability represented by S ys te mC lo ck.
using Dip.Application.Abstractions.Time; namespace Dip.Infrastructure.Time; public sealed class SystemClock : ISystemClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
