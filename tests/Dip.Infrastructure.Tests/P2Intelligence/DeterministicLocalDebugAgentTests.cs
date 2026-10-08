// Purpose: Locks deterministic local reasoning to evidence-grounded hypotheses and bounded evidence-derived fixes.
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.P2Intelligence;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class DeterministicLocalDebugAgentTests
{
    [Fact]
    public async Task GenerateHypothesesAsync_RanksAsyncContractDrift_WhenQueuedEvidenceIsPresent()
    {
        var id = Guid.NewGuid();
        var agent = new DeterministicLocalDebugAgent();
        var result = await agent.GenerateHypothesesAsync(
            new DebugHypothesisRequest(
                Guid.NewGuid(),
                "gui-e2e",
                "202 Accepted returns Queued but test expected a terminal result",
                JsonSerializer.Serialize(new { evidenceId = id, status = "Queued", transport = "202 Accepted", durableJob = true }),
                "background job polling",
                3),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Hypotheses.Should().NotBeEmpty();
        result.Hypotheses[0].Id.Should().Contain("async.contract-drift");
        result.Hypotheses[0].EvidenceIds.Should().Contain(id);
    }

    [Fact]
    public async Task ProposeAsync_ReturnsOnlyExplicitBoundedEvidenceCandidate()
    {
        var evidenceId = Guid.NewGuid();
        var evidence = JsonSerializer.Serialize(new
        {
            evidenceId,
            localFixCandidate = new
            {
                summary = "Fix deterministic assertion",
                unifiedDiff = "--- a/tests/example.cs\n+++ b/tests/example.cs\n@@ -1 +1 @@\n-old\n+new\n"
            }
        });
        var agent = new DeterministicLocalDebugAgent();
        var proposal = await agent.ProposeAsync(
            new FixProposalRequest(Guid.NewGuid(), "test", "fix", evidence, null),
            CancellationToken.None);

        proposal.Should().NotBeNull();
        proposal!.Provider.Should().Be("DIP.LocalIntelligence");
        proposal.EvidenceIds.Should().Contain(evidenceId);
        proposal.UnifiedDiff.Should().Contain("+++ b/tests/example.cs");
    }

    [Fact]
    public async Task ProposeAsync_DoesNotInventPatchWithoutEvidenceCandidate()
    {
        var agent = new DeterministicLocalDebugAgent();
        var proposal = await agent.ProposeAsync(
            new FixProposalRequest(
                Guid.NewGuid(),
                "test",
                "fix this",
                JsonSerializer.Serialize(new { evidenceId = Guid.NewGuid(), error = "CS0103" }),
                null),
            CancellationToken.None);

        proposal.Should().BeNull();
    }
}
