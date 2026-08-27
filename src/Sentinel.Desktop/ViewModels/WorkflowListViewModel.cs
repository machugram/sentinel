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
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private string _editorTitle = "New workflow";
    [ObservableProperty] private string _draftName = string.Empty;
    [ObservableProperty] private string _draftDescription = string.Empty;
    [ObservableProperty] private string _draftRegion = "Americas";
    [ObservableProperty] private string _draftSchedule = "0 * * * *";
    [ObservableProperty] private string _draftStatus = "Active";
    [ObservableProperty] private string? _editorError;

    private Guid? _editingId;

    public IReadOnlyList<string> StatusOptions { get; } = new[] { "All", "Active", "Paused", "Draft", "Archived" };
    public IReadOnlyList<string> RegionOptions { get; } = new[] { "All", "Americas", "EMEA", "APAC", "Global" };
    public IReadOnlyList<string> EditorStatusOptions { get; } = new[] { "Active", "Draft", "Paused" };
    public IReadOnlyList<string> EditorRegionOptions { get; } = new[] { "Americas", "EMEA", "APAC", "Global" };
    public bool CanSaveWorkflow => !string.IsNullOrWhiteSpace(DraftName);

    partial void OnDraftNameChanged(string value)
    {
        EditorError = null;
        OnPropertyChanged(nameof(CanSaveWorkflow));
    }

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
        _editingId = null;
        EditorTitle = "New workflow";
        DraftName = string.Empty;
        DraftDescription = string.Empty;
        DraftRegion = "Americas";
        DraftSchedule = "0 * * * *";
        DraftStatus = "Active";
        EditorError = null;
        IsEditorOpen = true;
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
        _editingId = workflow.Id;
        EditorTitle = "Edit workflow";
        DraftName = workflow.Name;
        DraftDescription = string.Empty;
        DraftRegion = workflow.Region;
        DraftSchedule = workflow.Schedule == "—" ? string.Empty : workflow.Schedule;
        DraftStatus = workflow.Status.ToString();
        EditorError = null;
        IsEditorOpen = true;
        _ = LoadDescriptionAsync(workflow.Id);
    }

    [RelayCommand]
    private void CancelEditor()
    {
        IsEditorOpen = false;
        EditorError = null;
    }

    [RelayCommand]
    private async Task SaveWorkflowAsync()
    {
        if (string.IsNullOrWhiteSpace(DraftName))
        {
            EditorError = "Name is required.";
            return;
        }

        if (!Enum.TryParse<WorkflowStatus>(DraftStatus, out var status))
            status = WorkflowStatus.Active;

        var name = DraftName.Trim();
        var description = DraftDescription.Trim();
        var schedule = string.IsNullOrWhiteSpace(DraftSchedule) ? null : DraftSchedule.Trim();
        var region = string.IsNullOrWhiteSpace(DraftRegion) ? "Global" : DraftRegion;

        if (_workflowService is null)
        {
            if (_editingId is Guid existingId)
            {
                var index = _all.FindIndex(w => w.Id == existingId);
                if (index >= 0)
                    _all[index] = _all[index] with { Name = name, Status = status, Region = region, Schedule = schedule ?? "—" };
            }
            else
            {
                _all.Insert(0, new WorkflowListItem(Guid.NewGuid(), name, status, region, 0, 100, schedule ?? "—"));
            }

            FinishEditor(name);
            ApplyFilters();
            return;
        }

        if (_editingId is Guid id)
        {
            var existing = await _workflowService.GetWorkflowByIdAsync(id);
            if (existing is null)
            {
                EditorError = "Workflow was not found.";
                return;
            }

            existing.Name = name;
            existing.Description = description;
            existing.Status = status;
            existing.CronExpression = schedule;
            existing.Metadata["Region"] = region;
            await _workflowService.UpdateWorkflowAsync(existing);
            WeakReferenceMessenger.Default.Send(new StatusMessage($"Updated {name}"));
        }
        else
        {
            var workflow = new Workflow
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? $"Created from desktop by Alex Chen" : description,
                Status = status,
                CronExpression = schedule,
                Metadata = new Dictionary<string, string> { ["Region"] = region },
                Sla = new WorkflowSla
                {
                    ExpectedDuration = TimeSpan.FromMinutes(12),
                    WarningThreshold = TimeSpan.FromMinutes(10),
                    CriticalThreshold = TimeSpan.FromMinutes(15),
                    NotificationChannel = "ops-trading"
                },
                Tasks =
                {
                    new WorkflowTask { Id = Guid.NewGuid(), Name = "Prepare", Type = TaskType.Shell, Command = "prepare.sh", Status = Core.Models.TaskStatus.Pending, XPosition = 40, YPosition = 80 },
                    new WorkflowTask { Id = Guid.NewGuid(), Name = "Run", Type = TaskType.Shell, Command = "run.sh", Status = Core.Models.TaskStatus.Pending, XPosition = 240, YPosition = 80 },
                    new WorkflowTask { Id = Guid.NewGuid(), Name = "Publish", Type = TaskType.Http, Command = "POST /publish", Status = Core.Models.TaskStatus.Pending, XPosition = 440, YPosition = 80 }
                }
            };
            await _workflowService.CreateWorkflowAsync(workflow);
            WeakReferenceMessenger.Default.Send(new StatusMessage($"Created {name}"));
        }

        FinishEditor(name);
        await LoadAsync();
        SelectedWorkflow = Workflows.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private void FinishEditor(string name)
    {
        SearchQuery = string.Empty;
        StatusFilter = "All";
        RegionFilter = "All";
        IsEditorOpen = false;
        EditorError = null;
        _editingId = null;
    }

    private async Task LoadDescriptionAsync(Guid id)
    {
        if (_workflowService is null)
            return;
        var existing = await _workflowService.GetWorkflowByIdAsync(id);
        if (existing is not null && IsEditorOpen && _editingId == id)
            DraftDescription = existing.Description;
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
