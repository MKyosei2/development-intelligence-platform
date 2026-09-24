// Purpose: Verifies Generic JSON Collector API submission handling for success, HTTP failure, malformed JSON, and empty responses.
using System.Net;
using System.Text;
using GenericJsonCollector.Clients;

namespace GenericJsonCollector.Tests;

public sealed class DipApiClientTests
{
    [Fact]
    public async Task SubmitAsync_ReturnsNormalizedSuccessResult()
    {
        var pipelineRunId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        using var http = CreateClient(
            HttpStatusCode.OK,
            $$"""
            {
              "pipelineRunId": "{{pipelineRunId}}",
              "pipelineStatus": "Completed",
              "wasDuplicate": false,
              "qualityGate": { "status": "Passed" }
            }
            """);
        var client = new DipApiClient(http, "test-key");

        var result = await client.SubmitAsync(
            CollectorReportTestData.CreateValid(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(pipelineRunId, result.PipelineRunId);
        Assert.Equal("Completed", result.PipelineStatus);
        Assert.False(result.WasDuplicate);
        Assert.Equal("Passed", result.QualityGateStatus);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SubmitAsync_ReturnsApiDetailForHttpError()
    {
        using var http = CreateClient(
            HttpStatusCode.BadRequest,
            "{\"detail\":\"report rejected\"}");
        var client = new DipApiClient(http);

        var result = await client.SubmitAsync(
            CollectorReportTestData.CreateValid(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("report rejected", result.ErrorMessage);
    }

    [Fact]
    public async Task SubmitAsync_ReportsMalformedSuccessJsonWithoutThrowing()
    {
        using var http = CreateClient(HttpStatusCode.OK, "{not-json");
        var client = new DipApiClient(http);

        var result = await client.SubmitAsync(
            CollectorReportTestData.CreateValid(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("invalid JSON", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAsync_ReportsEmptySuccessResponseWithoutThrowing()
    {
        using var http = CreateClient(HttpStatusCode.OK, string.Empty);
        var client = new DipApiClient(http);

        var result = await client.SubmitAsync(
            CollectorReportTestData.CreateValid(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("The API returned an empty response.", result.ErrorMessage);
    }

    private static HttpClient CreateClient(HttpStatusCode status, string body)
    {
        var handler = new StubHandler((request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/collector-reports", request.RequestUri?.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dip.example/")
        };
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => responder(request, cancellationToken);
    }
}
