// Purpose: Provides additive web-client operations for management, QA, debugging, and project intelligence.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Dip.Application.Features.Management;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Web.Services;

/// <summary>
/// Additive ApiClient operations used by management spreadsheet, autonomous debug,
/// commercial QA, branch QA, and project-management command-center surfaces.
/// Existing operations are intentionally retained so this file is an additive merge,
/// never a replacement that removes previously supported GUI calls.
/// </summary>
public static class ApiClientAdditionalOperations
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // -----------------------------------------------------------------
    // Existing management spreadsheet operations (must be preserved)
    // -----------------------------------------------------------------
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

    // -----------------------------------------------------------------
    // Existing autonomous-debug operations (must be preserved)
    // -----------------------------------------------------------------
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

    // -----------------------------------------------------------------
    // Commercial QA / PM decision operations
    // -----------------------------------------------------------------
    public static async Task<IReadOnlyList<PmQaDecisionItemDto>> GetQaPmDecisionsAsync(
        this ApiClient client,
        Guid projectId,
        int take = 10,
        CancellationToken cancellationToken = default)
        => await Http(client).GetFromJsonAsync<IReadOnlyList<PmQaDecisionItemDto>>(
               $"api/v1/qa/projects/{projectId}/pm-decisions?take={Math.Clamp(take, 1, 50)}",
               cancellationToken)
           ?? Array.Empty<PmQaDecisionItemDto>();

    public static async Task<CommercialQaCommandCenterDto> GetCommercialQaCommandCenterAsync(
        this ApiClient client,
        Guid projectId,
        int take = 40,
        CancellationToken cancellationToken = default)
        => await Http(client).GetFromJsonAsync<CommercialQaCommandCenterDto>(
               $"api/v1/qa/projects/{projectId}/command-center?take={Math.Clamp(take, 10, 100)}",
               cancellationToken)
           ?? throw new InvalidDataException("Commercial QA command-center API returned an empty response.");

    // -----------------------------------------------------------------
    // Branch QA operations
    // -----------------------------------------------------------------
    public static async Task<BranchQualityOverviewDto> GetBranchQualityAsync(
        this ApiClient client,
        Guid projectId,
        string workspaceRelativePath,
        bool refreshRemote = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRelativePath))
            throw new ArgumentException("Workspace path is required.", nameof(workspaceRelativePath));

        var path = Uri.EscapeDataString(workspaceRelativePath);
        return await Http(client).GetFromJsonAsync<BranchQualityOverviewDto>(
                   $"api/v1/qa/projects/{projectId}/branches?workspaceRelativePath={path}&refreshRemote={refreshRemote.ToString().ToLowerInvariant()}",
                   cancellationToken)
               ?? throw new InvalidDataException("Branch quality API returned an empty response.");
    }

    public static async Task<QaRunResultDto> RunBranchQaAsync(
        this ApiClient client,
        Guid projectId,
        string branch,
        string workspaceRelativePath,
        string language = "auto",
        string? commitSha = null,
        string? runtimeExecutable = null,
        IReadOnlyList<string>? runtimeArguments = null,
        int timeoutSeconds = 120,
        bool automaticallyReproduce = true,
        bool recordScreen = true,
        bool recordSystemAudio = true,
        bool refreshRemote = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branch))
            throw new ArgumentException("Branch is required.", nameof(branch));
        if (string.IsNullOrWhiteSpace(workspaceRelativePath))
            throw new ArgumentException("Workspace path is required.", nameof(workspaceRelativePath));

        using var response = await Http(client).PostAsJsonAsync(
            $"api/v1/qa/projects/{projectId}/branches/run",
            new
            {
                branch,
                workspaceRelativePath,
                language,
                commitSha,
                runtimeExecutable,
                runtimeArguments,
                timeoutSeconds,
                automaticallyReproduce,
                recordScreen,
                recordSystemAudio,
                refreshRemote
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QaRunResultDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Branch QA API returned an empty response.");
    }

    // -----------------------------------------------------------------
    // Project-management command center
    // -----------------------------------------------------------------
    public static async Task<ProjectManagementCommandCenterDto> GetProjectManagementCommandCenterAsync(
        this ApiClient client,
        Guid projectId,
        int horizonWeeks = 8,
        string? ownerRole = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/project-control/projects/{projectId}/command-center?horizonWeeks={Math.Clamp(horizonWeeks, 1, 26)}";
        if (!string.IsNullOrWhiteSpace(ownerRole))
            url += "&ownerRole=" + Uri.EscapeDataString(ownerRole.Trim());

        return await Http(client).GetFromJsonAsync<ProjectManagementCommandCenterDto>(
                   url,
                   cancellationToken)
               ?? throw new InvalidDataException("Project management command-center API returned an empty response.");
    }

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
    {
        ArgumentNullException.ThrowIfNull(client);
        return ApiClientTransportAccessor.GetHttpClient(client);
    }

    private static class ApiClientTransportAccessor
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_httpClient")]
        internal static extern ref HttpClient GetHttpClient(ApiClient instance);
    }
}
