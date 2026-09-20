// Purpose: Authenticates external event ingestion and forwards normalized project incidents to the application layer.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.ExternalEvents;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.ExternalEvents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/external-events")]
public sealed class ExternalEventsController(
    ExternalIncidentIngestionService service,
    IOptions<ExternalEventIngestionOptions> options) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("projects/{projectId:guid}/{provider}")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Ingest(
        Guid projectId,
        string provider,
        [FromQuery] string subjectKey,
        [FromQuery] string? commitSha,
        [FromQuery] string? buildId,
        [FromQuery] string? language,
        [FromBody] JsonElement payload,
        CancellationToken ct)
    {
        var configuredKey = options.Value.IngestKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "External event ingestion is disabled until ExternalEventIngestion:IngestKey is configured."
                });
        }

        if (!Request.Headers.TryGetValue("X-DIP-Ingest-Key", out var supplied)
            || !FixedTimeEqual(configuredKey, supplied.ToString()))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(subjectKey))
            return BadRequest(new { error = "subjectKey is required." });

        var parsedLanguage = LanguageId.TryParse(language, out var parsed)
            ? parsed
            : LanguageId.Unknown;

        var result = await service.IngestAsync(
            new(projectId, subjectKey, provider, commitSha, buildId, parsedLanguage),
            payload,
            ct);

        return Accepted(new
        {
            result.IncidentId,
            result.Fingerprint,
            result.Severity,
            result.Category,
            result.Summary,
            result.Signals
        });
    }

    [Authorize]
    [HttpGet("providers")]
    public IActionResult Providers() => Ok(service.Providers);

    private static bool FixedTimeEqual(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
