using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;

namespace Sentinel.Desktop.ViewModels;

public partial class RunsViewModel : ViewModelBase
{
    private readonly IWorkflowRunService? _runService;
    private readonly IWorkflowService? _workflowService;
    private readonly Dictionary<Guid, WorkflowSla> _slas = new();
    private List<WorkflowRun> _all = new();

    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private ObservableCollection<WorkflowRun> _runs = new();
    [ObservableProperty] private WorkflowRun? _selectedRun;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _runLog = string.Empty;
    [ObservableProperty] private bool _selectedRunExceedsSla;

    public IReadOnlyList<string> StatusOptions { get; } =
        new[] { "All", "Running", "Success", "Failed", "Pending", "Cancelled", "TimedOut" };

    public bool HasSelection => SelectedRun is not null;
    public bool CanRetry => SelectedRun is { Status: RunStatus.Failed or RunStatus.TimedOut or RunStatus.Cancelled };
    public bool CanCancel => SelectedRun is { Status: RunStatus.Running or RunStatus.Pending };

    public RunsViewModel()
    {
        LoadSample();
    }

    public RunsViewModel(IWorkflowRunService runService, IWorkflowService workflowService)
    {
        _runService = runService;
        _workflowService = workflowService;
        _ = LoadAsync();
    }

    partial void OnStatusFilterChanged(string value) => ApplyFilters();
    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedRunChanged(WorkflowRun? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(CanCancel));
        RefreshSelectionMeta();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_runService is null)
            return;

        IsLoading = true;
        try
        {
            _all = (await _runService.GetRecentRunsAsync(50)).ToList();
            if (_workflowService is not null)
            {
                _slas.Clear();
                foreach (var workflow in await _workflowService.GetAllWorkflowsAsync())
                {
                    if (workflow.Sla is not null)
                        _slas[workflow.Id] = workflow.Sla;
                }
            }
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (_runService is null || SelectedRun is null)
            return;
        SelectedRun = await _runService.CancelRunAsync(SelectedRun.Id);
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Cancelled {SelectedRun.WorkflowName}"));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        if (_runService is null || SelectedRun is null)
            return;
        var retry = await _runService.RetryRunAsync(SelectedRun.Id);
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Retrying {retry.WorkflowName}"));
        await LoadAsync();
        SelectedRun = Runs.FirstOrDefault(r => r.Id == retry.Id);
    }

    private void ApplyFilters()
    {
        IEnumerable<WorkflowRun> query = _all;
        if (!string.Equals(StatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => r.Status.ToString().Equals(StatusFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            query = query.Where(r => r.WorkflowName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        var selectedId = SelectedRun?.Id;
        Runs = new ObservableCollection<WorkflowRun>(query.OrderByDescending(r => r.StartedAt));
        SelectedRun = selectedId is { } id ? Runs.FirstOrDefault(r => r.Id == id) : Runs.FirstOrDefault();
        RefreshSelectionMeta();
    }

    public async Task FocusRunAsync(Guid? id)
    {
        if (id is Guid)
        {
            StatusFilter = "All";
            SearchQuery = string.Empty;
        }

        await LoadAsync();
        if (id is not Guid runId)
            return;

        SelectedRun = Runs.FirstOrDefault(r => r.Id == runId)
            ?? _all.FirstOrDefault(r => r.Id == runId);
    }

    private void RefreshSelectionMeta()
    {
        RunLog = BuildRunLog(SelectedRun);
        SelectedRunExceedsSla = SelectedRun is not null && ExceedsSla(SelectedRun);
    }

    private bool ExceedsSla(WorkflowRun run)
    {
        if (!_slas.TryGetValue(run.WorkflowId, out var sla))
            return false;
        var duration = run.Duration ?? DateTime.UtcNow - run.StartedAt;
        return duration > sla.WarningThreshold;
    }

    private static string BuildRunLog(WorkflowRun? run)
    {
        if (run is null)
            return string.Empty;

        var log = new StringBuilder();
        log.AppendLine($"[{run.StartedAt:HH:mm:ss}] Run started ({run.TriggerType} by {run.TriggeredBy ?? "unknown"})");
        foreach (var task in run.TaskRuns.OrderBy(t => t.StartedAt ?? DateTime.MaxValue))
        {
            if (task.StartedAt is DateTime started)
                log.AppendLine($"[{started:HH:mm:ss}] Task {task.TaskName} started (attempt {task.AttemptNumber})");
            if (task.CompletedAt is DateTime completed)
            {
                var seconds = task.Duration?.TotalSeconds ?? 0;
                log.AppendLine($"[{completed:HH:mm:ss}] Task {task.TaskName} {task.Status.ToString().ToLowerInvariant()} in {seconds:F0}s  exit={task.ExitCode?.ToString() ?? "-"}");
            }
            else if (task.Status == RunStatus.Running)
                log.AppendLine($"[{DateTime.UtcNow:HH:mm:ss}] Task {task.TaskName} still running…");

            if (!string.IsNullOrWhiteSpace(task.Output))
                log.AppendLine($"                {task.Output}");
            if (!string.IsNullOrWhiteSpace(task.ErrorMessage))
                log.AppendLine($"                ERROR {task.ErrorMessage}");
        }

        if (run.CompletedAt is DateTime done)
            log.AppendLine($"[{done:HH:mm:ss}] Run {run.Status.ToString().ToLowerInvariant()}");
        else
            log.AppendLine($"[{DateTime.UtcNow:HH:mm:ss}] Run in progress ({run.Status})");

        if (!string.IsNullOrWhiteSpace(run.ErrorMessage))
            log.AppendLine($"                {run.ErrorMessage}");

        return log.ToString().TrimEnd();
    }

    private void LoadSample()
    {
        var now = DateTime.UtcNow;
        _all =
        [
            new WorkflowRun
            {
                Id = Guid.NewGuid(),
                WorkflowName = "Trade Capture Pipeline",
                Status = RunStatus.Success,
                StartedAt = now.AddMinutes(-8),
                CompletedAt = now.AddMinutes(-5),
                TriggeredBy = "scheduler",
                TriggerType = TriggerType.Scheduled,
                TaskRuns =
                {
                    new TaskRun { TaskName = "Prepare", Status = RunStatus.Success, StartedAt = now.AddMinutes(-8), CompletedAt = now.AddMinutes(-7) },
                    new TaskRun { TaskName = "Book", Status = RunStatus.Success, StartedAt = now.AddMinutes(-7), CompletedAt = now.AddMinutes(-5) }
                }
            }
        ];
        ApplyFilters();
    }
}
