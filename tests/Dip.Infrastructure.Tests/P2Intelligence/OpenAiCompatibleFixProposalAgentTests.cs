// Purpose: Verifies OpenAI-compatible QA calls produce grounded structured hypotheses without a live external service.
using System.Net;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.P2Intelligence;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class OpenAiCompatibleFixProposalAgentTests
{
    [Fact]
    public async Task Generates_grounded_hypothesis_from_structured_provider_response()
    {
        var evidenceId = Guid.NewGuid();
        var modelContent = $$"""
{"hypotheses":[{"id":"AI-01","title":"Null state","explanation":"The evidence contains a null dereference.","confidence":0.91,"evidenceIds":["{{evidenceId}}"],"suggestedExperiments":["Reproduce","RegressionTest"]}]}
""";
        var providerPayload = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = modelContent }, finish_reason = "stop" } },
            usage = new { prompt_tokens = 100, completion_tokens = 40 }
        });
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(providerPayload, Encoding.UTF8, "application/json")
        }));
        var options = Options.Create(new GenerativeQaOptions
        {
            Enabled = true,
            Endpoint = "https://example.test/v1/chat/completions",
            Model = "qa-model",
            ApiKey = "test-key",
            MaxRetries = 0
        });
        var sut = new OpenAiCompatibleFixProposalAgent(
            http,
            options,
            new GenerativeQaRedactor(),
            new GenerativeQaResponseParser(options));

        var result = await sut.GenerateHypothesesAsync(
            new DebugHypothesisRequest(
                Guid.NewGuid(),
                "runtime-null",
                "Find the cause",
                $$"""{"id":"{{evidenceId}}","error":"NullReferenceException"}""",
                "src/A.cs",
                3),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Provider.Should().Be("OpenAI-Compatible");
        result.Model.Should().Be("qa-model");
        result.Hypotheses.Should().ContainSingle();
        result.Hypotheses[0].EvidenceIds.Should().ContainSingle().Which.Should().Be(evidenceId);
        result.Ai.Should().NotBeNull();
        result.Ai!.InputTokens.Should().Be(100);
    }

    [Fact]
    public async Task Rejects_provider_hypothesis_that_invents_evidence()
    {
        var supplied = Guid.NewGuid();
        var invented = Guid.NewGuid();
        var modelContent = $$"""
{"hypotheses":[{"id":"AI-01","title":"Guess","explanation":"Unsupported","confidence":0.9,"evidenceIds":["{{invented}}"],"suggestedExperiments":["Reproduce"]}]}
""";
        var providerPayload = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = modelContent }, finish_reason = "stop" } }
        });
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(providerPayload, Encoding.UTF8, "application/json")
        }));
        var options = Options.Create(new GenerativeQaOptions
        {
            Enabled = true,
            Endpoint = "https://example.test/v1/chat/completions",
            Model = "qa-model",
            ApiKey = "test-key",
            MaxRetries = 0
        });
        var sut = new OpenAiCompatibleFixProposalAgent(http, options, new GenerativeQaRedactor(), new GenerativeQaResponseParser(options));

        var result = await sut.GenerateHypothesesAsync(
            new DebugHypothesisRequest(Guid.NewGuid(), "x", "q", $$"""{"id":"{{supplied}}"}""", null, 3),
            CancellationToken.None);

        result.Should().BeNull();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
