using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.Core.Interfaces;
using Sentinel.Desktop.Models;
using Sentinel.Desktop.Services;
using Sentinel.Infrastructure.Auth;

namespace Sentinel.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IRecipient<StatusMessage>, IRecipient<DataRefreshedMessage>
{
    private readonly IServiceProvider? _services;
    private readonly IAuthService? _authService;
    private readonly IAlertService? _alertService;
    private readonly AppConfiguration _config;
    private readonly Dictionary<string, ViewModelBase> _views = new();

    [ObservableProperty] private ViewModelBase _currentView = null!;
    [ObservableProperty] private string _userName = "Alex Chen";
    [ObservableProperty] private string _userRole = "Operator";
    [ObservableProperty] private string _userInitials = "AC";
    [ObservableProperty] private int _activeAlertCount;
    [ObservableProperty] private bool _hasActiveAlerts;
    [ObservableProperty] private string _selectedNav = "Dashboard";
    [ObservableProperty] private bool _isSidebarExpanded = true;
    [ObservableProperty] private bool _isDarkTheme = true;
    [ObservableProperty] private bool _isConnected = true;
    [ObservableProperty] private string _connectionLabel = "Local mock";
    [ObservableProperty] private DateTime _lastRefreshed = DateTime.Now;
    [ObservableProperty] private string _globalSearch = string.Empty;
    [ObservableProperty] private string? _toastMessage;
    [ObservableProperty] private bool _isToastVisible;

    public double SidebarWidth => IsSidebarExpanded ? 252 : 76;

    public ObservableCollection<NavigationItem> MainNavItems { get; } = new();
    public ObservableCollection<NavigationItem> ToolsNavItems { get; } = new();
    public ObservableCollection<NavigationItem> ComplianceNavItems { get; } = new();
    public ObservableCollection<NavigationItem> AllNavItems { get; } = new();

    public MainWindowViewModel()
    {
        _config = new AppConfiguration();
        BuildNavigation();
        CurrentView = new DashboardViewModel();
        MarkActive("Dashboard");
    }

    public MainWindowViewModel(
        IServiceProvider services,
        IAuthService authService,
        IAlertService alertService,
        AppConfiguration config)
    {
        _services = services;
        _authService = authService;
        _alertService = alertService;
        _config = config;
        IsDarkTheme = !string.Equals(config.Theme, "Light", StringComparison.OrdinalIgnoreCase);

        BuildNavigation();
        WeakReferenceMessenger.Default.Register<StatusMessage>(this, (_, message) => Receive(message));
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(this, (_, message) => Receive(message));
        CurrentView = GetView<DashboardViewModel>("Dashboard");
        MarkActive("Dashboard");
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_authService is not null && !_authService.IsAuthenticated)
            await _authService.LoginAsync("Alex Chen", "dev");

        if (_authService?.CurrentUser is { } user)
        {
            UserName = user.DisplayName;
            UserRole = user.Roles.FirstOrDefault() ?? "Operator";
            UserInitials = GetInitials(UserName);
        }

        await RefreshAlertBadgeAsync();
    }

    private void BuildNavigation()
    {
        MainNavItems.Add(new NavigationItem { Label = "Dashboard", IconKey = "Icon.Dashboard", ViewKey = "Dashboard" });
        MainNavItems.Add(new NavigationItem { Label = "Workflows", IconKey = "Icon.Workflows", ViewKey = "Workflows" });
        MainNavItems.Add(new NavigationItem { Label = "Runs", IconKey = "Icon.Runs", ViewKey = "Runs" });
        MainNavItems.Add(new NavigationItem { Label = "Alerts", IconKey = "Icon.Alerts", ViewKey = "Alerts" });

        ToolsNavItems.Add(new NavigationItem { Label = "JIL Migration", IconKey = "Icon.Migration", ViewKey = "Migration" });
        ToolsNavItems.Add(new NavigationItem { Label = "Calendars", IconKey = "Icon.Calendars", ViewKey = "Calendars" });

        ComplianceNavItems.Add(new NavigationItem { Label = "Audit Logs", IconKey = "Icon.Audit", ViewKey = "Audit" });

        foreach (var item in MainNavItems.Concat(ToolsNavItems).Concat(ComplianceNavItems))
            AllNavItems.Add(item);
    }

    [RelayCommand]
    private void Navigate(string viewKey)
    {
        SelectedNav = viewKey;
        MarkActive(viewKey);
        CurrentView = viewKey switch
        {
            "Dashboard" => GetView<DashboardViewModel>(viewKey),
            "Workflows" => GetView<WorkflowListViewModel>(viewKey),
            "Runs" => GetView<RunsViewModel>(viewKey),
            "Alerts" => GetView<AlertsViewModel>(viewKey),
            "Migration" => GetView<MigrationWizardViewModel>(viewKey),
            "Calendars" => GetView<CalendarsViewModel>(viewKey),
            "Audit" => GetView<AuditViewModel>(viewKey),
            "Settings" => GetView<SettingsViewModel>(viewKey),
            _ => CurrentView
        };
    }

    [RelayCommand] private void NavigateToDashboard() => Navigate("Dashboard");
    [RelayCommand] private void NavigateToWorkflows() => Navigate("Workflows");
    [RelayCommand] private void NavigateToRuns() => Navigate("Runs");
    [RelayCommand] private void NavigateToAlerts() => Navigate("Alerts");
    [RelayCommand] private void NavigateToMigration() => Navigate("Migration");
    [RelayCommand] private void NavigateToCalendars() => Navigate("Calendars");
    [RelayCommand] private void NavigateToAudit() => Navigate("Audit");
    [RelayCommand] private void NavigateToSettings() => Navigate("Settings");

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        OnPropertyChanged(nameof(SidebarWidth));
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _config.Theme = IsDarkTheme ? "Dark" : "Light";
        ThemeManager.Apply(_config.Theme);
    }

    [RelayCommand]
    private void SubmitSearch()
    {
        Navigate("Workflows");
        if (GetView<WorkflowListViewModel>("Workflows") is WorkflowListViewModel workflows)
            workflows.SearchQuery = GlobalSearch;
    }

    [RelayCommand]
    private void DismissToast()
    {
        IsToastVisible = false;
    }

    public void Receive(StatusMessage message)
    {
        ToastMessage = message.Text;
        IsToastVisible = true;
        _ = HideToastAsync();
    }

    public void Receive(DataRefreshedMessage message)
    {
        LastRefreshed = message.When.ToLocalTime();
        _ = RefreshAlertBadgeAsync();
    }

    private async Task HideToastAsync()
    {
        await Task.Delay(3200);
        IsToastVisible = false;
    }

    private async Task RefreshAlertBadgeAsync()
    {
        if (_alertService is null)
            return;

        var alerts = await _alertService.GetActiveAlertsAsync();
        ActiveAlertCount = alerts.Count(a => a.ResolvedAt is null && a.AcknowledgedAt is null);
        HasActiveAlerts = ActiveAlertCount > 0;
        var alertsNav = AllNavItems.FirstOrDefault(i => i.ViewKey == "Alerts");
        if (alertsNav is not null)
            alertsNav.BadgeCount = ActiveAlertCount;
    }

    private ViewModelBase GetView<T>(string key) where T : ViewModelBase
    {
        if (_views.TryGetValue(key, out var existing))
            return existing;

        ViewModelBase created;
        if (_services is not null)
            created = _services.GetRequiredService<T>();
        else
            created = (ViewModelBase)Activator.CreateInstance(typeof(T))!;

        _views[key] = created;
        return created;
    }

    private void MarkActive(string viewKey)
    {
        foreach (var item in AllNavItems)
            item.IsActive = item.ViewKey == viewKey;
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "U";
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
        return string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }
}
