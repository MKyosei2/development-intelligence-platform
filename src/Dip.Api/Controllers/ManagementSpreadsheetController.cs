// Purpose: Exposes management workbook import, export, schema inference, and Google Sheets synchronization endpoints.
using Dip.Api.Authorization;
using Dip.Application.Features.Management;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-control")]
public sealed class ManagementSpreadsheetController(
    ManagementSpreadsheetService spreadsheets,
    SpreadsheetSchemaInferenceService spreadsheetInference) : ControllerBase
{
    private const long MaxWorkbookBytes = 25_000_000;
    private const long MaxMultipartRequestBytes = 27_000_000;
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/management.xlsx")]
    public async Task<IActionResult> ExportManagementWorkbook(
        Guid projectId,
        CancellationToken ct)
    {
        var bytes = await spreadsheets.ExportXlsxAsync(projectId, ct);
        return File(
            bytes,
            XlsxContentType,
            $"dip-management-{projectId:N}.xlsx");
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/import")]
    [RequestSizeLimit(MaxMultipartRequestBytes)]
    public Task<ActionResult<SpreadsheetImportResult>> ImportManagementWorkbook(
        Guid projectId,
        IFormFile? file,
        CancellationToken ct) =>
        ExecuteWorkbookAsync(
            file,
            (stream, token) => spreadsheets.ImportXlsxAsync(projectId, stream, token),
            "Management workbook import",
            ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/analyze")]
    [RequestSizeLimit(MaxMultipartRequestBytes)]
    public Task<ActionResult<SpreadsheetSchemaAnalysisDto>> AnalyzeManagementWorkbook(
        Guid projectId,
        IFormFile? file,
        CancellationToken ct) =>
        ExecuteWorkbookAsync(
            file,
            (stream, token) => spreadsheetInference.AnalyzeXlsxAsync(projectId, stream, token),
            "Spreadsheet schema analysis",
            ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/import-flexible")]
    [RequestSizeLimit(MaxMultipartRequestBytes)]
    public Task<ActionResult<FlexibleSpreadsheetImportResult>> ImportFlexibleManagementWorkbook(
        Guid projectId,
        IFormFile? file,
        [FromQuery] string? domainId = null,
        [FromQuery] bool dryRun = true,
        CancellationToken ct = default) =>
        ExecuteWorkbookAsync(
            file,
            (stream, token) => spreadsheetInference.ImportFlexibleXlsxAsync(
                projectId,
                stream,
                domainId,
                dryRun,
                token),
            dryRun
                ? "Flexible spreadsheet dry run"
                : "Flexible spreadsheet import",
            ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("google-sheets/sync")]
    public Task<GoogleSheetsSyncResult> SyncGoogleSheets(
        [FromBody] GoogleSheetsSyncCommand command,
        CancellationToken ct) =>
        spreadsheets.SyncGoogleAsync(command, ct);

    private async Task<ActionResult<T>> ExecuteWorkbookAsync<T>(
        IFormFile? file,
        Func<Stream, CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken ct)
    {
        var validation = ValidateWorkbook(file);
        if (validation is not null)
            return validation;

        try
        {
            await using var stream = file!.OpenReadStream();
            var result = await operation(stream, ct);
            return Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or FormatException
            or ArgumentException)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = $"{operationName} could not process the workbook.",
                Detail = exception.Message,
                Type = "https://httpstatuses.com/422"
            });
        }
    }

    private ActionResult? ValidateWorkbook(IFormFile? file)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "An XLSX workbook is required.",
                Detail = "Upload a non-empty .xlsx file in the multipart 'file' field."
            });
        }

        if (file.Length > MaxWorkbookBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new ProblemDetails
                {
                    Status = StatusCodes.Status413PayloadTooLarge,
                    Title = "Workbook is too large.",
                    Detail = $"The maximum workbook size is {MaxWorkbookBytes} bytes."
                });
        }

        if (!Path.GetExtension(file.FileName)
                .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unsupported workbook type.",
                Detail = "Only .xlsx workbooks are accepted."
            });
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType)
            && !file.ContentType.Equals(XlsxContentType, StringComparison.OrdinalIgnoreCase)
            && !file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unsupported workbook content type.",
                Detail = $"Received '{file.ContentType}'. Expected '{XlsxContentType}'."
            });
        }

        return null;
    }
}
