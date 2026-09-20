// Purpose: Validates and ingests signed GitHub webhook events into the DIP evidence pipeline.
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks/github")]
[AllowAnonymous]
[EnableRateLimiting("github-webhook")]
[Consumes("application/json")]
public sealed class WebhooksController(
    IGitHubWebhookVerifier verifier,
    GitHubWebhookProcessor processor) : ControllerBase
{
    private const int MaxRequestBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> SupportedEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "ping",
        "push",
        "pull_request",
        "check_run",
        "workflow_run"
    };

    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        var delivery = Request.Headers["X-GitHub-Delivery"].FirstOrDefault();
        var eventName = Request.Headers["X-GitHub-Event"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signature)
            || string.IsNullOrWhiteSpace(delivery)
            || string.IsNullOrWhiteSpace(eventName))
        {
            return BadRequest();
        }

        await using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        if (body.Length == 0 || body.Length > MaxRequestBytes)
            return BadRequest();

        var bytes = body.ToArray();
        if (!verifier.Verify(bytes, signature))
            return Unauthorized();

        var text = Encoding.UTF8.GetString(bytes);
        try
        {
            using var _ = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        if (eventName.Equals("ping", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                deliveryId = delivery,
                eventName,
                accepted = true
            });
        }

        if (!SupportedEvents.Contains(eventName))
        {
            return Accepted(new
            {
                deliveryId = delivery,
                eventName,
                ignored = true
            });
        }

        var result = await processor.ProcessAsync(delivery, eventName, text, ct);
        return result.WasDuplicate ? Ok(result) : Accepted(result);
    }
}
