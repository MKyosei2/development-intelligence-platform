// Purpose: Verifies behavior and invariants covered by I nt eg ra ti on Co nn ec ti on Te st s.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.EvidenceIntegration;

public sealed class IntegrationConnectionTests
{
    [Fact]
    public void Disable_is_fail_closed_and_reenable_requires_retest()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new IntegrationConnection(
            Guid.NewGuid(),
            "GitHub",
            "Personal GitHub",
            IntegrationConnectionMode.GitHubPat,
            "https://api.github.com/",
            "local-secret://0123456789abcdef0123456789abcdef",
            now);

        item.RecordHealth(
            ConnectionHealthState.Healthy,
            "ok",
            "owner",
            now);

        item.SetEnabled(false, now.AddMinutes(1));
        item.HealthState.Should().Be(ConnectionHealthState.Disabled);

        item.SetEnabled(true, now.AddMinutes(2));
        item.HealthState.Should().Be(ConnectionHealthState.Unknown);
    }
}
