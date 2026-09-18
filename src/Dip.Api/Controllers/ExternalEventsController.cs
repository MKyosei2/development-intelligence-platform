// Purpose: Exposes HTTP endpoints for E xt er na lE ve nt sC on tr ol le r.
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

[ApiController][Route("api/v1/external-events")]
public sealed class ExternalEventsController(ExternalIncidentIngestionService service,IOptions<ExternalEventIngestionOptions> options):ControllerBase
{
    [AllowAnonymous][HttpPost("projects/{projectId:guid}/{provider}")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Ingest(Guid projectId,string provider,[FromQuery]string subjectKey,[FromQuery]string? commitSha,[FromQuery]string? buildId,[FromQuery]string? language,[FromBody]JsonElement payload,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(options.Value.IngestKey))return StatusCode(StatusCodes.Status503ServiceUnavailable,new{error="External event ingestion is disabled until ExternalEventIngestion:IngestKey is configured."});
        if(!Request.Headers.TryGetValue("X-DIP-Ingest-Key",out var supplied)||!Equal(options.Value.IngestKey,supplied.ToString()))return Unauthorized();
        if(string.IsNullOrWhiteSpace(subjectKey))return BadRequest(new{error="subjectKey is required."});var lang=LanguageId.TryParse(language,out var parsed)?parsed:LanguageId.Unknown;
        var result=await service.IngestAsync(new(projectId,subjectKey,provider,commitSha,buildId,lang),payload,ct);return Accepted(new{result.IncidentId,result.Fingerprint,result.Severity,result.Category,result.Summary,result.Signals});
    }
    [Microsoft.AspNetCore.Authorization.Authorize][HttpGet("providers")]public IActionResult Providers()=>Ok(service.Providers);
    private static bool Equal(string a,string b){var aa=SHA256.HashData(Encoding.UTF8.GetBytes(a));var bb=SHA256.HashData(Encoding.UTF8.GetBytes(b));return CryptographicOperations.FixedTimeEquals(aa,bb);}
}
