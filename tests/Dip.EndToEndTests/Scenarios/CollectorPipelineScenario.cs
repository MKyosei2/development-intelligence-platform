using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class CollectorPipelineScenario
{
    [Fact(Skip = "Enable after the repository root solution and migrations are wired.")]
    public async Task CollectorReport_ProducesQualityGateResult()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("DIP_API_URL") ?? "http://localhost:7001")
        };

        var projectResponse = await client.PostAsJsonAsync("/api/projects", new
        {
            name = "E2E Project",
            repositoryUrl = "https://github.com/example/e2e",
            defaultBranch = "develop",
            releaseDateUtc = (DateTimeOffset?)null
        });

        projectResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
