using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Infrastructure.Mock;

public sealed class MockAlertService : IAlertService
{
    private readonly MockDataStore _store;

    public MockAlertService(MockDataStore store)
    {
        _store = store;
    }

    public Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Alert>>(_store.Alerts.Where(a => a.ResolvedAt is null).OrderByDescending(a => a.CreatedAt).ToList());

    public Task<IEnumerable<Alert>> GetAlertsByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Alert>>(_store.Alerts.Where(a => a.WorkflowId == workflowId).ToList());

    public Task<Alert?> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Alerts.FirstOrDefault(a => a.Id == id));

    public Task<Alert> AcknowledgeAlertAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var alert = Require(id);
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedBy = userId;
        _store.Save();
        return Task.FromResult(alert);
    }

    public Task<Alert> ResolveAlertAsync(Guid id, string resolution, CancellationToken cancellationToken = default)
    {
        var alert = Require(id);
        alert.ResolvedAt = DateTime.UtcNow;
        alert.Context["resolution"] = resolution;
        _store.Save();
        return Task.FromResult(alert);
    }

    public Task<IEnumerable<Alert>> GetAlertHistoryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Alert>>(_store.Alerts.Where(a => a.CreatedAt >= from && a.CreatedAt <= to).ToList());

    private Alert Require(Guid id)
        => _store.Alerts.FirstOrDefault(a => a.Id == id)
           ?? throw new InvalidOperationException($"Alert {id} not found");
}

public sealed class MockAuditService : IAuditService
{
    private readonly MockDataStore _store;

    public MockAuditService(MockDataStore store)
    {
        _store = store;
    }

    public Task<IEnumerable<AuditLogEntry>> GetAuditLogsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<AuditLogEntry>>(
            _store.AuditLogs.Where(e => e.Timestamp >= from && e.Timestamp <= to).OrderByDescending(e => e.Timestamp).ToList());

    public Task<IEnumerable<AuditLogEntry>> GetAuditLogsByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<AuditLogEntry>>(
            _store.AuditLogs.Where(e => e.EntityType == entityType && e.EntityId == entityId).ToList());

    public Task<IEnumerable<AuditLogEntry>> GetAuditLogsByUserAsync(string userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<AuditLogEntry>>(
            _store.AuditLogs.Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp <= to).ToList());

    public Task<Stream> ExportAuditLogsAsync(DateTime from, DateTime to, ExportFormat format, CancellationToken cancellationToken = default)
    {
        var logs = _store.AuditLogs.Where(e => e.Timestamp >= from && e.Timestamp <= to).ToList();
        var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, leaveOpen: true);
        if (format == ExportFormat.Csv)
        {
            writer.WriteLine("timestamp,action,entityType,userName,newValue");
            foreach (var log in logs)
                writer.WriteLine($"{log.Timestamp:O},{log.Action},{log.EntityType},{log.UserName},{log.NewValue}");
        }
        else
        {
            foreach (var log in logs)
                writer.WriteLine($"{{\"timestamp\":\"{log.Timestamp:O}\",\"action\":\"{log.Action}\",\"entity\":\"{log.EntityType}\",\"user\":\"{log.UserName}\"}}");
        }
        writer.Flush();
        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }
}
