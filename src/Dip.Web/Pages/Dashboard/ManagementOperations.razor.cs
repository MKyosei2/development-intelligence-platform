// Purpose: Loads the management cockpit, coordinates cadence and tab state, and refreshes parent summary data after child mutations.
using Dip.Application.Features.Management;
using Microsoft.AspNetCore.Components;

namespace Dip.Web.Pages.Dashboard;

public partial class ManagementOperations
{
    [Parameter]
    public Guid ProjectId { get; set; }

    private readonly string[] _tabs =
    [
        "Alerts & approvals",
        "Risk / decisions / actions",
        "Scope & resources",
        "RACI & calendar",
        "Sync & source of truth"
    ];

    private string _tab = "Alerts & approvals";
    private string _cockpitOwnerRole = "";
    private bool _loading = true;
    private string? _error;
    private string? _notice;

    private DailyCockpitDto? _cockpit;
    private ManagementRoleDirectoryDto? _roles;
    private SyncStateOverviewDto? _sync;
    private OperatingCadenceDto? _cadence;

    protected override Task OnParametersSetAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;

        try
        {
            var cockpitTask = Api.GetDailyCockpitAsync(
                ProjectId,
                8,
                Null(_cockpitOwnerRole));
            var rolesTask = Api.GetManagementRolesAsync(ProjectId);
            var syncTask = Api.GetManagementSyncStateAsync(ProjectId);

            await Task.WhenAll(cockpitTask, rolesTask, syncTask);

            _cockpit = await cockpitTask;
            _roles = await rolesTask;
            _sync = await syncTask;
            _cadence = await Api.GetOperatingCadenceAsync(
                ProjectId,
                "daily");
        }
        catch (Exception exception)
        {
            _error = exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadCadence(string mode)
    {
        try
        {
            _cadence = await Api.GetOperatingCadenceAsync(ProjectId, mode);
            _notice = $"Loaded {mode} review.";
        }
        catch (Exception exception)
        {
            _error = exception.Message;
        }
    }

    private async Task OnPanelChanged(string message)
    {
        _notice = message;
        await LoadAsync();
    }

    private Task SetActiveTab(string tab)
    {
        if (_tabs.Contains(tab, StringComparer.Ordinal))
            _tab = tab;

        return Task.CompletedTask;
    }

    private IReadOnlyList<string> CurrentActorRoles =>
        _roles?.Bindings
            .Where(binding =>
                binding.Status.Equals(
                    "Active",
                    StringComparison.OrdinalIgnoreCase)
                && binding.ActorIdentity.Equals(
                    _roles.CurrentActor,
                    StringComparison.OrdinalIgnoreCase))
            .Select(binding => binding.Role)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? Array.Empty<string>();

    private static string? Null(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
