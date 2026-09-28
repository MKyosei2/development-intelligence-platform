// Purpose: Verifies Pull Request Pipeline Scenario behavior, edge cases, and repository invariants.
using System.Net;
using System.Text.Json;
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class PullRequestPipelineScenario
{
    [Fact]
    public async Task SignedPullRequestWebhook_IsAcceptedAndIdempotent()
    {
        var secret =
            Environment.GetEnvironmentVariable("DIP_VERIFY_GITHUB_WEBHOOK_SECRET")
            ?? "integration-test-secret";
        var deliveryId = Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.Serialize(new
        {
            action = "opened",
            repository = new { full_name = "example/e2e" },
            pull_request = new { number = 42 },
            sender = new { login = "e2e-user" }
        });

        using var api = new DipScenarioClient();
        using var first = await api.SendSignedGitHubWebhookAsync(
            deliveryId, "pull_request", payload, secret);
        first.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var duplicate = await api.SendSignedGitHubWebhookAsync(
            deliveryId, "pull_request", payload, secret);
        duplicate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await duplicate.Content.ReadAsStringAsync()).Should().Contain("wasDuplicate");
    }
}
