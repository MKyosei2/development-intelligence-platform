// Purpose: Verifies expected behavior and invariants for the surrounding feature.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Xunit;

namespace Dip.Domain.Tests.PlatformIntelligence;

public sealed class IncidentTests
{
    [Fact]
    public void ResolvedIncident_Reopens_WhenSameFingerprintOccursAgain()
    {
        var now = DateTimeOffset.UtcNow;
        var incident = new Incident(Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), "qa:test", "Crash", Severity.Warning, now);
        incident.Observe(Severity.Warning, now, false);
        incident.Resolve("abcdef1", now.AddMinutes(1));

        incident.Observe(Severity.Critical, now.AddMinutes(2), true);

        Assert.Equal(IncidentStatus.Reopened, incident.Status);
        Assert.Equal(2, incident.OccurrenceCount);
        Assert.Equal(1, incident.ReproducedCount);
        Assert.Equal(Severity.Critical, incident.Severity);
        Assert.Null(incident.ResolvedAtUtc);
    }
}
