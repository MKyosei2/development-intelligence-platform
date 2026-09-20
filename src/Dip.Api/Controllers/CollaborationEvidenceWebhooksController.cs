// Purpose: Validates Slack and Teams collaboration webhooks before ingesting them as project evidence.
using System.Globalization;
using System.Text.Json;
using Dip.Application.Features.ProjectIntelligence;
using Dip.Infrastructure.ProjectIntelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-intelligence/hooks")]
public sealed class CollaborationEvidenceWebhooksController(
    CollaborationWebhookVerifier verifier,
    ProjectSourceIngestionService ingestion) : ControllerBase
{
    [AllowAnonymous]
    [RequestSizeLimit(1024 * 1024)]
    [HttpPost("slack/{projectId:guid}")]
    public async Task<IActionResult> Slack(Guid projectId, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(ct);
        var timestamp = Request.Headers["X-Slack-Request-Timestamp"].ToString();
        var signature = Request.Headers["X-Slack-Signature"].ToString();

        if (!verifier.VerifySlack(timestamp, signature, raw, DateTimeOffset.UtcNow))
            return Unauthorized();

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        if (root.TryGetProperty("type", out var type)
            && type.GetString() == "url_verification")
        {
            return Ok(new { challenge = root.GetProperty("challenge").GetString() });
        }

        if (!root.TryGetProperty("event", out var message)
            || message.ValueKind != JsonValueKind.Object)
        {
            return Ok(new { accepted = false, reason = "no-event" });
        }

        if (!message.TryGetProperty("type", out var eventType)
            || eventType.GetString() != "message")
        {
            return Ok(new { accepted = false, reason = "unsupported-event" });
        }

        if (message.TryGetProperty("bot_id", out _))
            return Ok(new { accepted = false, reason = "bot-message" });

        var text = message.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return Ok(new { accepted = false, reason = "empty-message" });

        var channel = message.TryGetProperty("channel", out var channelElement)
            ? channelElement.GetString()
            : null;
        var author = message.TryGetProperty("user", out var userElement)
            ? userElement.GetString()
            : null;

        if (!verifier.IsSlackChannelAllowed(projectId, channel))
            return Unauthorized();

        var eventId = root.TryGetProperty("event_id", out var eventIdElement)
            ? eventIdElement.GetString()
            : null;
        var slackTimestamp = message.TryGetProperty("ts", out var timestampElement)
            ? timestampElement.GetString()
            : null;
        var messageId = !string.IsNullOrWhiteSpace(slackTimestamp)
            ? slackTimestamp!
            : !string.IsNullOrWhiteSpace(eventId)
                ? eventId!
                : Guid.NewGuid().ToString("N");

        var observedAt = SlackTime(messageId);
        var externalId = $"{channel ?? "unknown"}:{messageId}";
        var references = SlackReferences(message);

        var result = await ingestion.IngestCollaborationAsync(
            new(
                projectId,
                "Slack",
                externalId,
                channel,
                author,
                text,
                observedAt,
                null,
                references,
                messageId),
            ct);

        return Ok(result);
    }

    [AllowAnonymous]
    [RequestSizeLimit(1024 * 1024)]
    [HttpPost("teams/{projectId:guid}")]
    public async Task<IActionResult> Teams(
        Guid projectId,
        [FromBody] TeamsEvidenceWebhook payload,
        CancellationToken ct)
    {
        if (!verifier.VerifyTeams(projectId, Request.Headers["X-DIP-Ingest-Key"].ToString()))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(payload.ExternalId)
            || string.IsNullOrWhiteSpace(payload.Text))
        {
            return BadRequest(new { error = "externalId and text are required" });
        }

        var result = await ingestion.IngestCollaborationAsync(
            new(
                projectId,
                "Teams",
                payload.ExternalId,
                payload.Channel,
                payload.Author,
                payload.Text,
                payload.ObservedAtUtc ?? DateTimeOffset.UtcNow,
                payload.Url,
                payload.References,
                payload.Revision),
            ct);

        return Ok(result);
    }

    private static IReadOnlyList<string> SlackReferences(JsonElement message)
    {
        var text = message.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;
        var references = ProjectSourceIngestionService.ExtractReferences(text).ToList();

        if (message.TryGetProperty("files", out var files)
            && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in files.EnumerateArray())
            {
                if (file.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(name.GetString()))
                {
                    references.Add("file:" + name.GetString());
                }

                if (file.TryGetProperty("permalink", out var permalink)
                    && permalink.ValueKind == JsonValueKind.String)
                {
                    references.Add(permalink.GetString()!);
                }
                else if (file.TryGetProperty("url_private", out var privateUrl)
                         && privateUrl.ValueKind == JsonValueKind.String)
                {
                    references.Add(privateUrl.GetString()!);
                }
            }
        }

        return references
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();
    }

    private static DateTimeOffset SlackTime(string timestamp)
    {
        var parts = timestamp.Split('.', 2);
        return long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : DateTimeOffset.UtcNow;
    }
}

public sealed record TeamsEvidenceWebhook(
    string ExternalId,
    string? Channel,
    string? Author,
    string Text,
    DateTimeOffset? ObservedAtUtc,
    string? Url,
    IReadOnlyList<string>? References,
    string? Revision);
