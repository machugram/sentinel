using System.Collections.ObjectModel;
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
    private List<WorkflowRun> _all = new();

    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private ObservableCollection<WorkflowRun> _runs = new();
    [ObservableProperty] private WorkflowRun? _selectedRun;
    [ObservableProperty] private bool _isLoading;

    public IReadOnlyList<string> StatusOptions { get; } =
        new[] { "All", "Running", "Success", "Failed", "Pending", "Cancelled", "TimedOut" };

    public bool HasSelection => SelectedRun is not null;
    public bool CanRetry => SelectedRun is { Status: RunStatus.Failed or RunStatus.TimedOut or RunStatus.Cancelled };
    public bool CanCancel => SelectedRun is { Status: RunStatus.Running or RunStatus.Pending };

    public RunsViewModel()
    {
        LoadSample();
    }

    public RunsViewModel(IWorkflowRunService runService)
    {
        _runService = runService;
        _ = LoadAsync();
    }

    partial void OnStatusFilterChanged(string value) => ApplyFilters();
    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedRunChanged(WorkflowRun? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(CanCancel));
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
