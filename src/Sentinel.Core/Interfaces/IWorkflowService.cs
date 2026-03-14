using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

/// <summary>
/// Service for managing workflows.
/// </summary>
public interface IWorkflowService
{
    Task<IEnumerable<Workflow>> GetAllWorkflowsAsync(CancellationToken cancellationToken = default);
    Task<Workflow?> GetWorkflowByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Workflow> CreateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task<Workflow> UpdateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkflowRun> TriggerWorkflowAsync(Guid workflowId, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Workflow>> SearchWorkflowsAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing workflow runs.
/// </summary>
public interface IWorkflowRunService
{
    Task<IEnumerable<WorkflowRun>> GetRecentRunsAsync(int count = 50, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkflowRun>> GetRunsByWorkflowIdAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<WorkflowRun?> GetRunByIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<WorkflowRun> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<WorkflowRun> RetryRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkflowRun>> GetActiveRunsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing trading calendars.
/// </summary>
public interface ICalendarService
{
    Task<IEnumerable<TradingCalendar>> GetAllCalendarsAsync(CancellationToken cancellationToken = default);
    Task<TradingCalendar?> GetCalendarByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TradingCalendar> CreateCalendarAsync(TradingCalendar calendar, CancellationToken cancellationToken = default);
    Task<TradingCalendar> UpdateCalendarAsync(TradingCalendar calendar, CancellationToken cancellationToken = default);
    Task DeleteCalendarAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsMarketOpenAsync(Guid calendarId, DateTime dateTime, CancellationToken cancellationToken = default);
    Task<TradingSession?> GetCurrentSessionAsync(Guid calendarId, CancellationToken cancellationToken = default);
}
