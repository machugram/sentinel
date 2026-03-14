namespace Sentinel.Core.Models;

/// <summary>
/// Represents a single execution run of a workflow.
/// </summary>
public class WorkflowRun
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public RunStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
    public string? TriggeredBy { get; set; }
    public TriggerType TriggerType { get; set; }
    public List<TaskRun> TaskRuns { get; set; } = new();
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents the execution of a single task within a workflow run.
/// </summary>
public class TaskRun
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public RunStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => StartedAt.HasValue && CompletedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
    public int AttemptNumber { get; set; } = 1;
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ExitCode { get; set; }
}

public enum RunStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Cancelled,
    TimedOut
}

public enum TriggerType
{
    Manual,
    Scheduled,
    FileEvent,
    Webhook,
    KafkaMessage,
    Dependency
}
