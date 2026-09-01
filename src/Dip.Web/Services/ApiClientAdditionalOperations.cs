using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Dip.Application.Features.Management;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Web.Services;

/// <summary>
/// Operations used by the management spreadsheet and autonomous-debug UI that
/// were added after the original ApiClient surface. Kept in a separate source
/// file so the existing client remains source-compatible.
/// </summary>
public static class ApiClientAdditionalOperations
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static async Task<SpreadsheetSchemaAnalysisDto> AnalyzeManagementSpreadsheetAsync(
        this ApiClient client,
        Guid projectId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        using var form = CreateWorkbookForm(fileName, content);
        using var response = await Http(client).PostAsync(
            $"api/v1/project-control/projects/{projectId}/management.xlsx/analyze",
            form,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SpreadsheetSchemaAnalysisDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Spreadsheet analysis API returned an empty response.");
    }

    public static async Task<FlexibleSpreadsheetImportResult> ImportFlexibleManagementSpreadsheetAsync(
        this ApiClient client,
        Guid projectId,
        string fileName,
        byte[] content,
        bool dryRun,
        string? domainId = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"dryRun={dryRun.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(domainId))
            query += "&domainId=" + Uri.EscapeDataString(domainId.Trim());

        using var form = CreateWorkbookForm(fileName, content);
        using var response = await Http(client).PostAsync(
            $"api/v1/project-control/projects/{projectId}/management.xlsx/import-flexible?{query}",
            form,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FlexibleSpreadsheetImportResult>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Flexible spreadsheet import API returned an empty response.");
    }

    public static async Task<AutonomousDebugSessionDto> RunAutonomousDebugAsync(
        this ApiClient client,
        Guid projectId,
        string subjectKey,
        string question,
        string evidenceJson,
        string workspaceRelativePath,
        string? repositoryContext = null,
        string? baseCommit = null,
        string? language = null,
        string domainId = "software",
        AutonomousDebugBudget? budget = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http(client).PostAsJsonAsync(
            $"api/v1/defect-intelligence/projects/{projectId}/autonomous-debug",
            new
            {
                subjectKey,
                question,
                evidenceJson,
                workspaceRelativePath,
                repositoryContext,
                baseCommit,
                language,
                domainId,
                budget
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AutonomousDebugSessionDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Autonomous debug API returned an empty response.");
    }

    public static async Task<IReadOnlyList<AutonomousDebugSessionDto>> GetAutonomousDebugSessionsAsync(
        this ApiClient client,
        Guid projectId,
        int take = 20,
        CancellationToken cancellationToken = default)
        => await Http(client).GetFromJsonAsync<IReadOnlyList<AutonomousDebugSessionDto>>(
               $"api/v1/defect-intelligence/projects/{projectId}/autonomous-debug?take={Math.Clamp(take, 1, 200)}",
               cancellationToken)
           ?? Array.Empty<AutonomousDebugSessionDto>();

    private static MultipartFormDataContent CreateWorkbookForm(string fileName, byte[] content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Workbook file name is required.", nameof(fileName));
        ArgumentNullException.ThrowIfNull(content);

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(XlsxContentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private static HttpClient Http(ApiClient client)
        => ApiClientTransportAccessor.GetHttpClient(client);

    private static class ApiClientTransportAccessor
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_httpClient")]
        internal static extern ref HttpClient GetHttpClient(ApiClient instance);
    }
}
