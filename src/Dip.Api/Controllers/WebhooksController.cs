using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHub;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks/github")]
[EnableRateLimiting("github-webhook")]
[Consumes("application/json")]
public sealed class WebhooksController : ControllerBase
{
    private const int MaximumPayloadBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> SupportedEvents =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ping",
            "push",
            "pull_request",
            "check_run",
            "workflow_run"
        };

    private readonly IGitHubWebhookVerifier _verifier;
    private readonly GitHubWebhookProcessor _processor;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IGitHubWebhookVerifier verifier,
        GitHubWebhookProcessor processor,
        ILogger<WebhooksController> logger)
    {
        _verifier = verifier;
        _processor = processor;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(MaximumPayloadBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (!Request.HasJsonContentType())
        {
            return Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Unsupported media type",
                detail: "GitHub webhook requests must use application/json.");
        }

        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        var deliveryId = Request.Headers["X-GitHub-Delivery"].FirstOrDefault();
        var eventName = Request.Headers["X-GitHub-Event"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(deliveryId) ||
            string.IsNullOrWhiteSpace(eventName))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing GitHub webhook headers",
                detail: "X-Hub-Signature-256, X-GitHub-Delivery and X-GitHub-Event are required.");
        }

        await using var memory = new MemoryStream();
        await Request.Body.CopyToAsync(memory, cancellationToken);

        if (memory.Length == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Empty webhook payload");
        }

        if (memory.Length > MaximumPayloadBytes)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Webhook payload is too large");
        }

        var payload = memory.ToArray();

        if (!_verifier.Verify(payload, signature))
        {
            _logger.LogWarning(
                "Rejected GitHub webhook {DeliveryId}: invalid signature.",
                deliveryId);

            return Unauthorized();
        }

        var payloadText = Encoding.UTF8.GetString(payload);

        try
        {
            using var _ = JsonDocument.Parse(payloadText);
        }
        catch (JsonException)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid JSON payload");
        }

        if (eventName.Equals("ping", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                deliveryId,
                eventName,
                accepted = true
            });
        }

        if (!SupportedEvents.Contains(eventName))
        {
            _logger.LogInformation(
                "Ignored unsupported GitHub event {EventName} ({DeliveryId}).",
                eventName,
                deliveryId);

            return Accepted(new
            {
                deliveryId,
                eventName,
                ignored = true
            });
        }

        var result = await _processor.ProcessAsync(
            deliveryId,
            eventName,
            payloadText,
            cancellationToken);

        return result.WasDuplicate
            ? Ok(result)
            : Accepted(result);
    }
}
