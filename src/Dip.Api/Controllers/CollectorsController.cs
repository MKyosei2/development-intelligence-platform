using Dip.Application.Features.Collectors;
using Dip.Shared.Contracts.Collectors;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/collector-reports")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class CollectorsController : ControllerBase
{
    private const int MaximumPayloadBytes = 10 * 1024 * 1024;

    private readonly CollectorReportService _service;
    private readonly ILogger<CollectorsController> _logger;

    public CollectorsController(
        CollectorReportService service,
        ILogger<CollectorsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(MaximumPayloadBytes)]
    [ProducesResponseType(typeof(CollectorReportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CollectorReportResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<CollectorReportResult>> Submit(
        [FromBody] CollectorReport report,
        CancellationToken cancellationToken)
    {
        if (!Request.HasJsonContentType())
        {
            return Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Unsupported media type",
                detail: "Collector reports must use application/json.");
        }

        var result = await _service.ProcessAsync(
            report,
            cancellationToken);

        _logger.LogInformation(
            "Collector report {ReportId} processed. PipelineRunId={PipelineRunId}, Duplicate={WasDuplicate}",
            report.ReportId,
            result.PipelineRunId,
            result.WasDuplicate);

        Response.Headers["X-Pipeline-Run-Id"] =
            result.PipelineRunId.ToString();

        return result.WasDuplicate
            ? Ok(result)
            : Created(
                $"/api/v1/pipeline-runs/{result.PipelineRunId}",
                result);
    }
}
