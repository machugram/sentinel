namespace Sentinel.Core.Models;

/// <summary>
/// Represents an alert in the system (SLA breaches, anomalies, failures).
/// Based on PRD §5.4 - Observability & AI Assist.
/// </summary>
public class Alert
{
    public Guid Id { get; set; }
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public Guid? WorkflowId { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public Guid? TaskId { get; set; }
    public Dictionary<string, string> Context { get; set; } = new();
    public string? AiSuggestion { get; set; }
}

/// <summary>
/// Represents an audit log entry for compliance tracking.
/// Based on PRD §6 NFR - Compliance requirements.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum AlertType
{
    SlaBreach,
    TaskFailure,
    AnomalyDetected,
    SystemError,
    SecurityEvent,
    MaintenanceRequired
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
