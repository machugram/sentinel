using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;

namespace Sentinel.Desktop.ViewModels;

public partial class WorkflowListViewModel : ViewModelBase
{
    private readonly IWorkflowService? _workflowService;
    private readonly IWorkflowRunService? _runService;
    private List<WorkflowListItem> _all = new();

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private string _regionFilter = "All";
    [ObservableProperty] private ObservableCollection<WorkflowListItem> _workflows = new();
    [ObservableProperty] private WorkflowListItem? _selectedWorkflow;
    [ObservableProperty] private bool _isLoading;

    public IReadOnlyList<string> StatusOptions { get; } = new[] { "All", "Active", "Paused", "Draft", "Archived" };
    public IReadOnlyList<string> RegionOptions { get; } = new[] { "All", "Americas", "EMEA", "APAC", "Global" };

    public WorkflowListViewModel()
    {
        _all =
        [
            new(Guid.NewGuid(), "Trade Capture Pipeline", WorkflowStatus.Active, "Americas", 156, 98.5, "*/15 * * * *"),
            new(Guid.NewGuid(), "EOD Risk Calculation", WorkflowStatus.Active, "Global", 89, 99.1, "0 17 * * 1-5"),
            new(Guid.NewGuid(), "DTCC Regulatory Report", WorkflowStatus.Active, "Americas", 234, 97.8, "0 6 * * 1-5"),
            new(Guid.NewGuid(), "Market Data Reconciliation", WorkflowStatus.Active, "APAC", 112, 95.2, "0 8 * * *"),
            new(Guid.NewGuid(), "NAV Calculation", WorkflowStatus.Active, "EMEA", 67, 99.5, "0 18 * * 1-5"),
            new(Guid.NewGuid(), "Surveillance Daily", WorkflowStatus.Active, "Global", 45, 100.0, "0 0 * * *"),
            new(Guid.NewGuid(), "Client Reporting", WorkflowStatus.Paused, "Americas", 23, 98.0, "0 9 1 * *"),
            new(Guid.NewGuid(), "Margin Calculation", WorkflowStatus.Active, "EMEA", 178, 97.2, "0 16 * * 1-5"),
        ];
        ApplyFilters();
    }

    public WorkflowListViewModel(IWorkflowService workflowService, IWorkflowRunService runService)
    {
        _workflowService = workflowService;
        _runService = runService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_workflowService is null || _runService is null)
            return;

        IsLoading = true;
        try
        {
            var workflows = (await _workflowService.GetAllWorkflowsAsync()).ToList();
            var runs = (await _runService.GetRecentRunsAsync(200)).ToList();

            _all = workflows.Select(w =>
            {
                var wfRuns = runs.Where(r => r.WorkflowId == w.Id).ToList();
                var completed = wfRuns.Where(r => r.Status is RunStatus.Success or RunStatus.Failed).ToList();
                var rate = completed.Count == 0 ? 100.0 : completed.Count(r => r.Status == RunStatus.Success) * 100.0 / completed.Count;
                var region = w.Metadata.TryGetValue("Region", out var value) ? value : "Global";
                return new WorkflowListItem(
                    w.Id,
                    w.Name,
                    w.Status,
                    region,
                    wfRuns.Count,
                    Math.Round(rate, 1),
                    w.CronExpression ?? "—");
            }).ToList();
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnStatusFilterChanged(string value) => ApplyFilters();
    partial void OnRegionFilterChanged(string value) => ApplyFilters();

    [RelayCommand]
    private void CreateWorkflow()
    {
        WeakReferenceMessenger.Default.Send(new StatusMessage("Workflow designer is scheduled for a later milestone."));
    }

    [RelayCommand]
    private async Task TriggerWorkflow(WorkflowListItem? workflow)
    {
        if (workflow is null || _workflowService is null)
            return;

        await _workflowService.TriggerWorkflowAsync(workflow.Id);
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Triggered {workflow.Name}"));
        await LoadAsync();
    }

    [RelayCommand]
    private void EditWorkflow(WorkflowListItem? workflow)
    {
        if (workflow is null)
            return;
        SelectedWorkflow = workflow;
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Opened {workflow.Name} (read-only in v0.1)"));
    }

    private void ApplyFilters()
    {
        IEnumerable<WorkflowListItem> query = _all;
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            query = query.Where(w => w.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        if (!string.Equals(StatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(w => w.Status.ToString().Equals(StatusFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.Equals(RegionFilter, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(w => w.Region.Equals(RegionFilter, StringComparison.OrdinalIgnoreCase));

        Workflows = new ObservableCollection<WorkflowListItem>(query);
    }
}

public record WorkflowListItem(
    Guid Id,
    string Name,
    WorkflowStatus Status,
    string Region,
    int TotalRuns,
    double SuccessRate,
    string Schedule)
{
    public string StatusText => Status.ToString();
    public string SuccessRateText => $"{SuccessRate:F1}%";
}
