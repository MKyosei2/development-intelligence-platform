// Purpose: Exposes HTTP endpoints for this API responsibility.
using Dip.Api.Authorization;
using Dip.Application.Features.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController]
[Route("api/v1/performance-budgets")]
public sealed class PerformanceBudgetsController(PerformanceBudgetService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)][HttpPost] public Task<PerformanceBudgetProfileDto> Create([FromBody] CreatePerformanceBudgetCommand command, CancellationToken ct) => service.CreateAsync(command, ct);
    [Authorize(Policy = Policies.ProjectRead)][HttpGet] public Task<IReadOnlyList<PerformanceBudgetProfileDto>> List([FromQuery] Guid? projectId, CancellationToken ct) => service.ListAsync(projectId, ct);
    [Authorize(Policy = Policies.ProjectWrite)][HttpPost("{profileId:guid}/evaluate/{projectId:guid}")] public Task<PerformanceBudgetEvaluationDto> Evaluate(Guid profileId, Guid projectId, [FromBody] IReadOnlyList<PerformanceMetricInput> metrics, CancellationToken ct) => service.EvaluateAsync(projectId, profileId, metrics, null, null, ct);
}
