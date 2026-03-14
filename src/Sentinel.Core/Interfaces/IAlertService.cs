using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

/// <summary>
/// Service for managing alerts and notifications.
/// </summary>
public interface IAlertService
{
    Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Alert>> GetAlertsByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<Alert?> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Alert> AcknowledgeAlertAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Alert> ResolveAlertAsync(Guid id, string resolution, CancellationToken cancellationToken = default);
    Task<IEnumerable<Alert>> GetAlertHistoryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for audit logging and compliance.
/// </summary>
public interface IAuditService
{
    Task<IEnumerable<AuditLogEntry>> GetAuditLogsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLogEntry>> GetAuditLogsByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLogEntry>> GetAuditLogsByUserAsync(string userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<Stream> ExportAuditLogsAsync(DateTime from, DateTime to, ExportFormat format, CancellationToken cancellationToken = default);
}

public enum ExportFormat
{
    Json,
    Ndjson,
    Csv
}
