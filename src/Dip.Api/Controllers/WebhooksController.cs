using System.Text;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHub;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks/github")]
public sealed class WebhooksController : ControllerBase
{
    private readonly IGitHubWebhookVerifier _verifier;
    private readonly GitHubWebhookProcessor _processor;

    public WebhooksController(
        IGitHubWebhookVerifier verifier,
        GitHubWebhookProcessor processor)
    {
        _verifier = verifier;
        _processor = processor;
    }

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Receive(
        CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        await using var memory = new MemoryStream();
        await Request.Body.CopyToAsync(memory, cancellationToken);
        var payload = memory.ToArray();

        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (!_verifier.Verify(payload, signature))
            return Unauthorized();

        var deliveryId = Request.Headers["X-GitHub-Delivery"].FirstOrDefault();
        var eventName = Request.Headers["X-GitHub-Event"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(deliveryId)
            || string.IsNullOrWhiteSpace(eventName))
        {
            return BadRequest(new
            {
                error = "Missing GitHub webhook headers."
            });
        }

        var result = await _processor.ProcessAsync(
            deliveryId,
            eventName,
            Encoding.UTF8.GetString(payload),
            cancellationToken);

        if (result.WasDuplicate)
            return Ok(result);

        return Accepted(result);
    }
}
