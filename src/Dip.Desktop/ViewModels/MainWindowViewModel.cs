using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dip.Client;

namespace Dip.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DipPlatformClient _client;
    private ProjectListItem? _selectedProject;
    private CapabilityListItem? _selectedCapability;
    private string _status = "Starting...";
    private string _endpoint = string.Empty;
    private string _content = "Select a capability.";
    private bool _busy;

    public MainWindowViewModel()
    {
        _client = new DipPlatformClient();
    }

    public ObservableCollection<ProjectListItem> Projects { get; } = [];
    public ObservableCollection<CapabilityListItem> Capabilities { get; } = [];

    public ProjectListItem? SelectedProject
    {
        get => _selectedProject;
        set { if (!Equals(_selectedProject, value)) { _selectedProject = value; OnPropertyChanged(); } }
    }

    public CapabilityListItem? SelectedCapability
    {
        get => _selectedCapability;
        set { if (!Equals(_selectedCapability, value)) { _selectedCapability = value; OnPropertyChanged(); } }
    }

    public string Status
    {
        get => _status;
        private set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    public string Endpoint
    {
        get => _endpoint;
        private set { if (_endpoint != value) { _endpoint = value; OnPropertyChanged(); } }
    }

    public string Content
    {
        get => _content;
        private set { if (_content != value) { _content = value; OnPropertyChanged(); } }
    }

    public bool Busy
    {
        get => _busy;
        private set { if (_busy != value) { _busy = value; OnPropertyChanged(); } }
    }

    public async Task InitializeAsync()
    {
        Busy = true;
        try
        {
            if (!await _client.IsHealthyAsync())
            {
                Status = "DIP API is not reachable at http://127.0.0.1:5080";
                Content = "Start the DIP backend, then choose Refresh.";
                return;
            }

            await ReloadProjectsAsync();
            await ReloadCapabilitiesAsync();
            Status = "DIP API connected. Select a capability.";
        }
        catch (Exception ex)
        {
            Status = "Initialization failed";
            Content = ex.ToString();
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task ReloadProjectsAsync()
    {
        var previous = SelectedProject?.Id;
        var projects = await _client.GetProjectsAsync();
        Projects.Clear();
        foreach (var project in projects.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            Projects.Add(project);

        SelectedProject = previous.HasValue
            ? Projects.FirstOrDefault(x => x.Id == previous.Value) ?? Projects.FirstOrDefault()
            : Projects.FirstOrDefault();
    }

    public async Task ReloadCapabilitiesAsync()
    {
        var previous = SelectedCapability?.Id;
        var capabilities = await _client.GetCapabilitiesAsync();
        Capabilities.Clear();
        foreach (var capability in capabilities
                     .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            Capabilities.Add(capability);

        SelectedCapability = previous is null
            ? Capabilities.FirstOrDefault()
            : Capabilities.FirstOrDefault(x => x.Id == previous) ?? Capabilities.FirstOrDefault();
    }

    public async Task LoadSelectedCapabilityAsync()
    {
        if (SelectedCapability is null)
            return;

        Busy = true;
        try
        {
            var result = await _client.ReadCapabilityAsync(SelectedCapability, SelectedProject?.Id);
            Endpoint = result.Endpoint;
            Content = result.Success
                ? result.Content
                : $"HTTP {result.StatusCode}\n{result.Error}\n\n{result.Content}";
            Status = result.Success
                ? $"{SelectedCapability.DisplayName}: HTTP {result.StatusCode}"
                : $"{SelectedCapability.DisplayName}: FAILED ({result.StatusCode})";
        }
        catch (Exception ex)
        {
            Status = $"{SelectedCapability.DisplayName}: failed";
            Content = ex.ToString();
        }
        finally
        {
            Busy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose() => _client.Dispose();
}
