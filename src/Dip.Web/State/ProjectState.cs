// Purpose: Implements P ro je ct St at e web UI behavior.
using Dip.Application.Features.Dashboard;
using Dip.Web.Services;

namespace Dip.Web.State;

public sealed class ProjectState(ApiClient api)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<ProjectDashboardSummary> _projects = Array.Empty<ProjectDashboardSummary>();

    public IReadOnlyList<ProjectDashboardSummary> Projects => _projects;
    public Guid? CurrentProjectId { get; private set; }
    public ProjectDashboardSummary? CurrentProject => CurrentProjectId is { } id
        ? _projects.FirstOrDefault(project => project.ProjectId == id)
        : null;
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? LastRefreshedAtUtc { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (LastRefreshedAtUtc is not null || IsLoading)
            return;
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            IsLoading = true;
            Error = null;
            Notify();
            _projects = await api.GetProjectsAsync(cancellationToken);
            if (CurrentProjectId is { } active
                && !_projects.Any(project => project.ProjectId == active))
            {
                CurrentProjectId = null;
            }
            LastRefreshedAtUtc = DateTimeOffset.UtcNow;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Keep the last known portfolio visible during a transient API/realtime refresh failure.
            Error = exception.Message;
        }
        finally
        {
            IsLoading = false;
            _gate.Release();
            Notify();
        }
    }

    public void Select(Guid? projectId)
    {
        if (CurrentProjectId == projectId)
            return;
        CurrentProjectId = projectId;
        Notify();
    }

    public void SelectFromPath(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2
            && segments[0].Equals("dashboard", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[1], out var projectId))
        {
            Select(projectId);
            return;
        }
        if (segments.Length == 0 || segments[0].Equals("projects", StringComparison.OrdinalIgnoreCase))
            Select(null);
    }

    public ProjectDashboardSummary? Find(Guid projectId) =>
        _projects.FirstOrDefault(project => project.ProjectId == projectId);

    private void Notify() => Changed?.Invoke();
}
