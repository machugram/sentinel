using Refit;
using Sentinel.Core.Models;

namespace Sentinel.Infrastructure.Api;

/// <summary>
/// Refit API client for Sentinel Orchestrator backend.
/// </summary>
public interface ISentinelApiClient
{
    #region Workflows
    
    [Get("/api/v1/workflows")]
    Task<IEnumerable<Workflow>> GetWorkflowsAsync(CancellationToken cancellationToken = default);
    
    [Get("/api/v1/workflows/{id}")]
    Task<Workflow> GetWorkflowAsync(Guid id, CancellationToken cancellationToken = default);
    
    [Post("/api/v1/workflows")]
    Task<Workflow> CreateWorkflowAsync([Body] Workflow workflow, CancellationToken cancellationToken = default);
    
    [Put("/api/v1/workflows/{id}")]
    Task<Workflow> UpdateWorkflowAsync(Guid id, [Body] Workflow workflow, CancellationToken cancellationToken = default);
    
    [Delete("/api/v1/workflows/{id}")]
    Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default);
    
    [Post("/api/v1/workflows/{id}/trigger")]
    Task<WorkflowRun> TriggerWorkflowAsync(Guid id, [Body] Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default);
    
    [Get("/api/v1/workflows/search")]
    Task<IEnumerable<Workflow>> SearchWorkflowsAsync([Query] string query, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Workflow Runs
    
    [Get("/api/v1/runs")]
    Task<IEnumerable<WorkflowRun>> GetRecentRunsAsync([Query] int count = 50, CancellationToken cancellationToken = default);
    
    [Get("/api/v1/runs/{id}")]
    Task<WorkflowRun> GetRunAsync(Guid id, CancellationToken cancellationToken = default);
    
    [Get("/api/v1/workflows/{workflowId}/runs")]
    Task<IEnumerable<WorkflowRun>> GetWorkflowRunsAsync(Guid workflowId, CancellationToken cancellationToken = default);
    
    [Post("/api/v1/runs/{id}/cancel")]
    Task<WorkflowRun> CancelRunAsync(Guid id, CancellationToken cancellationToken = default);
    
    [Post("/api/v1/runs/{id}/retry")]
    Task<WorkflowRun> RetryRunAsync(Guid id, CancellationToken cancellationToken = default);
    
    [Get("/api/v1/runs/active")]
    Task<IEnumerable<WorkflowRun>> GetActiveRunsAsync(CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Calendars
    
    [Get("/api/v1/calendars")]
    Task<IEnumerable<TradingCalendar>> GetCalendarsAsync(CancellationToken cancellationToken = default);
    
    [Get("/api/v1/calendars/{id}")]
    Task<TradingCalendar> GetCalendarAsync(Guid id, CancellationToken cancellationToken = default);
    
    [Post("/api/v1/calendars")]
    Task<TradingCalendar> CreateCalendarAsync([Body] TradingCalendar calendar, CancellationToken cancellationToken = default);
    
    [Put("/api/v1/calendars/{id}")]
    Task<TradingCalendar> UpdateCalendarAsync(Guid id, [Body] TradingCalendar calendar, CancellationToken cancellationToken = default);
    
    [Delete("/api/v1/calendars/{id}")]
    Task DeleteCalendarAsync(Guid id, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Alerts
    
    [Get("/api/v1/alerts")]
    Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    
    [Get("/api/v1/alerts/{id}")]
    Task<Alert> GetAlertAsync(Guid id, CancellationToken cancellationToken = default);
    
    [Post("/api/v1/alerts/{id}/acknowledge")]
    Task<Alert> AcknowledgeAlertAsync(Guid id, CancellationToken cancellationToken = default);
    
    [Post("/api/v1/alerts/{id}/resolve")]
    Task<Alert> ResolveAlertAsync(Guid id, [Body] string resolution, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Audit
    
    [Get("/api/v1/audit")]
    Task<IEnumerable<AuditLogEntry>> GetAuditLogsAsync(
        [Query] DateTime from, 
        [Query] DateTime to, 
        CancellationToken cancellationToken = default);
    
    #endregion
}
