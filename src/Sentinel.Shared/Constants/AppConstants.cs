namespace Sentinel.Shared.Constants;

/// <summary>
/// Application-wide constants for the Sentinel Orchestrator platform.
/// </summary>
public static class AppConstants
{
    public const string AppName = "Sentinel Orchestrator";
    public const string AppVersion = "0.1.0";

    public static class Api
    {
        public const string Version = "v1";
        public const string BasePath = $"/api/{Version}";
        public const string MonitoringHubPath = "/hubs/monitoring";
        public const string MigrationHubPath = "/hubs/migration";
    }

    public static class SignalR
    {
        public const string WorkflowRunStatusChanged = "WorkflowRunStatusChanged";
        public const string AlertCreated = "AlertCreated";
        public const string MigrationProgress = "MigrationProgress";
        public const string AgentHeartbeat = "AgentHeartbeat";
    }

    public static class Defaults
    {
        public const int MaxRetries = 3;
        public const int RetryDelaySeconds = 60;
        public const int DashboardRefreshSeconds = 30;
        public const int RecentRunsLimit = 50;
        public const int MaxConcurrentJobs = 100;
        public const int SchedulerPollIntervalSeconds = 10;
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Operator = "Operator";
        public const string Developer = "Developer";
        public const string Viewer = "Viewer";
    }

    public static class Permissions
    {
        public const string WorkflowsRead = "workflows:read";
        public const string WorkflowsWrite = "workflows:write";
        public const string RunsRead = "runs:read";
        public const string RunsWrite = "runs:write";
        public const string AlertsRead = "alerts:read";
        public const string AlertsWrite = "alerts:write";
        public const string CalendarsRead = "calendars:read";
        public const string CalendarsWrite = "calendars:write";
        public const string AuditRead = "audit:read";
        public const string MigrationExecute = "migration:execute";
        public const string AdminAccess = "admin:access";
    }
}
