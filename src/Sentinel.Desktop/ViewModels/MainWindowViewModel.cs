using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;
using Sentinel.Desktop.Services;
using Sentinel.Infrastructure.Auth;

namespace Sentinel.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IRecipient<StatusMessage>, IRecipient<DataRefreshedMessage>
{
    private readonly IServiceProvider? _services;
    private readonly IAuthService? _authService;
    private readonly IAlertService? _alertService;
    private readonly IWorkflowService? _workflowService;
    private readonly AppConfiguration _config;
    private readonly Dictionary<string, ViewModelBase> _views = new();
    private List<Workflow> _paletteWorkflows = [];

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
    [ObservableProperty] private bool _isPaletteOpen;
    [ObservableProperty] private string _paletteQuery = string.Empty;
    [ObservableProperty] private ObservableCollection<CommandPaletteItem> _paletteResults = new();
    [ObservableProperty] private CommandPaletteItem? _selectedPaletteItem;
    [ObservableProperty] private bool _isConfirmOpen;
    [ObservableProperty] private string _confirmTitle = string.Empty;
    [ObservableProperty] private string _confirmMessage = string.Empty;
    [ObservableProperty] private string _confirmLabel = "Confirm";
    [ObservableProperty] private bool _confirmIsDanger;

    private Action? _confirmAction;

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
        IWorkflowService workflowService,
        AppConfiguration config)
    {
        _services = services;
        _authService = authService;
        _alertService = alertService;
        _workflowService = workflowService;
        _config = config;
        IsDarkTheme = !string.Equals(config.Theme, "Light", StringComparison.OrdinalIgnoreCase);

        BuildNavigation();
        WeakReferenceMessenger.Default.Register<StatusMessage>(this, (_, message) => Receive(message));
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(this, (_, message) => Receive(message));
        WeakReferenceMessenger.Default.Register<NavigateRequest>(this, (_, request) =>
        {
            Dispatcher.UIThread.Post(() => _ = ApplyNavigateRequestAsync(request));
        });
        WeakReferenceMessenger.Default.Register<ConfirmRequest>(this, (_, request) =>
        {
            Dispatcher.UIThread.Post(() => ShowConfirm(request));
        });
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

    [RelayCommand]
    private async Task TogglePaletteAsync()
    {
        if (IsPaletteOpen)
        {
            ClosePalette();
            return;
        }

        PaletteQuery = string.Empty;
        if (_workflowService is not null)
            _paletteWorkflows = (await _workflowService.GetAllWorkflowsAsync()).ToList();
        RebuildPalette();
        IsPaletteOpen = true;
        SelectedPaletteItem = PaletteResults.FirstOrDefault();
    }

    [RelayCommand]
    private void HandleEscape()
    {
        if (IsConfirmOpen)
        {
            CancelConfirm();
            return;
        }

        if (IsPaletteOpen)
            ClosePalette();
    }

    [RelayCommand]
    private void ClosePalette()
    {
        IsPaletteOpen = false;
        PaletteQuery = string.Empty;
    }

    private void ShowConfirm(ConfirmRequest request)
    {
        ConfirmTitle = request.Title;
        ConfirmMessage = request.Message;
        ConfirmLabel = request.ConfirmLabel;
        ConfirmIsDanger = request.IsDanger;
        _confirmAction = request.OnConfirm;
        IsConfirmOpen = true;
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        IsConfirmOpen = false;
        _confirmAction = null;
    }

    [RelayCommand]
    private void AcceptConfirm()
    {
        var action = _confirmAction;
        IsConfirmOpen = false;
        _confirmAction = null;
        action?.Invoke();
    }

    [RelayCommand]
    private void ExecutePaletteItem(CommandPaletteItem? item)
    {
        var target = item ?? SelectedPaletteItem;
        if (target is null)
            return;
        ClosePalette();
        target.Execute();
    }

    [RelayCommand]
    private void NewWorkflowShortcut()
    {
        if (IsPaletteOpen)
            ClosePalette();
        _ = ApplyNavigateRequestAsync(new NavigateRequest("Workflows", OpenCreate: true));
    }

    [RelayCommand]
    private void RefreshCurrent()
    {
        switch (CurrentView)
        {
            case DashboardViewModel dashboard:
                dashboard.RefreshDataCommand.Execute(null);
                break;
            case WorkflowListViewModel workflows:
                workflows.LoadCommand.Execute(null);
                break;
            case RunsViewModel runs:
                runs.LoadCommand.Execute(null);
                break;
            case AlertsViewModel alerts:
                alerts.LoadCommand.Execute(null);
                break;
            case CalendarsViewModel calendars:
                calendars.LoadCommand.Execute(null);
                break;
            case AuditViewModel audit:
                audit.LoadCommand.Execute(null);
                break;
        }
    }

    partial void OnPaletteQueryChanged(string value)
    {
        RebuildPalette();
        SelectedPaletteItem = PaletteResults.FirstOrDefault();
    }

    private void RebuildPalette()
    {
        var query = PaletteQuery?.Trim() ?? string.Empty;
        var items = new List<CommandPaletteItem>();

        AddIfMatch(items, query, "Dashboard", "Go to page", "Page", () => Navigate("Dashboard"));
        AddIfMatch(items, query, "Workflows", "Go to page", "Page", () => Navigate("Workflows"));
        AddIfMatch(items, query, "Runs", "Go to page", "Page", () => Navigate("Runs"));
        AddIfMatch(items, query, "Alerts", "Go to page", "Page", () => Navigate("Alerts"));
        AddIfMatch(items, query, "JIL Migration", "Go to page", "Page", () => Navigate("Migration"));
        AddIfMatch(items, query, "Calendars", "Go to page", "Page", () => Navigate("Calendars"));
        AddIfMatch(items, query, "Audit Logs", "Go to page", "Page", () => Navigate("Audit"));
        AddIfMatch(items, query, "Settings", "Go to page", "Page", () => Navigate("Settings"));
        AddIfMatch(items, query, "New workflow", "Create a workflow", "Action", () =>
            _ = ApplyNavigateRequestAsync(new NavigateRequest("Workflows", OpenCreate: true)));
        AddIfMatch(items, query, "Toggle theme", IsDarkTheme ? "Switch to light" : "Switch to dark", "Action", ToggleTheme);
        AddIfMatch(items, query, "Refresh", "Reload the current page", "Action", RefreshCurrent);

        foreach (var workflow in _paletteWorkflows)
        {
            var id = workflow.Id;
            AddIfMatch(items, query, workflow.Name, $"Workflow · {workflow.Status}", "Workflow",
                () => _ = ApplyNavigateRequestAsync(new NavigateRequest("Workflows", id)));
        }

        PaletteResults = new ObservableCollection<CommandPaletteItem>(items);
    }

    private static void AddIfMatch(
        List<CommandPaletteItem> items,
        string query,
        string title,
        string subtitle,
        string kind,
        Action execute)
    {
        if (!string.IsNullOrEmpty(query) &&
            !title.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !kind.Contains(query, StringComparison.OrdinalIgnoreCase))
            return;

        items.Add(new CommandPaletteItem
        {
            Title = title,
            Subtitle = subtitle,
            Kind = kind,
            Execute = execute
        });
    }

    private async Task ApplyNavigateRequestAsync(NavigateRequest request)
    {
        Navigate(request.ViewKey);
        switch (request.ViewKey)
        {
            case "Runs":
                await GetView<RunsViewModel>("Runs").ApplyIncomingAsync(request.EntityId, request.Filter);
                break;
            case "Alerts":
                await GetView<AlertsViewModel>("Alerts").FocusAlertAsync(request.EntityId);
                break;
            case "Workflows":
                var workflows = GetView<WorkflowListViewModel>("Workflows");
                await workflows.ApplyIncomingAsync(request.EntityId, request.Filter);
                if (request.OpenCreate)
                    workflows.OpenCreate();
                break;
        }
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
        switch (CurrentView)
        {
            case DashboardViewModel dashboard:
                dashboard.RefreshQuiet();
                break;
            case RunsViewModel runs:
                runs.RefreshQuiet();
                break;
            case WorkflowListViewModel workflows:
                workflows.RefreshQuiet();
                break;
        }
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

    private T GetView<T>(string key) where T : ViewModelBase
    {
        if (_views.TryGetValue(key, out var existing))
            return (T)existing;

        T created;
        if (_services is not null)
            created = _services.GetRequiredService<T>();
        else
            created = (T)Activator.CreateInstance(typeof(T))!;

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
