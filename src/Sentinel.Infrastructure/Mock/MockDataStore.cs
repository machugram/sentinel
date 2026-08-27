using System.Text.Json;
using System.Text.Json.Serialization;
using Sentinel.Core.Models;

namespace Sentinel.Infrastructure.Mock;

/// <summary>
/// Shared catalog used by all mock services so Dashboard, Runs, and Alerts stay consistent.
/// Persists to %LocalAppData%/Sentinel/mock-store.json so desktop edits survive restart.
/// </summary>
public sealed class MockDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public List<Workflow> Workflows { get; set; } = new();
    public List<WorkflowRun> Runs { get; set; } = new();
    public List<Alert> Alerts { get; set; } = new();
    public List<TradingCalendar> Calendars { get; set; } = new();
    public List<AuditLogEntry> AuditLogs { get; set; } = new();

    public MockDataStore() : this(DefaultPath())
    {
    }

    public MockDataStore(string path)
    {
        _path = path;
        if (!TryLoad())
        {
            Seed();
            Save();
        }
    }

    public static string DefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel", "mock-store.json");

    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var snapshot = new MockStoreSnapshot
        {
            Workflows = Workflows,
            Runs = Runs,
            Alerts = Alerts,
            Calendars = Calendars,
            AuditLogs = AuditLogs
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private bool TryLoad()
    {
        try
        {
            if (!File.Exists(_path))
                return false;

            var snapshot = JsonSerializer.Deserialize<MockStoreSnapshot>(File.ReadAllText(_path), JsonOptions);
            if (snapshot?.Workflows is not { Count: > 0 })
                return false;

            Workflows = snapshot.Workflows;
            Runs = snapshot.Runs;
            Alerts = snapshot.Alerts;
            Calendars = snapshot.Calendars;
            AuditLogs = snapshot.AuditLogs;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class MockStoreSnapshot
    {
        public List<Workflow> Workflows { get; set; } = new();
        public List<WorkflowRun> Runs { get; set; } = new();
        public List<Alert> Alerts { get; set; } = new();
        public List<TradingCalendar> Calendars { get; set; } = new();
        public List<AuditLogEntry> AuditLogs { get; set; } = new();
    }

    public static Guid TradeCaptureId { get; } = Id(1);
    public static Guid EodRiskId { get; } = Id(2);
    public static Guid DtccReportId { get; } = Id(3);
    public static Guid MarketDataId { get; } = Id(4);
    public static Guid NavCalcId { get; } = Id(5);
    public static Guid SurveillanceId { get; } = Id(6);
    public static Guid ClientReportingId { get; } = Id(7);
    public static Guid MarginCalcId { get; } = Id(8);

    public static Guid GlobalCalendarId { get; } = Id(101);
    public static Guid UsCalendarId { get; } = Id(102);

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private void Seed()
    {
        var now = DateTime.UtcNow;

        Workflows.Add(CreateWorkflow(TradeCaptureId, "Trade Capture Pipeline", "Ingests exchange fills and books trades.", WorkflowStatus.Active, "Americas", "*/15 * * * *", now.AddMinutes(-5), now.AddMinutes(10), new[] { "trading", "core" }));
        Workflows.Add(CreateWorkflow(EodRiskId, "EOD Risk Calculation", "End-of-day VaR and Greeks.", WorkflowStatus.Active, "Global", "0 17 * * 1-5", now.AddMinutes(-15), now.Date.AddHours(17), new[] { "risk", "eod" }));
        Workflows.Add(CreateWorkflow(DtccReportId, "DTCC Regulatory Report", "Daily DTCC submission pack.", WorkflowStatus.Active, "Americas", "0 6 * * 1-5", now.AddMinutes(-30), now.Date.AddDays(1).AddHours(6), new[] { "regulatory" }));
        Workflows.Add(CreateWorkflow(MarketDataId, "Market Data Reconciliation", "Vendor vs internal price check.", WorkflowStatus.Active, "APAC", "0 8 * * *", now.AddHours(-1), now.Date.AddDays(1).AddHours(8), new[] { "marketdata" }));
        Workflows.Add(CreateWorkflow(NavCalcId, "NAV Calculation", "Fund NAV and publication.", WorkflowStatus.Active, "EMEA", "0 18 * * 1-5", now.AddHours(-2), now.Date.AddHours(18), new[] { "funds" }));
        Workflows.Add(CreateWorkflow(SurveillanceId, "Surveillance Daily", "Alert rollup for compliance.", WorkflowStatus.Active, "Global", "0 0 * * *", now.AddHours(-6), now.Date.AddDays(1), new[] { "compliance" }));
        Workflows.Add(CreateWorkflow(ClientReportingId, "Client Reporting", "Monthly client packs.", WorkflowStatus.Paused, "Americas", "0 9 1 * *", now.AddDays(-12), null, new[] { "client" }));
        Workflows.Add(CreateWorkflow(MarginCalcId, "Margin Calculation", "CCP margin and collateral.", WorkflowStatus.Active, "EMEA", "0 16 * * 1-5", now.AddHours(-3), now.Date.AddHours(16), new[] { "margin" }));

        foreach (var workflow in Workflows)
        {
            workflow.Tasks = CreateDefaultTasks(workflow.Name);
        }

        Runs.Add(CreateRun(Id(201), TradeCaptureId, "Trade Capture Pipeline", RunStatus.Success, now.AddMinutes(-7), now.AddMinutes(-5), TriggerType.Scheduled, "scheduler"));
        Runs.Add(CreateRun(Id(202), EodRiskId, "EOD Risk Calculation", RunStatus.Running, now.AddMinutes(-15), null, TriggerType.Scheduled, "scheduler"));
        Runs.Add(CreateRun(Id(203), DtccReportId, "DTCC Regulatory Report", RunStatus.Success, now.AddMinutes(-38), now.AddMinutes(-30), TriggerType.Scheduled, "scheduler"));
        Runs.Add(CreateRun(Id(204), MarketDataId, "Market Data Reconciliation", RunStatus.Failed, now.AddHours(-1).AddMinutes(-1), now.AddHours(-1), TriggerType.Scheduled, "scheduler", "Price file hash mismatch after 3 retries"));
        Runs.Add(CreateRun(Id(205), NavCalcId, "NAV Calculation", RunStatus.Success, now.AddHours(-2).AddMinutes(-12), now.AddHours(-2), TriggerType.Scheduled, "scheduler"));
        Runs.Add(CreateRun(Id(206), SurveillanceId, "Surveillance Daily", RunStatus.Success, now.AddHours(-6).AddMinutes(-4), now.AddHours(-6), TriggerType.Scheduled, "scheduler"));
        Runs.Add(CreateRun(Id(207), MarginCalcId, "Margin Calculation", RunStatus.Success, now.AddHours(-3).AddMinutes(-9), now.AddHours(-3), TriggerType.Scheduled, "scheduler"));
        Runs.Add(CreateRun(Id(208), TradeCaptureId, "Trade Capture Pipeline", RunStatus.Success, now.AddMinutes(-22), now.AddMinutes(-20), TriggerType.Scheduled, "scheduler"));
        Runs.Add(CreateRun(Id(209), DtccReportId, "DTCC Regulatory Report", RunStatus.Pending, now.AddMinutes(-1), null, TriggerType.Manual, "Alex Chen"));
        Runs.Add(CreateRun(Id(210), ClientReportingId, "Client Reporting", RunStatus.Cancelled, now.AddDays(-12), now.AddDays(-12).AddMinutes(2), TriggerType.Manual, "Alex Chen"));
        Runs.Add(CreateRun(Id(211), EodRiskId, "EOD Risk Calculation", RunStatus.TimedOut, now.AddDays(-1).AddHours(-2), now.AddDays(-1).AddHours(-1), TriggerType.Scheduled, "scheduler", "Exceeded 60m SLA"));
        Runs.Add(CreateRun(Id(212), NavCalcId, "NAV Calculation", RunStatus.Running, now.AddMinutes(-4), null, TriggerType.Manual, "Alex Chen"));

        AttachTaskRuns();

        Alerts.Add(new Alert
        {
            Id = Id(301),
            Type = AlertType.SlaBreach,
            Severity = AlertSeverity.Warning,
            Title = "SLA Breach Warning",
            Message = "Trade Capture Pipeline approaching SLA threshold (12m remaining of 15m).",
            CreatedAt = now.AddMinutes(-10),
            WorkflowId = TradeCaptureId,
            WorkflowRunId = Id(201),
            AiSuggestion = "Check the downstream booking API latency; last 3 runs spent 80% of time on BookTrades."
        });
        Alerts.Add(new Alert
        {
            Id = Id(302),
            Type = AlertType.TaskFailure,
            Severity = AlertSeverity.Critical,
            Title = "Task Failure",
            Message = "Market Data Reconciliation failed after 3 retries.",
            CreatedAt = now.AddHours(-1),
            WorkflowId = MarketDataId,
            WorkflowRunId = Id(204),
            AiSuggestion = "Vendor file MD5 changed. Re-pull the 16:00 snapshot or skip the stale RIC batch."
        });
        Alerts.Add(new Alert
        {
            Id = Id(303),
            Type = AlertType.AnomalyDetected,
            Severity = AlertSeverity.Info,
            Title = "Anomaly Detected",
            Message = "Unusual execution time for EOD Risk Calculation.",
            CreatedAt = now.AddHours(-2),
            WorkflowId = EodRiskId,
            WorkflowRunId = Id(202),
            AiSuggestion = "Universe size is 18% above 20-day average. Consider splitting equity and rates books."
        });
        Alerts.Add(new Alert
        {
            Id = Id(304),
            Type = AlertType.SystemError,
            Severity = AlertSeverity.Warning,
            Title = "Executor Queue Depth",
            Message = "SSH executor queue is at 42 pending jobs (warn at 40).",
            CreatedAt = now.AddMinutes(-25),
            AiSuggestion = "Scale the APAC SSH pool from 8 to 12 workers before Tokyo open."
        });

        Calendars.Add(new TradingCalendar
        {
            Id = GlobalCalendarId,
            Name = "Global Follow-the-Sun",
            Description = "Americas, EMEA, and APAC cash-equity sessions.",
            TimeZone = "UTC",
            Sessions =
            {
                new TradingSession { Id = Id(401), Name = "APAC Cash", Region = TradingRegion.APAC, OpenTime = new TimeOnly(0, 0), CloseTime = new TimeOnly(8, 0), TimeZone = "UTC" },
                new TradingSession { Id = Id(402), Name = "EMEA Cash", Region = TradingRegion.EMEA, OpenTime = new TimeOnly(8, 0), CloseTime = new TimeOnly(16, 30), TimeZone = "UTC" },
                new TradingSession { Id = Id(403), Name = "Americas Cash", Region = TradingRegion.Americas, OpenTime = new TimeOnly(13, 30), CloseTime = new TimeOnly(21, 0), TimeZone = "UTC" }
            },
            Holidays =
            {
                new Holiday { Date = new DateOnly(now.Year, 1, 1), Name = "New Year's Day", Type = HolidayType.Full, AffectedMarkets = new[] { "NYSE", "LSE", "TSE" } },
                new Holiday { Date = new DateOnly(now.Year, 12, 25), Name = "Christmas Day", Type = HolidayType.Full, AffectedMarkets = new[] { "NYSE", "LSE" } },
                new Holiday { Date = new DateOnly(now.Year, 7, 3), Name = "US Early Close", Type = HolidayType.EarlyClose, AffectedMarkets = new[] { "NYSE", "NASDAQ" } }
            },
            MaintenanceWindows =
            {
                new MaintenanceWindow
                {
                    Id = Id(501),
                    StartTime = now.Date.AddDays(3).AddHours(2),
                    EndTime = now.Date.AddDays(3).AddHours(4),
                    Description = "PostgreSQL partition rollover",
                    IsRecurring = true,
                    RecurrenceRule = "FREQ=WEEKLY;BYDAY=SA"
                }
            }
        });

        Calendars.Add(new TradingCalendar
        {
            Id = UsCalendarId,
            Name = "US Equities",
            Description = "NYSE/NASDAQ regular session with US holidays.",
            TimeZone = "America/New_York",
            Sessions =
            {
                new TradingSession { Id = Id(404), Name = "NYSE Regular", Region = TradingRegion.Americas, OpenTime = new TimeOnly(9, 30), CloseTime = new TimeOnly(16, 0), TimeZone = "America/New_York" }
            },
            Holidays =
            {
                new Holiday { Date = new DateOnly(now.Year, 1, 1), Name = "New Year's Day", Type = HolidayType.Full, AffectedMarkets = new[] { "NYSE", "NASDAQ" } },
                new Holiday { Date = new DateOnly(now.Year, 7, 4), Name = "Independence Day", Type = HolidayType.Full, AffectedMarkets = new[] { "NYSE", "NASDAQ" } },
                new Holiday { Date = new DateOnly(now.Year, 11, 26), Name = "Thanksgiving", Type = HolidayType.Full, AffectedMarkets = new[] { "NYSE", "NASDAQ" } }
            }
        });

        AuditLogs.Add(Entry(now.AddMinutes(-8), "workflow.trigger", "Workflow", TradeCaptureId, "Alex Chen", "operator-1", null, "manual trigger"));
        AuditLogs.Add(Entry(now.AddMinutes(-15), "run.start", "WorkflowRun", Id(202), "scheduler", "system", null, "EOD Risk Calculation"));
        AuditLogs.Add(Entry(now.AddMinutes(-25), "alert.create", "Alert", Id(304), "sentinel", "system", null, "Executor Queue Depth"));
        AuditLogs.Add(Entry(now.AddHours(-1), "run.fail", "WorkflowRun", Id(204), "scheduler", "system", "Running", "Failed"));
        AuditLogs.Add(Entry(now.AddHours(-1), "alert.create", "Alert", Id(302), "sentinel", "system", null, "Task Failure"));
        AuditLogs.Add(Entry(now.AddHours(-2), "workflow.pause", "Workflow", ClientReportingId, "Alex Chen", "operator-1", "Active", "Paused"));
        AuditLogs.Add(Entry(now.AddHours(-3), "calendar.update", "TradingCalendar", GlobalCalendarId, "Priya Shah", "admin-2", null, "Added US Early Close"));
        AuditLogs.Add(Entry(now.AddHours(-6), "run.success", "WorkflowRun", Id(206), "scheduler", "system", "Running", "Success"));
        AuditLogs.Add(Entry(now.AddDays(-1), "settings.update", "AppConfiguration", Guid.Empty, "Alex Chen", "operator-1", "Dark", "Dark"));
        AuditLogs.Add(Entry(now.AddDays(-2), "migration.import", "Workflow", MarginCalcId, "Alex Chen", "operator-1", "JIL:MARGIN_CALC", "Imported"));
        AuditLogs.Add(Entry(now.AddDays(-3), "user.login", "User", Guid.Empty, "Alex Chen", "operator-1", null, "mock-auth"));
        AuditLogs.Add(Entry(now.AddDays(-4), "workflow.create", "Workflow", SurveillanceId, "Priya Shah", "admin-2", null, "Surveillance Daily"));
    }

    private static Workflow CreateWorkflow(
        Guid id,
        string name,
        string description,
        WorkflowStatus status,
        string region,
        string cron,
        DateTime? lastRun,
        DateTime? nextRun,
        string[] tags)
    {
        return new Workflow
        {
            Id = id,
            Name = name,
            Description = description,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddMonths(-4),
            LastRunAt = lastRun,
            NextRunAt = nextRun,
            CronExpression = cron,
            Tags = tags,
            Metadata = new Dictionary<string, string> { ["Region"] = region },
            Sla = new WorkflowSla
            {
                ExpectedDuration = TimeSpan.FromMinutes(12),
                WarningThreshold = TimeSpan.FromMinutes(10),
                CriticalThreshold = TimeSpan.FromMinutes(15),
                NotificationChannel = "ops-trading"
            }
        };
    }

    private static List<WorkflowTask> CreateDefaultTasks(string workflowName)
    {
        return new List<WorkflowTask>
        {
            new() { Id = Guid.NewGuid(), Name = "Prepare", Type = TaskType.Shell, Command = "prepare.sh", Status = Core.Models.TaskStatus.Success, XPosition = 40, YPosition = 80 },
            new() { Id = Guid.NewGuid(), Name = workflowName.Split(' ')[0] + " Work", Type = TaskType.Python, Command = "run.py", Status = Core.Models.TaskStatus.Running, XPosition = 240, YPosition = 80 },
            new() { Id = Guid.NewGuid(), Name = "Publish", Type = TaskType.Http, Command = "POST /publish", Status = Core.Models.TaskStatus.Pending, XPosition = 440, YPosition = 80 }
        };
    }

    private static WorkflowRun CreateRun(
        Guid id,
        Guid workflowId,
        string workflowName,
        RunStatus status,
        DateTime started,
        DateTime? completed,
        TriggerType trigger,
        string triggeredBy,
        string? error = null)
    {
        return new WorkflowRun
        {
            Id = id,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            Status = status,
            StartedAt = started,
            CompletedAt = completed,
            TriggerType = trigger,
            TriggeredBy = triggeredBy,
            ErrorMessage = error
        };
    }

    private void AttachTaskRuns()
    {
        foreach (var run in Runs)
        {
            var workflow = Workflows.First(w => w.Id == run.WorkflowId);
            var started = run.StartedAt;
            run.TaskRuns = workflow.Tasks.Select((task, index) =>
            {
                var taskStatus = run.Status switch
                {
                    RunStatus.Success => RunStatus.Success,
                    RunStatus.Failed => index == workflow.Tasks.Count - 1 ? RunStatus.Failed : RunStatus.Success,
                    RunStatus.Running => index == 0 ? RunStatus.Success : index == 1 ? RunStatus.Running : RunStatus.Pending,
                    RunStatus.Pending => RunStatus.Pending,
                    RunStatus.Cancelled => index == 0 ? RunStatus.Success : RunStatus.Cancelled,
                    RunStatus.TimedOut => index < workflow.Tasks.Count - 1 ? RunStatus.Success : RunStatus.TimedOut,
                    _ => RunStatus.Pending
                };

                DateTime? taskStart = taskStatus == RunStatus.Pending ? null : started.AddMinutes(index * 2);
                DateTime? taskEnd = taskStatus is RunStatus.Running or RunStatus.Pending
                    ? null
                    : (taskStart ?? started).AddMinutes(1.5);

                return new TaskRun
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    TaskName = task.Name,
                    Status = taskStatus,
                    StartedAt = taskStart,
                    CompletedAt = taskEnd,
                    AttemptNumber = run.Status == RunStatus.Failed && index == workflow.Tasks.Count - 1 ? 3 : 1,
                    ExitCode = taskStatus == RunStatus.Failed ? 2 : taskStatus == RunStatus.Success ? 0 : null,
                    ErrorMessage = taskStatus == RunStatus.Failed ? run.ErrorMessage : null,
                    Output = taskStatus == RunStatus.Success ? "completed" : null
                };
            }).ToList();
        }
    }

    private static AuditLogEntry Entry(
        DateTime timestamp,
        string action,
        string entityType,
        Guid entityId,
        string userName,
        string userId,
        string? oldValue,
        string? newValue)
    {
        return new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserName = userName,
            UserId = userId,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = "10.20.14.8",
            UserAgent = "Sentinel.Desktop/0.1"
        };
    }
}
