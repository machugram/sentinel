using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.SignalR.Client;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;

namespace Sentinel.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IWorkflowService? _workflowService;
    private readonly IWorkflowRunService? _runService;
    private readonly IAlertService? _alertService;
    private readonly AppConfiguration? _config;
    private HubConnection? _hubConnection;
    private CancellationTokenSource? _refreshCts;

    [ObservableProperty] private int _activeWorkflows = 7;
    [ObservableProperty] private int _runningJobs = 2;
    [ObservableProperty] private int _successRate = 98;
    [ObservableProperty] private int _pendingAlerts = 3;
    public bool HasPendingAlerts => PendingAlerts > 0;
    [ObservableProperty] private string _activeTrend = "+2 vs yesterday";
    [ObservableProperty] private string _runningTrend = "2 in-flight";
    [ObservableProperty] private string _successTrend = "24h completed";
    [ObservableProperty] private string _alertTrend = "1 critical";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTime _lastRefreshed = DateTime.Now;
    [ObservableProperty] private ObservableCollection<RecentRunItem> _recentRuns = new();
    [ObservableProperty] private ObservableCollection<AlertItem> _activeAlerts = new();

    public DashboardViewModel()
    {
        LoadSample();
    }

    public DashboardViewModel(
        IWorkflowService workflowService,
        IWorkflowRunService runService,
        IAlertService alertService,
        AppConfiguration config)
    {
        _workflowService = workflowService;
        _runService = runService;
        _alertService = alertService;
        _config = config;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_config?.EnableRealtime == true)
            await SetupSignalRConnection();

        await RefreshDataAsync();
        StartAutoRefresh();
    }

    private async Task SetupSignalRConnection()
    {
        if (_config is null || string.IsNullOrWhiteSpace(_config.SignalRHubUrl))
            return;

        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_config.SignalRHubUrl)
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10) })
                .Build();

            _hubConnection.On<string, string>("WorkflowRunStatusChanged", async (_, _) =>
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => await RefreshDataAsync());
            });

            _hubConnection.On<string, string, string>("AlertCreated", async (_, _, _) =>
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => await RefreshDataAsync());
            });

            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR connection failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (_workflowService is null || _runService is null || _alertService is null)
            return;
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            var workflowsTask = _workflowService.GetAllWorkflowsAsync();
            var runsTask = _runService.GetRecentRunsAsync(10);
            var alertsTask = _alertService.GetActiveAlertsAsync();
            await Task.WhenAll(workflowsTask, runsTask, alertsTask);

            var workflows = (await workflowsTask).ToList();
            var runs = (await runsTask).ToList();
            var alerts = (await alertsTask).ToList();

            ActiveWorkflows = workflows.Count(w => w.Status == WorkflowStatus.Active);
            RunningJobs = runs.Count(r => r.Status == RunStatus.Running);
            ActiveTrend = $"{workflows.Count} total";
            RunningTrend = $"{runs.Count(r => r.Status == RunStatus.Pending)} queued";

            var completed = runs.Where(r => r.Status is RunStatus.Success or RunStatus.Failed).ToList();
            SuccessRate = completed.Count == 0
                ? 100
                : (int)Math.Round(completed.Count(r => r.Status == RunStatus.Success) * 100.0 / completed.Count);
            SuccessTrend = $"{completed.Count} completed";

            PendingAlerts = alerts.Count;
            OnPropertyChanged(nameof(HasPendingAlerts));
            AlertTrend = $"{alerts.Count(a => a.Severity == AlertSeverity.Critical)} critical";

            var slaByWorkflow = workflows
                .Where(w => w.Sla is not null)
                .ToDictionary(w => w.Id, w => w.Sla!);

            RecentRuns = new ObservableCollection<RecentRunItem>(
                runs.OrderByDescending(r => r.StartedAt).Take(8).Select(r =>
                {
                    var duration = r.Duration ?? DateTime.UtcNow - r.StartedAt;
                    var exceeds = slaByWorkflow.TryGetValue(r.WorkflowId, out var sla) && duration > sla.WarningThreshold;
                    return new RecentRunItem(r.Id, r.WorkflowName, r.Status, r.Duration, r.StartedAt, exceeds);
                }));

            ActiveAlerts = new ObservableCollection<AlertItem>(
                alerts.OrderByDescending(a => a.CreatedAt).Take(5).Select(a =>
                    new AlertItem(a.Id, a.Title, a.Message, a.Severity, a.CreatedAt, a.AiSuggestion, a.WorkflowRunId)));

            LastRefreshed = DateTime.Now;
            WeakReferenceMessenger.Default.Send(new DataRefreshedMessage(DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to refresh dashboard: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void StartAutoRefresh()
    {
        _refreshCts = new CancellationTokenSource();
        var interval = TimeSpan.FromSeconds(Math.Max(5, _config?.DashboardRefreshIntervalSeconds ?? 30));
        _ = Task.Run(async () =>
        {
            while (!_refreshCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, _refreshCts.Token);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => await RefreshDataAsync());
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, _refreshCts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        if (_hubConnection != null)
            await _hubConnection.DisposeAsync();
    }

    private void LoadSample()
    {
        RecentRuns = new ObservableCollection<RecentRunItem>
        {
            new(Guid.NewGuid(), "Trade Capture Pipeline", RunStatus.Success, TimeSpan.FromMinutes(2), DateTime.Now.AddMinutes(-5)),
            new(Guid.NewGuid(), "EOD Risk Calculation", RunStatus.Running, null, DateTime.Now.AddMinutes(-15)),
            new(Guid.NewGuid(), "DTCC Regulatory Report", RunStatus.Success, TimeSpan.FromMinutes(8), DateTime.Now.AddMinutes(-30)),
            new(Guid.NewGuid(), "Market Data Reconciliation", RunStatus.Failed, TimeSpan.FromMinutes(1), DateTime.Now.AddHours(-1), true),
            new(Guid.NewGuid(), "NAV Calculation", RunStatus.Success, TimeSpan.FromMinutes(12), DateTime.Now.AddHours(-2)),
        };

        ActiveAlerts = new ObservableCollection<AlertItem>
        {
            new(Guid.NewGuid(), "SLA Breach Warning", "Trade Capture Pipeline approaching SLA threshold", AlertSeverity.Warning, DateTime.Now.AddMinutes(-10), "Check booking API latency."),
            new(Guid.NewGuid(), "Task Failure", "Market Data Reconciliation failed after 3 retries", AlertSeverity.Critical, DateTime.Now.AddHours(-1), "Re-pull the 16:00 snapshot."),
            new(Guid.NewGuid(), "Anomaly Detected", "Unusual execution time for EOD Risk Calculation", AlertSeverity.Info, DateTime.Now.AddHours(-2), "Split equity and rates books."),
        };
    }

    [RelayCommand]
    private void OpenRun(RecentRunItem? item)
    {
        if (item is null)
            return;
        WeakReferenceMessenger.Default.Send(new NavigateRequest("Runs", item.RunId));
    }

    [RelayCommand]
    private void OpenAlert(AlertItem? item)
    {
        if (item is null)
            return;
        if (item.WorkflowRunId is Guid runId && runId != Guid.Empty)
            WeakReferenceMessenger.Default.Send(new NavigateRequest("Runs", runId));
        else
            WeakReferenceMessenger.Default.Send(new NavigateRequest("Alerts", item.Id));
    }
}

public record RecentRunItem(Guid RunId, string WorkflowName, RunStatus Status, TimeSpan? Duration, DateTime StartedAt, bool ExceedsSla = false)
{
    public string StatusText => Status.ToString();
    public string DurationText => Duration.HasValue ? $"{Duration.Value.TotalMinutes:F1} min" : "In progress";
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - StartedAt.ToLocalTime();
            if (diff.TotalMinutes < 60) return $"{diff.TotalMinutes:F0}m ago";
            if (diff.TotalHours < 24) return $"{diff.TotalHours:F0}h ago";
            return $"{diff.TotalDays:F0}d ago";
        }
    }
}

public record AlertItem(Guid Id, string Title, string Message, AlertSeverity Severity, DateTime CreatedAt, string? AiSuggestion, Guid? WorkflowRunId = null)
{
    public string SeverityText => Severity.ToString();
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - CreatedAt.ToLocalTime();
            if (diff.TotalMinutes < 60) return $"{diff.TotalMinutes:F0}m ago";
            if (diff.TotalHours < 24) return $"{diff.TotalHours:F0}h ago";
            return $"{diff.TotalDays:F0}d ago";
        }
    }
}
