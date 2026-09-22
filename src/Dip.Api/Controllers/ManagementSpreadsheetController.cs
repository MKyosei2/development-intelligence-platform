// Purpose: Exposes management workbook import, export, schema inference, and Google Sheets synchronization endpoints.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.Assets;
using Dip.Application.Features.ChangeImpact;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Decisions;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.FeatureControl;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Management;
using Dip.Application.Features.Milestones;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.Review;
using Dip.Application.Features.Scenarios;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectControlDecisionBriefDto = Dip.Application.Features.Decisions.DecisionBriefDto;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-control")]
public sealed class ManagementSpreadsheetController(ManagementSpreadsheetService spreadsheets, SpreadsheetSchemaInferenceService spreadsheetInference) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/management.xlsx")]
    public async Task<IActionResult> ExportManagementWorkbook(Guid projectId, CancellationToken ct)
    { var bytes = await spreadsheets.ExportXlsxAsync(projectId, ct); return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"dip-management-{projectId:N}.xlsx"); }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/import")]
    [RequestSizeLimit(25_000_000)]
    public async Task<SpreadsheetImportResult> ImportManagementWorkbook(Guid projectId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("An .xlsx file is required.", nameof(file));
        await using var stream = file.OpenReadStream();
        return await spreadsheets.ImportXlsxAsync(projectId, stream, ct);
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/analyze")]
    [RequestSizeLimit(25_000_000)]
    public async Task<SpreadsheetSchemaAnalysisDto> AnalyzeManagementWorkbook(Guid projectId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("An .xlsx file is required.", nameof(file));
        await using var stream = file.OpenReadStream();
        return await spreadsheetInference.AnalyzeXlsxAsync(projectId, stream, ct);
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/import-flexible")]
    [RequestSizeLimit(25_000_000)]
    public async Task<FlexibleSpreadsheetImportResult> ImportFlexibleManagementWorkbook(Guid projectId, IFormFile file, [FromQuery] string? domainId = null, [FromQuery] bool dryRun = true, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("An .xlsx file is required.", nameof(file));
        await using var stream = file.OpenReadStream();
        return await spreadsheetInference.ImportFlexibleXlsxAsync(projectId, stream, domainId, dryRun, ct);
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("google-sheets/sync")]
    public Task<GoogleSheetsSyncResult> SyncGoogleSheets([FromBody] GoogleSheetsSyncCommand command, CancellationToken ct)
            => spreadsheets.SyncGoogleAsync(command, ct);
}
