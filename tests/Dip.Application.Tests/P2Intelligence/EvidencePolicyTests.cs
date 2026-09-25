// Purpose: Defines Evidence Policy Tests responsibilities for this repository area.
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.P2Intelligence;

public sealed class EvidencePolicyTests
{
    [Fact]
    public void Freshness_DecaysSimulationFasterThanMeasuredRuntime()
    {
        var now = DateTimeOffset.UtcNow;
        var simulation = Evidence("hardware.simulation", EvidenceSourceType.Performance, EvidenceOutcome.Succeeded, EvidenceConfidence.High, now.AddDays(-5));
        var runtime = Evidence("runtime.launch", EvidenceSourceType.Runtime, EvidenceOutcome.Succeeded, EvidenceConfidence.High, now.AddDays(-5));

        EvidenceFreshnessPolicy.Freshness(simulation, now).Should().Be("Aging");
        EvidenceFreshnessPolicy.Freshness(runtime, now).Should().Be("Fresh");
    }

    [Fact]
    public void ConflictPolicy_PrefersFreshVerifiedRuntimeOverOlderSimulation()
    {
        var now = DateTimeOffset.UtcNow;
        var simulated = Evidence("runtime.launch", EvidenceSourceType.Performance, EvidenceOutcome.Succeeded, EvidenceConfidence.High, now.AddHours(-2));
        var measured = Evidence("runtime.launch", EvidenceSourceType.Runtime, EvidenceOutcome.Failed, EvidenceConfidence.Verified, now.AddHours(-1));

        var group = EvidenceConflictPolicy.Resolve([simulated, measured], now).Single();

        group.Authoritative.Id.Should().Be(measured.Id);
        group.Conflicts.Should().ContainSingle(x => x.Id == simulated.Id);
    }

    private static EvidenceRecord Evidence(string kind, EvidenceSourceType source, EvidenceOutcome outcome, EvidenceConfidence confidence, DateTimeOffset at) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, "project", source, kind, outcome, "title", "summary", confidence, null, null, at, at);
}
