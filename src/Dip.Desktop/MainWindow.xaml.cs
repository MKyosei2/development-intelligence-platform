// Purpose: Implements M ai nW in do w.x am l desktop UI behavior.
using System.Windows;
using System.Windows.Controls;
using Dip.Desktop.ViewModels;

namespace Dip.Desktop;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += (_, _) => _viewModel.Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await _viewModel.InitializeAsync();
        await _viewModel.LoadSelectedCapabilityAsync();
    }

    private async void CapabilitySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        await _viewModel.LoadSelectedCapabilityAsync();
    }

    private async void ProjectSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        await _viewModel.LoadSelectedCapabilityAsync();
    }

    private async void RefreshProjectsClicked(object sender, RoutedEventArgs e)
    {
        await _viewModel.ReloadProjectsAsync();
        await _viewModel.LoadSelectedCapabilityAsync();
    }

    private async void RefreshViewClicked(object sender, RoutedEventArgs e)
        => await _viewModel.LoadSelectedCapabilityAsync();
}
