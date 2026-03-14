namespace Sentinel.Core.Models;

/// <summary>
/// Represents a workflow in the Sentinel Orchestrator.
/// Based on PRD §5.1 - DAG-based workflows with tasks, dependencies, retries, SLAs.
/// </summary>
public class Workflow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? CronExpression { get; set; }
    public List<WorkflowTask> Tasks { get; set; } = new();
    public List<TaskDependency> Dependencies { get; set; } = new();
    public WorkflowSla? Sla { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a single task within a workflow DAG.
/// </summary>
public class WorkflowTask
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TaskType Type { get; set; }
    public TaskStatus Status { get; set; }
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public int RetryCount { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan? Timeout { get; set; }
    public int XPosition { get; set; }
    public int YPosition { get; set; }
}

/// <summary>
/// Represents a dependency between two tasks in the DAG.
/// </summary>
public class TaskDependency
{
    public Guid FromTaskId { get; set; }
    public Guid ToTaskId { get; set; }
    public DependencyCondition Condition { get; set; } = DependencyCondition.Success;
}

/// <summary>
/// SLA configuration for workflow completion.
/// </summary>
public class WorkflowSla
{
    public TimeSpan ExpectedDuration { get; set; }
    public TimeSpan WarningThreshold { get; set; }
    public TimeSpan CriticalThreshold { get; set; }
    public string? NotificationChannel { get; set; }
}

public enum WorkflowStatus
{
    Draft,
    Active,
    Paused,
    Archived,
    Failed
}

public enum TaskType
{
    Shell,
    Docker,
    Http,
    Kafka,
    Python,
    Custom
}

public enum TaskStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Skipped,
    Cancelled
}

public enum DependencyCondition
{
    Success,
    Failure,
    Always,
    Complete
}
