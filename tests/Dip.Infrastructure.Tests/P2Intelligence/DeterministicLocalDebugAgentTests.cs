// Purpose: Verifies D et er mi ni st ic Lo ca lD eb ug Ag en t behavior, edge cases, and invariants for p2 in te ll ig en ce.
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.P2Intelligence;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class DeterministicLocalDebugAgentTests
{
    [Fact]
    public async Task Generates_grounded_http_500_hypothesis_without_external_provider()
    {
        var evidenceId = Guid.NewGuid();
        var sut = new DeterministicLocalDebugAgent();

        var result = await sut.GenerateHypothesesAsync(
            new DebugHypothesisRequest(
                Guid.NewGuid(),
                "project-sources",
                "Plan & Sources returns HTTP 500 Internal Server Error",
                $$"""{"id":"{{evidenceId}}","outcome":"Failed","summary":"HTTP 500 Internal Server Error"}""",
                null,
                3),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Provider.Should().Be("DIP.LocalIntelligence");
        result.Model.Should().Be("deterministic-rules-v1");
        result.Hypotheses.Should().NotBeEmpty();
        result.Hypotheses[0].EvidenceIds.Should().Contain(evidenceId);
    }

    [Fact]
    public async Task Fix_proposal_fails_closed_when_no_provable_local_patch_rule_exists()
    {
        var sut = new DeterministicLocalDebugAgent();
        var result = await sut.ProposeAsync(
            new FixProposalRequest(Guid.NewGuid(), "subject", "question", "{}", null),
            CancellationToken.None);

        result.Should().BeNull();
    }
}
