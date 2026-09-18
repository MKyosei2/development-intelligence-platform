// Purpose: Exposes HTTP endpoints for C ol la bo ra ti on Ev ID en ce We bh oo ks Co nt ro ll er.
using System.Globalization;
using System.Text.Json;
using Dip.Application.Features.ProjectIntelligence;
using Dip.Infrastructure.ProjectIntelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-intelligence/hooks")]
public sealed class CollaborationEvidenceWebhooksController(CollaborationWebhookVerifier verifier,ProjectSourceIngestionService ingestion):ControllerBase
{
    [AllowAnonymous]
    [RequestSizeLimit(1024*1024)]
    [HttpPost("slack/{projectId:guid}")]
    public async Task<IActionResult> Slack(Guid projectId,CancellationToken ct)
    {
        using var reader=new StreamReader(Request.Body);var raw=await reader.ReadToEndAsync(ct);var timestamp=Request.Headers["X-Slack-Request-Timestamp"].ToString();var signature=Request.Headers["X-Slack-Signature"].ToString();if(!verifier.VerifySlack(timestamp,signature,raw,DateTimeOffset.UtcNow))return Unauthorized();using var doc=JsonDocument.Parse(raw);var root=doc.RootElement;if(root.TryGetProperty("type",out var type)&&type.GetString()=="url_verification")return Ok(new{challenge=root.GetProperty("challenge").GetString()});if(!root.TryGetProperty("event",out var e)||e.ValueKind!=JsonValueKind.Object)return Ok(new{accepted=false,reason="no-event"});if(!e.TryGetProperty("type",out var et)||et.GetString()!="message")return Ok(new{accepted=false,reason="unsupported-event"});if(e.TryGetProperty("bot_id",out _))return Ok(new{accepted=false,reason="bot-message"});var text=e.TryGetProperty("text",out var tx)?tx.GetString()??"":"";if(string.IsNullOrWhiteSpace(text))return Ok(new{accepted=false,reason="empty-message"});var channel=e.TryGetProperty("channel",out var ch)?ch.GetString():null;var author=e.TryGetProperty("user",out var u)?u.GetString():null;if(!verifier.IsSlackChannelAllowed(projectId,channel))return Unauthorized();var eventId=root.TryGetProperty("event_id",out var eid)?eid.GetString():null;var slackTs=e.TryGetProperty("ts",out var tv)?tv.GetString():null;var messageId=!string.IsNullOrWhiteSpace(slackTs)?slackTs!:!string.IsNullOrWhiteSpace(eventId)?eventId!:Guid.NewGuid().ToString("N");var observed=SlackTime(messageId);var externalId=$"{channel??"unknown"}:{messageId}";var references=SlackReferences(e);var result=await ingestion.IngestCollaborationAsync(new(projectId,"Slack",externalId,channel,author,text,observed,null,references,messageId),ct);return Ok(result);
    }

    [AllowAnonymous]
    [RequestSizeLimit(1024*1024)]
    [HttpPost("teams/{projectId:guid}")]
    public async Task<IActionResult> Teams(Guid projectId,[FromBody]TeamsEvidenceWebhook payload,CancellationToken ct)
    {
        if(!verifier.VerifyTeams(projectId,Request.Headers["X-DIP-Ingest-Key"].ToString()))return Unauthorized();if(string.IsNullOrWhiteSpace(payload.ExternalId)||string.IsNullOrWhiteSpace(payload.Text))return BadRequest(new{error="externalId and text are required"});var result=await ingestion.IngestCollaborationAsync(new(projectId,"Teams",payload.ExternalId,payload.Channel,payload.Author,payload.Text,payload.ObservedAtUtc??DateTimeOffset.UtcNow,payload.Url,payload.References,payload.Revision),ct);return Ok(result);
    }

    private static IReadOnlyList<string> SlackReferences(JsonElement message){var refs=ProjectSourceIngestionService.ExtractReferences(message.TryGetProperty("text",out var t)?t.GetString()??"":"").ToList();if(message.TryGetProperty("files",out var files)&&files.ValueKind==JsonValueKind.Array)foreach(var f in files.EnumerateArray()){if(f.TryGetProperty("name",out var n)&&n.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(n.GetString()))refs.Add("file:"+n.GetString());if(f.TryGetProperty("permalink",out var p)&&p.ValueKind==JsonValueKind.String)refs.Add(p.GetString()!);else if(f.TryGetProperty("url_private",out var u)&&u.ValueKind==JsonValueKind.String)refs.Add(u.GetString()!);}return refs.Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray();}
    private static DateTimeOffset SlackTime(string ts){var p=ts.Split('.',2);return long.TryParse(p[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out var sec)?DateTimeOffset.FromUnixTimeSeconds(sec):DateTimeOffset.UtcNow;}
}

public sealed record TeamsEvidenceWebhook(string ExternalId,string? Channel,string? Author,string Text,DateTimeOffset? ObservedAtUtc,string? Url,IReadOnlyList<string>? References,string? Revision);
