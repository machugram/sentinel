using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Infrastructure.Mock;

public sealed class MockWorkflowService : IWorkflowService
{
    private readonly MockDataStore _store;

    public MockWorkflowService(MockDataStore store)
    {
        _store = store;
    }

    public Task<IEnumerable<Workflow>> GetAllWorkflowsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Workflow>>(_store.Workflows.ToList());

    public Task<Workflow?> GetWorkflowByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Workflows.FirstOrDefault(w => w.Id == id));

    public Task<Workflow> CreateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        if (workflow.Id == Guid.Empty)
            workflow.Id = Guid.NewGuid();
        workflow.CreatedAt = DateTime.UtcNow;
        _store.Workflows.Add(workflow);
        _store.AuditLogs.Insert(0, new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Action = "workflow.create",
            EntityType = "Workflow",
            EntityId = workflow.Id,
            UserId = "operator-1",
            UserName = "Alex Chen",
            NewValue = workflow.Name
        });
        _store.Save();
        return Task.FromResult(workflow);
    }

    public Task<Workflow> UpdateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var existing = _store.Workflows.FirstOrDefault(w => w.Id == workflow.Id)
            ?? throw new InvalidOperationException($"Workflow {workflow.Id} not found");
        var index = _store.Workflows.IndexOf(existing);
        _store.Workflows[index] = workflow;
        _store.Save();
        return Task.FromResult(workflow);
    }

    public Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = _store.Workflows.FirstOrDefault(w => w.Id == id);
        if (existing != null)
        {
            _store.Workflows.Remove(existing);
            _store.Save();
        }
        return Task.CompletedTask;
    }

    public Task<WorkflowRun> TriggerWorkflowAsync(Guid workflowId, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default)
    {
        var workflow = _store.Workflows.FirstOrDefault(w => w.Id == workflowId)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found");

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            Status = RunStatus.Running,
            StartedAt = DateTime.UtcNow,
            TriggerType = TriggerType.Manual,
            TriggeredBy = "Alex Chen",
            Parameters = parameters ?? new Dictionary<string, string>(),
            TaskRuns = workflow.Tasks.Select((task, index) => new TaskRun
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                TaskName = task.Name,
                Status = index == 0 ? RunStatus.Running : RunStatus.Pending,
                StartedAt = index == 0 ? DateTime.UtcNow : null,
                AttemptNumber = 1
            }).ToList()
        };

        _store.Runs.Insert(0, run);
        workflow.LastRunAt = run.StartedAt;
        _store.AuditLogs.Insert(0, new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Action = "workflow.trigger",
            EntityType = "Workflow",
            EntityId = workflow.Id,
            UserId = "operator-1",
            UserName = "Alex Chen",
            NewValue = "manual trigger"
        });
        _store.Save();

        return Task.FromResult(run);
    }

    public Task<IEnumerable<Workflow>> SearchWorkflowsAsync(string query, CancellationToken cancellationToken = default)
    {
        var result = string.IsNullOrWhiteSpace(query)
            ? _store.Workflows
            : _store.Workflows.Where(w =>
                w.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)));

        return Task.FromResult(result.ToList().AsEnumerable());
    }
}

public sealed class MockWorkflowRunService : IWorkflowRunService
{
    private readonly MockDataStore _store;

    public MockWorkflowRunService(MockDataStore store)
    {
        _store = store;
    }

    public Task<IEnumerable<WorkflowRun>> GetRecentRunsAsync(int count = 50, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<WorkflowRun>>(_store.Runs.OrderByDescending(r => r.StartedAt).Take(count).ToList());

    public Task<IEnumerable<WorkflowRun>> GetRunsByWorkflowIdAsync(Guid workflowId, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<WorkflowRun>>(_store.Runs.Where(r => r.WorkflowId == workflowId).OrderByDescending(r => r.StartedAt).ToList());

    public Task<WorkflowRun?> GetRunByIdAsync(Guid runId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Runs.FirstOrDefault(r => r.Id == runId));

    public Task<WorkflowRun> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = RequireRun(runId);
        run.Status = RunStatus.Cancelled;
        run.CompletedAt = DateTime.UtcNow;
        foreach (var task in run.TaskRuns.Where(t => t.Status is RunStatus.Running or RunStatus.Pending))
        {
            task.Status = RunStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
        }
        _store.Save();
        return Task.FromResult(run);
    }

    public Task<WorkflowRun> RetryRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var source = RequireRun(runId);
        var retry = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = source.WorkflowId,
            WorkflowName = source.WorkflowName,
            Status = RunStatus.Running,
            StartedAt = DateTime.UtcNow,
            TriggerType = TriggerType.Manual,
            TriggeredBy = "Alex Chen",
            Parameters = new Dictionary<string, string>(source.Parameters),
            TaskRuns = source.TaskRuns.Select(t => new TaskRun
            {
                Id = Guid.NewGuid(),
                TaskId = t.TaskId,
                TaskName = t.TaskName,
                Status = RunStatus.Pending,
                AttemptNumber = t.AttemptNumber + 1
            }).ToList()
        };
        if (retry.TaskRuns.Count > 0)
        {
            retry.TaskRuns[0].Status = RunStatus.Running;
            retry.TaskRuns[0].StartedAt = DateTime.UtcNow;
        }
        _store.Runs.Insert(0, retry);
        _store.Save();
        return Task.FromResult(retry);
    }

    public Task<IEnumerable<WorkflowRun>> GetActiveRunsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<WorkflowRun>>(_store.Runs.Where(r => r.Status is RunStatus.Running or RunStatus.Pending).ToList());

    private WorkflowRun RequireRun(Guid runId)
        => _store.Runs.FirstOrDefault(r => r.Id == runId)
           ?? throw new InvalidOperationException($"Run {runId} not found");
}
