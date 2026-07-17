using Dip.Application.Features.Collectors;
using Dip.Shared.Contracts.Collectors;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/collector-reports")]
public sealed class CollectorsController:ControllerBase
{
    private readonly CollectorReportService _service;
    public CollectorsController(CollectorReportService service)=>_service=service;
    [HttpPost]
    [RequestSizeLimit(10*1024*1024)]
    public async Task<ActionResult<CollectorReportResult>> Submit(CollectorReport report,CancellationToken ct)
    {
        var result=await _service.ProcessAsync(report,ct);
        return result.WasDuplicate?Ok(result):Created($"/api/v1/pipeline-runs/{result.PipelineRunId}",result);
    }
}
