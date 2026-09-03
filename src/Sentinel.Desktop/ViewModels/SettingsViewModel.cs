using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sentinel.Desktop.Models;
using Sentinel.Desktop.Services;
using Sentinel.Infrastructure.Mock;

namespace Sentinel.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppConfiguration _config;
    private readonly IDemoCatalogService? _catalog;

    [ObservableProperty] private string _apiBaseUrl;
    [ObservableProperty] private int _refreshIntervalSeconds;
    [ObservableProperty] private string _theme;
    [ObservableProperty] private bool _enableNotifications;
    [ObservableProperty] private bool _enableRealtime;
    [ObservableProperty] private bool _enableTelemetry;

    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "Dark", "Light" };

    public SettingsViewModel() : this(new AppConfiguration(), null)
    {
    }

    public SettingsViewModel(AppConfiguration config, IDemoCatalogService? catalog)
    {
        _config = config;
        _catalog = catalog;
        _apiBaseUrl = config.ApiBaseUrl;
        _refreshIntervalSeconds = config.DashboardRefreshIntervalSeconds;
        _theme = config.Theme;
        _enableNotifications = config.EnableNotifications;
        _enableRealtime = config.EnableRealtime;
        _enableTelemetry = config.EnableTelemetry;
    }

    partial void OnThemeChanged(string value)
    {
        _config.Theme = value;
        ThemeManager.Apply(value);
    }

    [RelayCommand]
    private void Save()
    {
        _config.ApiBaseUrl = ApiBaseUrl;
        _config.DashboardRefreshIntervalSeconds = Math.Clamp(RefreshIntervalSeconds, 5, 300);
        _config.Theme = Theme;
        _config.EnableNotifications = EnableNotifications;
        _config.EnableRealtime = EnableRealtime;
        _config.EnableTelemetry = EnableTelemetry;
        ThemeManager.Apply(_config.Theme);
        AppConfigurationStore.Save(_config);
        WeakReferenceMessenger.Default.Send(new StatusMessage("Settings saved on this machine"));
    }

    [RelayCommand]
    private void Reset()
    {
        ApiBaseUrl = "https://localhost:5001";
        RefreshIntervalSeconds = 30;
        Theme = "Dark";
        EnableNotifications = true;
        EnableRealtime = false;
        EnableTelemetry = false;
        Save();
    }

    [RelayCommand]
    private void ResetDemoData()
    {
        WeakReferenceMessenger.Default.Send(new ConfirmRequest(
            "Reset demo data",
            "Replace the local mock catalog with the original seed workflows, runs, and alerts? Your created workflows will be removed.",
            "Reset catalog",
            true,
            () =>
            {
                _catalog?.ResetToSeed();
                WeakReferenceMessenger.Default.Send(new StatusMessage("Demo catalog reset to seed data"));
            }));
    }
}
