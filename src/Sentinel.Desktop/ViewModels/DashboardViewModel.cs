using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Sentinel.Core.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Avalonia.Threading;

namespace Sentinel.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IWorkflowService _workflowService;
    private readonly IWorkflowRunService _runService;
    private readonly IAlertService _alertService;
    private HubConnection? _hubConnection;
    private CancellationTokenSource? _refreshCts;
    
    [ObservableProperty]
    private int _activeWorkflows = 47;
    
    [ObservableProperty]
    private int _runningJobs = 12;
    
    [ObservableProperty]
    private int _successRate = 98;
    
    [ObservableProperty]
    private int _pendingAlerts = 3;
    
    [ObservableProperty]
    private bool _isLoading = false;
    
    [ObservableProperty]
    private DateTime _lastRefreshed = DateTime.Now;
    
    [ObservableProperty]
    private ObservableCollection<RecentRunItem> _recentRuns;
    
    [ObservableProperty]
    private ObservableCollection<AlertItem> _activeAlerts;
    
    public DashboardViewModel(
        IWorkflowService workflowService,
        IWorkflowRunService runService,
        IAlertService alertService)
    {
        _workflowService = workflowService;
        _runService = runService;
        _alertService = alertService;
        
        // Initialize collections
        _recentRuns = new ObservableCollection<RecentRunItem>();
        _activeAlerts = new ObservableCollection<AlertItem>();
        
        // Start real-time connection
        _ = InitializeAsync();
    }
    
    private async Task InitializeAsync()
    {
        await SetupSignalRConnection();
        await RefreshDataAsync();
        StartAutoRefresh();
    }
    
    /// <summary>
    /// Setup SignalR for real-time updates
    /// </summary>
    private async Task SetupSignalRConnection()
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("https://sentinel-api.example.com/hubs/monitoring")
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10) })
                .Build();
            
            // Subscribe to workflow run updates
            _hubConnection.On<string, string>("WorkflowRunStatusChanged", async (runId, status) =>
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await RefreshRecentRunsAsync();
                    RunningJobs = _recentRuns.Count(r => r.Status == "Running");
                });
            });
            
            // Subscribe to alert notifications
            _hubConnection.On<string, string, string>("AlertCreated", async (alertId, title, severity) =>
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await RefreshAlertsAsync();
                    PendingAlerts = _activeAlerts.Count;
                });
            });
            
            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't crash - fall back to polling
            Console.WriteLine($"SignalR connection failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Refresh all dashboard data
    /// </summary>
    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (IsLoading) return;
        
        IsLoading = true;
        try
        {
            // Fetch data in parallel
            var workflowsTask = _workflowService.GetAllWorkflowsAsync();
            var runsTask = _runService.GetRecentRunsAsync(count: 10);
            var alertsTask = _alertService.GetActiveAlertsAsync();
            
            await Task.WhenAll(workflowsTask, runsTask, alertsTask);
            
            // Update statistics
            var workflows = await workflowsTask;
            ActiveWorkflows = workflows.Count(w => w.Status == Core.Models.WorkflowStatus.Active);
            
            var runs = await runsTask;
            RunningJobs = runs.Count(r => r.Status == Core.Models.RunStatus.Running);
            
            var completedRuns = runs.Where(r => 
                r.Status == Core.Models.RunStatus.Success || 
                r.Status == Core.Models.RunStatus.Failed).ToList();
            
            if (completedRuns.Any())
            {
                SuccessRate = (int)((double)completedRuns.Count(r => r.Status == Core.Models.RunStatus.Success) 
                    / completedRuns.Count * 100);
            }
            
            // Update recent runs
            _recentRuns.Clear();
            foreach (var run in runs.OrderByDescending(r => r.StartedAt).Take(10))
            {
                var workflow = workflows.FirstOrDefault(w => w.Id == run.WorkflowId);
                _recentRuns.Add(new RecentRunItem(
                    workflow?.Name ?? "Unknown",
                    run.Status.ToString(),
                    run.Duration,
                    run.StartedAt
                ));
            }
            
            // Update alerts
            var alerts = await alertsTask;
            PendingAlerts = alerts.Count();
            
            _activeAlerts.Clear();
            foreach (var alert in alerts.OrderByDescending(a => a.CreatedAt).Take(5))
            {
                _activeAlerts.Add(new AlertItem(
                    alert.Title,
                    alert.Message,
                    alert.Severity == Core.Models.AlertSeverity.Critical ? AlertSeverity.Critical :
                    alert.Severity == Core.Models.AlertSeverity.Warning ? AlertSeverity.Warning :
                    AlertSeverity.Info,
                    alert.CreatedAt
                ));
            }
            
            LastRefreshed = DateTime.Now;
        }
        catch (Exception ex)
        {
            // Show error notification
            Console.WriteLine($"Failed to refresh dashboard: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task RefreshRecentRunsAsync()
    {
        // Optimized refresh for just recent runs
        var runs = await _runService.GetRecentRunsAsync(count: 10);
        _recentRuns.Clear();
        foreach (var run in runs.OrderByDescending(r => r.StartedAt))
        {
            // Map to UI model
        }
    }
    
    private async Task RefreshAlertsAsync()
    {
        var alerts = await _alertService.GetActiveAlertsAsync();
        // Update collection
    }
    
    /// <summary>
    /// Auto-refresh every 30 seconds (only stats, not full refresh)
    /// </summary>
    private void StartAutoRefresh()
    {
        _refreshCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!_refreshCts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), _refreshCts.Token);
                if (!IsLoading)
                {
                    await RefreshDataAsync();
                }
            }
        }, _refreshCts.Token);
    }
    
    public async ValueTask DisposeAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
    
    // Legacy constructor for design-time data
    public DashboardViewModel()
    {
        // Sample data for demonstration
        _recentRuns = new ObservableCollection<RecentRunItem>
        {
            new("Trade Capture Pipeline", "Success", TimeSpan.FromMinutes(2), DateTime.Now.AddMinutes(-5)),
            new("EOD Risk Calculation", "Running", null, DateTime.Now.AddMinutes(-15)),
            new("DTCC Regulatory Report", "Success", TimeSpan.FromMinutes(8), DateTime.Now.AddMinutes(-30)),
            new("Market Data Reconciliation", "Failed", TimeSpan.FromMinutes(1), DateTime.Now.AddHours(-1)),
            new("NAV Calculation", "Success", TimeSpan.FromMinutes(12), DateTime.Now.AddHours(-2)),
        };
        
        _activeAlerts = new ObservableCollection<AlertItem>
        {
            new("SLA Breach Warning", "Trade Capture Pipeline approaching SLA threshold", AlertSeverity.Warning, DateTime.Now.AddMinutes(-10)),
            new("Task Failure", "Market Data Reconciliation failed after 3 retries", AlertSeverity.Critical, DateTime.Now.AddHours(-1)),
            new("Anomaly Detected", "Unusual execution time for EOD Risk Calculation", AlertSeverity.Info, DateTime.Now.AddHours(-2)),
        };
    }
}

public record RecentRunItem(string WorkflowName, string Status, TimeSpan? Duration, DateTime StartedAt)
{
    public string DurationText => Duration.HasValue 
        ? $"{Duration.Value.TotalMinutes:F1} min" 
        : "In Progress";
        
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - StartedAt;
            if (diff.TotalMinutes < 60) return $"{diff.TotalMinutes:F0}m ago";
            if (diff.TotalHours < 24) return $"{diff.TotalHours:F0}h ago";
            return $"{diff.TotalDays:F0}d ago";
        }
    }
}

public record AlertItem(string Title, string Message, AlertSeverity Severity, DateTime CreatedAt)
{
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - CreatedAt;
            if (diff.TotalMinutes < 60) return $"{diff.TotalMinutes:F0}m ago";
            if (diff.TotalHours < 24) return $"{diff.TotalHours:F0}h ago";
            return $"{diff.TotalDays:F0}d ago";
        }
    }
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
