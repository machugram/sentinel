using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;
using Sentinel.Desktop.Services;

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
    [ObservableProperty] private string _draftScheduleHuman = "Every hour";
    [ObservableProperty] private string _schedulePreview = "Runs every hour  ·  0 * * * *";
    [ObservableProperty] private bool _scheduleIsValid = true;
    [ObservableProperty] private string _draftStatus = "Active";
    [ObservableProperty] private string? _editorError;
    [ObservableProperty] private ObservableCollection<WorkflowParameterItem> _draftParameters = new();
    [ObservableProperty] private ObservableCollection<WorkflowTaskDraft> _draftTasks = new();
    [ObservableProperty] private decimal? _draftSlaExpectedMinutes = 12;
    [ObservableProperty] private decimal? _draftSlaWarningMinutes = 10;

    private Guid? _editingId;
    private bool _suppressScheduleSync;

    public IReadOnlyList<string> StatusOptions { get; } = new[] { "All", "Active", "Paused", "Draft", "Archived" };
    public IReadOnlyList<string> RegionOptions { get; } = new[] { "All", "Americas", "EMEA", "APAC", "Global" };
    public IReadOnlyList<string> EditorStatusOptions { get; } = new[] { "Active", "Draft", "Paused", "Archived" };
    public IReadOnlyList<string> EditorRegionOptions { get; } = new[] { "Americas", "EMEA", "APAC", "Global" };
    public IReadOnlyList<string> SchedulePresets { get; } =
    [
        "Every 15 minutes",
        "Every 30 minutes",
        "Every hour",
        "Weekdays at 5:00 PM",
        "Weekdays at 6:00 AM",
        "Daily at 6:00 AM",
        "Daily at midnight",
        "First of month at 9:00 AM",
        "Manual"
    ];
    public IReadOnlyList<string> TaskTypes { get; } = ["Shell", "Docker", "Http", "Python", "Kafka", "Custom"];
    public bool CanSaveWorkflow => !string.IsNullOrWhiteSpace(DraftName);
    public bool HasWorkflows => Workflows.Count > 0;
    public string EmptyMessage =>
        StatusFilter != "All" || RegionFilter != "All" || !string.IsNullOrWhiteSpace(SearchQuery)
            ? "No workflows match these filters."
            : "No workflows yet. Create one or import JIL drafts.";

    partial void OnDraftNameChanged(string value)
    {
        EditorError = null;
        OnPropertyChanged(nameof(CanSaveWorkflow));
    }

    partial void OnDraftScheduleHumanChanged(string value) => RefreshSchedulePreview();

    public WorkflowListViewModel()
    {
        _all =
        [
            new(Guid.NewGuid(), "Trade Capture Pipeline", WorkflowStatus.Active, "Americas", 156, 98.5, "*/15 * * * *", 12),
            new(Guid.NewGuid(), "EOD Risk Calculation", WorkflowStatus.Active, "Global", 89, 99.1, "0 17 * * 1-5", 45),
            new(Guid.NewGuid(), "DTCC Regulatory Report", WorkflowStatus.Active, "Americas", 234, 97.8, "0 6 * * 1-5", 20),
            new(Guid.NewGuid(), "Market Data Reconciliation", WorkflowStatus.Active, "APAC", 112, 95.2, "0 8 * * *", 15),
            new(Guid.NewGuid(), "NAV Calculation", WorkflowStatus.Active, "EMEA", 67, 99.5, "0 18 * * 1-5", 25),
            new(Guid.NewGuid(), "Surveillance Daily", WorkflowStatus.Active, "Global", 45, 100.0, "0 0 * * *", 10),
            new(Guid.NewGuid(), "Client Reporting", WorkflowStatus.Paused, "Americas", 23, 98.0, "0 9 1 * *", 30),
            new(Guid.NewGuid(), "Margin Calculation", WorkflowStatus.Active, "EMEA", 178, 97.2, "0 16 * * 1-5", 18),
        ];
        ApplyFilters();
    }

    public WorkflowListViewModel(IWorkflowService workflowService, IWorkflowRunService runService)
    {
        _workflowService = workflowService;
        _runService = runService;
        _ = LoadAsync();
    }

    public void OpenCreate() => CreateWorkflow();

    public async Task ApplyIncomingAsync(Guid? id, string? filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
            StatusFilter = filter;
        await LoadAsync();
        if (id is not Guid workflowId)
            return;
        SelectedWorkflow = Workflows.FirstOrDefault(w => w.Id == workflowId)
            ?? _all.FirstOrDefault(w => w.Id == workflowId);
    }

    public void RefreshQuiet() => _ = LoadAsync();

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
                    w.CronExpression ?? string.Empty,
                    w.Sla is null ? null : (int)Math.Round(w.Sla.ExpectedDuration.TotalMinutes));
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
        SetScheduleFromCron("0 * * * *");
        DraftStatus = "Active";
        EditorError = null;
        DraftSlaExpectedMinutes = 12;
        DraftSlaWarningMinutes = 10;
        ResetParameters();
        ResetTasks();
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void TriggerWorkflow(WorkflowListItem? workflow)
    {
        if (workflow is null || _workflowService is null)
            return;

        WeakReferenceMessenger.Default.Send(new ConfirmRequest(
            "Run workflow",
            $"Trigger “{workflow.Name}” now?",
            "Run now",
            false,
            () => _ = TriggerConfirmedAsync(workflow)));
    }

    private async Task TriggerConfirmedAsync(WorkflowListItem workflow)
    {
        if (_workflowService is null)
            return;
        var run = await _workflowService.TriggerWorkflowAsync(workflow.Id);
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Triggered {workflow.Name}"));
        WeakReferenceMessenger.Default.Send(new NavigateRequest("Runs", run.Id));
    }

    [RelayCommand]
    private void ViewRuns(WorkflowListItem? workflow)
    {
        if (workflow is null)
            return;
        WeakReferenceMessenger.Default.Send(new NavigateRequest("Runs", Filter: workflow.Name));
    }

    [RelayCommand]
    private void DuplicateWorkflow(WorkflowListItem? workflow)
    {
        if (workflow is null)
            return;
        _ = DuplicateAsync(workflow);
    }

    private async Task DuplicateAsync(WorkflowListItem item)
    {
        if (_workflowService is null)
        {
            _all.Insert(0, item with { Id = Guid.NewGuid(), Name = item.Name + " (copy)", Status = WorkflowStatus.Draft, TotalRuns = 0 });
            ApplyFilters();
            WeakReferenceMessenger.Default.Send(new StatusMessage($"Duplicated {item.Name}"));
            return;
        }

        var source = await _workflowService.GetWorkflowByIdAsync(item.Id);
        if (source is null)
            return;

        var copy = new Workflow
        {
            Name = source.Name + " (copy)",
            Description = source.Description,
            Status = WorkflowStatus.Draft,
            CronExpression = source.CronExpression,
            Metadata = new Dictionary<string, string>(source.Metadata),
            Sla = source.Sla,
            Tags = source.Tags.ToArray(),
            Tasks = source.Tasks.Select(t => new WorkflowTask
            {
                Id = Guid.NewGuid(),
                Name = t.Name,
                Type = t.Type,
                Command = t.Command,
                Parameters = new Dictionary<string, string>(t.Parameters),
                RetryCount = t.RetryCount,
                XPosition = t.XPosition,
                YPosition = t.YPosition
            }).ToList()
        };
        copy.Dependencies = SequentialDependencies(copy.Tasks);
        await _workflowService.CreateWorkflowAsync(copy);
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Duplicated {item.Name}"));
        await LoadAsync();
        SelectedWorkflow = Workflows.FirstOrDefault(w => w.Id == copy.Id);
    }

    [RelayCommand]
    private void PauseOrResume(WorkflowListItem? workflow)
    {
        if (workflow is null)
            return;
        var next = workflow.Status == WorkflowStatus.Paused ? WorkflowStatus.Active : WorkflowStatus.Paused;
        _ = SetStatusAsync(workflow, next);
    }

    [RelayCommand]
    private void ArchiveWorkflow(WorkflowListItem? workflow)
    {
        if (workflow is null)
            return;
        WeakReferenceMessenger.Default.Send(new ConfirmRequest(
            "Archive workflow",
            $"Archive “{workflow.Name}”? It will stop scheduling until you activate it again.",
            "Archive",
            false,
            () => _ = SetStatusAsync(workflow, WorkflowStatus.Archived)));
    }

    [RelayCommand]
    private void DeleteWorkflow(WorkflowListItem? workflow)
    {
        if (workflow is null)
            return;
        WeakReferenceMessenger.Default.Send(new ConfirmRequest(
            "Delete workflow",
            $"Delete “{workflow.Name}”? This cannot be undone in the mock catalog.",
            "Delete",
            true,
            () => _ = DeleteConfirmedAsync(workflow)));
    }

    private async Task SetStatusAsync(WorkflowListItem item, WorkflowStatus status)
    {
        if (_workflowService is null)
        {
            var index = _all.FindIndex(w => w.Id == item.Id);
            if (index >= 0)
                _all[index] = _all[index] with { Status = status };
            ApplyFilters();
            WeakReferenceMessenger.Default.Send(new StatusMessage($"{item.Name} is {status}"));
            return;
        }

        var existing = await _workflowService.GetWorkflowByIdAsync(item.Id);
        if (existing is null)
            return;
        existing.Status = status;
        await _workflowService.UpdateWorkflowAsync(existing);
        WeakReferenceMessenger.Default.Send(new StatusMessage($"{item.Name} is {status}"));
        await LoadAsync();
    }

    private async Task DeleteConfirmedAsync(WorkflowListItem item)
    {
        if (_workflowService is null)
        {
            _all.RemoveAll(w => w.Id == item.Id);
            ApplyFilters();
            WeakReferenceMessenger.Default.Send(new StatusMessage($"Deleted {item.Name}"));
            return;
        }

        await _workflowService.DeleteWorkflowAsync(item.Id);
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Deleted {item.Name}"));
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
        SetScheduleFromCron(workflow.Schedule);
        DraftStatus = workflow.Status.ToString();
        EditorError = null;
        ResetParameters();
        ResetTasks();
        DraftSlaExpectedMinutes = workflow.SlaMinutes ?? 12;
        DraftSlaWarningMinutes = Math.Max(1, (workflow.SlaMinutes ?? 12) - 2);
        IsEditorOpen = true;
        _ = LoadEditorDetailsAsync(workflow.Id);
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
        var region = string.IsNullOrWhiteSpace(DraftRegion) ? "Global" : DraftRegion;
        RefreshSchedulePreview();
        if (!ScheduleIsValid)
        {
            EditorError = "Could not understand that schedule. Try “every 15 minutes”, “weekdays at 5pm”, or a cron expression.";
            return;
        }

        var schedule = string.IsNullOrWhiteSpace(DraftSchedule) ? null : DraftSchedule.Trim();
        var parameters = CollectParameters();
        var tasks = BuildTasks(null, parameters);
        if (tasks.Count == 0)
        {
            EditorError = "Add at least one named task.";
            return;
        }

        var sla = BuildSla();

        if (_workflowService is null)
        {
            if (_editingId is Guid existingId)
            {
                var index = _all.FindIndex(w => w.Id == existingId);
                if (index >= 0)
                    _all[index] = _all[index] with { Name = name, Status = status, Region = region, Schedule = schedule ?? string.Empty, SlaMinutes = (int?)DraftSlaExpectedMinutes };
            }
            else
            {
                _all.Insert(0, new WorkflowListItem(Guid.NewGuid(), name, status, region, 0, 100, schedule ?? string.Empty, (int?)DraftSlaExpectedMinutes));
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
            existing.Sla = sla;
            existing.Tasks = BuildTasks(existing.Tasks, parameters);
            existing.Dependencies = SequentialDependencies(existing.Tasks);
            ApplyParameters(existing, region, parameters);
            await _workflowService.UpdateWorkflowAsync(existing);
            WeakReferenceMessenger.Default.Send(new StatusMessage($"Updated {name}"));
        }
        else
        {
            var workflow = new Workflow
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? "Created from desktop by Alex Chen" : description,
                Status = status,
                CronExpression = schedule,
                Metadata = BuildMetadata(region, parameters),
                Sla = sla,
                Tasks = tasks,
                Dependencies = SequentialDependencies(tasks)
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

    [RelayCommand]
    private void ApplySchedulePreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return;
        DraftScheduleHuman = preset;
    }

    [RelayCommand]
    private void AddParameter()
    {
        DraftParameters.Add(new WorkflowParameterItem());
    }

    [RelayCommand]
    private void RemoveParameter(WorkflowParameterItem? item)
    {
        if (item is null)
            return;
        DraftParameters.Remove(item);
        if (DraftParameters.Count == 0)
            DraftParameters.Add(new WorkflowParameterItem());
    }

    [RelayCommand]
    private void AddTask()
    {
        DraftTasks.Add(new WorkflowTaskDraft { Type = "Shell" });
    }

    [RelayCommand]
    private void RemoveTask(WorkflowTaskDraft? item)
    {
        if (item is null)
            return;
        DraftTasks.Remove(item);
        if (DraftTasks.Count == 0)
            DraftTasks.Add(new WorkflowTaskDraft { Type = "Shell" });
    }

    private void RefreshSchedulePreview()
    {
        if (_suppressScheduleSync)
            return;

        if (ScheduleText.TryParse(DraftScheduleHuman, out var cron, out var human))
        {
            DraftSchedule = cron;
            ScheduleIsValid = true;
            SchedulePreview = string.IsNullOrEmpty(cron)
                ? "Runs only when triggered manually"
                : $"{human}  ·  {cron}";
            if (EditorError?.Contains("schedule", StringComparison.OrdinalIgnoreCase) == true)
                EditorError = null;
        }
        else
        {
            ScheduleIsValid = false;
            SchedulePreview = "Could not understand that schedule. Try “every 15 minutes” or a cron expression.";
        }
    }

    private void SetScheduleFromCron(string? cron)
    {
        _suppressScheduleSync = true;
        DraftSchedule = cron ?? string.Empty;
        DraftScheduleHuman = ScheduleText.ToHuman(cron);
        _suppressScheduleSync = false;
        RefreshSchedulePreview();
        if (!string.IsNullOrEmpty(cron) && !ScheduleIsValid)
        {
            DraftScheduleHuman = cron;
            RefreshSchedulePreview();
        }
    }

    private void ResetParameters()
    {
        DraftParameters = new ObservableCollection<WorkflowParameterItem>
        {
            new(),
            new()
        };
    }

    private void ResetTasks()
    {
        DraftTasks = new ObservableCollection<WorkflowTaskDraft>
        {
            new() { Name = "Prepare", Type = "Shell", Command = "prepare.sh" },
            new() { Name = "Run", Type = "Shell", Command = "run.sh" },
            new() { Name = "Publish", Type = "Http", Command = "POST /publish" }
        };
    }

    private WorkflowSla BuildSla()
    {
        var expected = (int)(DraftSlaExpectedMinutes ?? 12);
        var warning = (int)(DraftSlaWarningMinutes ?? Math.Max(1, expected - 2));
        return new WorkflowSla
        {
            ExpectedDuration = TimeSpan.FromMinutes(Math.Max(1, expected)),
            WarningThreshold = TimeSpan.FromMinutes(Math.Max(1, warning)),
            CriticalThreshold = TimeSpan.FromMinutes(Math.Max(expected + 3, warning + 5)),
            NotificationChannel = "ops-trading"
        };
    }

    private List<WorkflowTask> BuildTasks(IEnumerable<WorkflowTask>? existing, Dictionary<string, string> parameters)
    {
        var existingById = existing?.ToDictionary(t => t.Id) ?? new Dictionary<Guid, WorkflowTask>();
        var tasks = new List<WorkflowTask>();
        var index = 0;
        foreach (var draft in DraftTasks.Where(t => !string.IsNullOrWhiteSpace(t.Name)))
        {
            if (!Enum.TryParse<TaskType>(draft.Type, true, out var type))
                type = TaskType.Shell;

            if (draft.Id == Guid.Empty || !existingById.TryGetValue(draft.Id, out var task))
                task = new WorkflowTask { Id = draft.Id == Guid.Empty ? Guid.NewGuid() : draft.Id };

            task.Name = draft.Name.Trim();
            task.Type = type;
            task.Command = draft.Command?.Trim() ?? string.Empty;
            task.Status = Core.Models.TaskStatus.Pending;
            task.XPosition = 40 + index * 200;
            task.YPosition = 80;
            foreach (var pair in parameters)
                task.Parameters[pair.Key] = pair.Value;
            tasks.Add(task);
            index++;
        }

        return tasks;
    }

    private static List<TaskDependency> SequentialDependencies(IReadOnlyList<WorkflowTask> tasks)
    {
        var dependencies = new List<TaskDependency>();
        for (var i = 1; i < tasks.Count; i++)
        {
            dependencies.Add(new TaskDependency
            {
                FromTaskId = tasks[i - 1].Id,
                ToTaskId = tasks[i].Id,
                Condition = DependencyCondition.Success
            });
        }

        return dependencies;
    }

    private Dictionary<string, string> CollectParameters()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in DraftParameters)
        {
            if (string.IsNullOrWhiteSpace(row.Key))
                continue;
            result[row.Key.Trim()] = row.Value?.Trim() ?? string.Empty;
        }
        return result;
    }

    private static Dictionary<string, string> BuildMetadata(string region, Dictionary<string, string> parameters)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Region"] = region
        };
        foreach (var pair in parameters)
            metadata[pair.Key] = pair.Value;
        return metadata;
    }

    private static void ApplyParameters(Workflow workflow, string region, Dictionary<string, string> parameters)
    {
        workflow.Metadata = BuildMetadata(region, parameters);
        foreach (var task in workflow.Tasks)
        {
            foreach (var pair in parameters)
                task.Parameters[pair.Key] = pair.Value;
        }
    }

    private async Task LoadEditorDetailsAsync(Guid id)
    {
        if (_workflowService is null)
            return;
        var existing = await _workflowService.GetWorkflowByIdAsync(id);
        if (existing is null || !IsEditorOpen || _editingId != id)
            return;

        DraftDescription = existing.Description;
        if (existing.Sla is not null)
        {
            DraftSlaExpectedMinutes = (decimal)Math.Round(existing.Sla.ExpectedDuration.TotalMinutes);
            DraftSlaWarningMinutes = (decimal)Math.Round(existing.Sla.WarningThreshold.TotalMinutes);
        }
        var rows = existing.Metadata
            .Where(kv => !kv.Key.Equals("Region", StringComparison.OrdinalIgnoreCase))
            .Select(kv => new WorkflowParameterItem { Key = kv.Key, Value = kv.Value })
            .ToList();
        if (rows.Count == 0)
            rows.Add(new WorkflowParameterItem());
        DraftParameters = new ObservableCollection<WorkflowParameterItem>(rows);

        var taskRows = existing.Tasks
            .Select(t => new WorkflowTaskDraft
            {
                Id = t.Id,
                Name = t.Name,
                Type = t.Type.ToString(),
                Command = t.Command
            })
            .ToList();
        if (taskRows.Count == 0)
            ResetTasks();
        else
            DraftTasks = new ObservableCollection<WorkflowTaskDraft>(taskRows);
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
        OnPropertyChanged(nameof(HasWorkflows));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}

public record WorkflowListItem(
    Guid Id,
    string Name,
    WorkflowStatus Status,
    string Region,
    int TotalRuns,
    double SuccessRate,
    string Schedule,
    int? SlaMinutes = null)
{
    public string StatusText => Status.ToString();
    public string SuccessRateText => $"{SuccessRate:F1}%";
    public string ScheduleDisplay => ScheduleText.ToHuman(Schedule);
    public string SlaDisplay => SlaMinutes is int minutes ? $"{minutes}m" : "—";
    public string PauseResumeLabel => Status == WorkflowStatus.Paused ? "Resume" : "Pause";
}

public partial class WorkflowParameterItem : ObservableObject
{
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
}

public partial class WorkflowTaskDraft : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _type = "Shell";
    [ObservableProperty] private string _command = string.Empty;
}
