---
name: observability-diagnostics
description: Comprehensive system visibility and debugging with structured logging, metrics, distributed tracing, and alerting strategies for production job orchestration systems.
---

# Observability & Diagnostics

You are an expert in implementing comprehensive observability for distributed job orchestration systems.

## Structured Logging

### Correlation IDs Across Distributed Traces

```csharp
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
            ?? Guid.NewGuid().ToString();
        
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add("X-Correlation-ID", correlationId);
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path
        }))
        {
            await _next(context);
        }
    }
}

// Usage in services
public class WorkflowService
{
    public async Task<WorkflowRun> ExecuteWorkflowAsync(Guid workflowId)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["WorkflowId"] = workflowId,
            ["CorrelationId"] = correlationId,
            ["Operation"] = "ExecuteWorkflow"
        }))
        {
            _logger.LogInformation("Starting workflow execution");
            // ... implementation
        }
    }
}
```

### Serilog Configuration with Enrichers

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "Sentinel")
    .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File(
        new JsonFormatter(),
        "logs/sentinel-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elasticsearch:9200"))
    {
        AutoRegisterTemplate = true,
        IndexFormat = "sentinel-logs-{0:yyyy.MM.dd}",
        NumberOfShards = 2,
        NumberOfReplicas = 1
    })
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .CreateLogger();
```

### Contextual Logging with Workflow Metadata

```csharp
public class WorkflowExecutionLogger
{
    public async Task LogWorkflowEventAsync(WorkflowRun run, string message, LogLevel level = LogLevel.Information)
    {
        var enrichedProperties = new Dictionary<string, object>
        {
            ["WorkflowId"] = run.WorkflowId,
            ["WorkflowName"] = run.WorkflowName,
            ["RunId"] = run.Id,
            ["RunStatus"] = run.Status,
            ["Attempt"] = run.Attempt,
            ["AgentId"] = run.AgentId,
            ["Duration"] = run.Duration?.TotalSeconds
        };
        
        using (_logger.BeginScope(enrichedProperties))
        {
            switch (level)
            {
                case LogLevel.Information:
                    _logger.LogInformation(message);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(message);
                    break;
                case LogLevel.Error:
                    _logger.LogError(message);
                    break;
            }
        }
        
        // Also store in database for querying
        await _context.WorkflowLogs.AddAsync(new WorkflowLog
        {
            RunId = run.Id,
            Timestamp = DateTime.UtcNow,
            Level = level.ToString(),
            Message = message,
            Properties = JsonSerializer.Serialize(enrichedProperties)
        });
        
        await _context.SaveChangesAsync();
    }
}
```

## Metrics & Monitoring

### Prometheus Metrics

```csharp
public class WorkflowMetricsService
{
    private readonly Counter _workflowExecutions;
    private readonly Histogram _workflowDuration;
    private readonly Gauge _activeWorkflows;
    private readonly Counter _workflowFailures;
    
    public WorkflowMetricsService()
    {
        _workflowExecutions = Metrics.CreateCounter(
            "sentinel_workflow_executions_total",
            "Total number of workflow executions",
            new CounterConfiguration
            {
                LabelNames = new[] { "workflow_name", "status", "agent_id" }
            });
        
        _workflowDuration = Metrics.CreateHistogram(
            "sentinel_workflow_duration_seconds",
            "Workflow execution duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "workflow_name" },
                Buckets = new[] { 1, 5, 10, 30, 60, 300, 600, 1800, 3600 }
            });
        
        _activeWorkflows = Metrics.CreateGauge(
            "sentinel_active_workflows",
            "Number of currently running workflows",
            new GaugeConfiguration
            {
                LabelNames = new[] { "agent_id" }
            });
        
        _workflowFailures = Metrics.CreateCounter(
            "sentinel_workflow_failures_total",
            "Total number of workflow failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "workflow_name", "failure_reason" }
            });
    }
    
    public void RecordWorkflowStarted(WorkflowRun run)
    {
        _activeWorkflows.WithLabels(run.AgentId ?? "unassigned").Inc();
    }
    
    public void RecordWorkflowCompleted(WorkflowRun run)
    {
        _activeWorkflows.WithLabels(run.AgentId ?? "unassigned").Dec();
        
        _workflowExecutions
            .WithLabels(run.WorkflowName, run.Status.ToString(), run.AgentId ?? "unknown")
            .Inc();
        
        if (run.Duration.HasValue)
        {
            _workflowDuration
                .WithLabels(run.WorkflowName)
                .Observe(run.Duration.Value.TotalSeconds);
        }
        
        if (run.Status == RunStatus.Failed)
        {
            _workflowFailures
                .WithLabels(run.WorkflowName, run.ErrorMessage ?? "unknown")
                .Inc();
        }
    }
}

// Startup.cs
app.UseMetricServer();  // Expose /metrics endpoint
app.UseHttpMetrics();   // Collect HTTP metrics
```

### Custom Dashboard Metrics

```csharp
public class DashboardMetricsService
{
    public async Task<DashboardMetrics> GetMetricsAsync(TimeSpan timeWindow)
    {
        var since = DateTime.UtcNow - timeWindow;
        
        var runs = await _context.WorkflowRuns
            .Where(r => r.StartTime >= since)
            .ToListAsync();
        
        return new DashboardMetrics
        {
            TotalRuns = runs.Count,
            SuccessCount = runs.Count(r => r.Status == RunStatus.Success),
            FailureCount = runs.Count(r => r.Status == RunStatus.Failed),
            SuccessRate = runs.Any() 
                ? (double)runs.Count(r => r.Status == RunStatus.Success) / runs.Count * 100 
                : 0,
            AverageDuration = runs
                .Where(r => r.Duration.HasValue)
                .Select(r => r.Duration!.Value.TotalSeconds)
                .DefaultIfEmpty(0)
                .Average(),
            P50Duration = Percentile(runs.Where(r => r.Duration.HasValue)
                .Select(r => r.Duration!.Value.TotalSeconds), 0.50),
            P95Duration = Percentile(runs.Where(r => r.Duration.HasValue)
                .Select(r => r.Duration!.Value.TotalSeconds), 0.95),
            P99Duration = Percentile(runs.Where(r => r.Duration.HasValue)
                .Select(r => r.Duration!.Value.TotalSeconds), 0.99),
            ActiveRuns = await _context.WorkflowRuns
                .CountAsync(r => r.Status == RunStatus.Running)
        };
    }
    
    private double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (!sorted.Any()) return 0;
        
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}
```

## Distributed Tracing

### OpenTelemetry Integration

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("Sentinel.*")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("Sentinel.Scheduler", serviceVersion: "1.0.0"))
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
            })
            .AddJaegerExporter(options =>
            {
                options.AgentHost = "jaeger";
                options.AgentPort = 6831;
            });
    });

// Custom tracing in services
public class WorkflowService
{
    private readonly ActivitySource _activitySource = new("Sentinel.WorkflowService");
    
    public async Task<WorkflowRun> ExecuteWorkflowAsync(Guid workflowId)
    {
        using var activity = _activitySource.StartActivity("ExecuteWorkflow");
        activity?.SetTag("workflow.id", workflowId);
        
        try
        {
            var workflow = await GetWorkflowAsync(workflowId);
            activity?.SetTag("workflow.name", workflow.Name);
            
            // Nested span for dependency resolution
            using var depActivity = _activitySource.StartActivity("ResolveDependencies", ActivityKind.Internal);
            var dependencies = await ResolveDependenciesAsync(workflowId);
            depActivity?.SetTag("dependency.count", dependencies.Count);
            
            // Execute workflow
            var run = await DispatchWorkflowAsync(workflow);
            activity?.SetTag("run.id", run.Id);
            
            return run;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

## Alerting Strategies

### SLA Breach Detection

```csharp
public class SlaMonitoringService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckSlaBreachesAsync();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
    
    private async Task CheckSlaBreachesAsync()
    {
        var workflowsWithSla = await _context.Workflows
            .Where(w => w.MaxDuration.HasValue)
            .ToListAsync();
        
        foreach (var workflow in workflowsWithSla)
        {
            var longRunningRuns = await _context.WorkflowRuns
                .Where(r => r.WorkflowId == workflow.Id)
                .Where(r => r.Status == RunStatus.Running)
                .Where(r => r.StartTime.HasValue)
                .Where(r => DateTime.UtcNow - r.StartTime.Value > workflow.MaxDuration)
                .ToListAsync();
            
            foreach (var run in longRunningRuns)
            {
                await _alertService.SendAlertAsync(new Alert
                {
                    Severity = AlertSeverity.High,
                    Title = $"SLA Breach: {workflow.Name}",
                    Description = $"Run {run.Id} has exceeded SLA of {workflow.MaxDuration}",
                    WorkflowId = workflow.Id,
                    RunId = run.Id,
                    Tags = new[] { "sla", "performance" }
                });
            }
        }
    }
}
```

### Anomaly Detection

```csharp
public class AnomalyDetectionService
{
    public async Task<bool> IsAnomalousAsync(WorkflowRun run)
    {
        if (!run.Duration.HasValue)
            return false;
        
        // Get historical data for this workflow
        var historicalRuns = await _context.WorkflowRuns
            .Where(r => r.WorkflowId == run.WorkflowId)
            .Where(r => r.Status == RunStatus.Success)
            .Where(r => r.Duration.HasValue)
            .Where(r => r.EndTime >= DateTime.UtcNow.AddDays(-30))
            .Select(r => r.Duration!.Value.TotalSeconds)
            .ToListAsync();
        
        if (historicalRuns.Count < 10)
            return false;  // Not enough data
        
        var mean = historicalRuns.Average();
        var variance = historicalRuns.Select(d => Math.Pow(d - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        
        // Alert if duration is > 3 standard deviations from mean
        var zScore = Math.Abs((run.Duration.Value.TotalSeconds - mean) / stdDev);
        
        if (zScore > 3)
        {
            await _alertService.SendAlertAsync(new Alert
            {
                Severity = AlertSeverity.Medium,
                Title = $"Anomalous Duration: {run.WorkflowName}",
                Description = $"Duration {run.Duration.Value.TotalSeconds:F1}s is {zScore:F1}σ from mean ({mean:F1}s)",
                WorkflowId = run.WorkflowId,
                RunId = run.Id,
                Tags = new[] { "anomaly", "performance" }
            });
            
            return true;
        }
        
        return false;
    }
}
```

### Multi-Channel Alerting

```csharp
public interface IAlertChannel
{
    Task SendAsync(Alert alert);
}

public class EmailAlertChannel : IAlertChannel
{
    public async Task SendAsync(Alert alert)
    {
        await _emailService.SendAsync(new Email
        {
            To = alert.Recipients,
            Subject = $"[{alert.Severity}] {alert.Title}",
            Body = alert.Description
        });
    }
}

public class SlackAlertChannel : IAlertChannel
{
    public async Task SendAsync(Alert alert)
    {
        var color = alert.Severity switch
        {
            AlertSeverity.Critical => "danger",
            AlertSeverity.High => "warning",
            _ => "good"
        };
        
        await _slackClient.PostMessageAsync(new SlackMessage
        {
            Channel = "#sentinel-alerts",
            Attachments = new[]
            {
                new SlackAttachment
                {
                    Color = color,
                    Title = alert.Title,
                    Text = alert.Description,
                    Fields = new[]
                    {
                        new SlackField { Title = "Workflow", Value = alert.WorkflowName },
                        new SlackField { Title = "Run ID", Value = alert.RunId?.ToString() }
                    }
                }
            }
        });
    }
}

public class AlertService
{
    private readonly List<IAlertChannel> _channels;
    
    public async Task SendAlertAsync(Alert alert)
    {
        // Log alert
        _logger.LogWarning("Alert triggered: {Title} - {Description}", alert.Title, alert.Description);
        
        // Record in database
        await _context.Alerts.AddAsync(alert);
        await _context.SaveChangesAsync();
        
        // Send through all channels
        var tasks = _channels.Select(channel => channel.SendAsync(alert));
        await Task.WhenAll(tasks);
    }
}
```

## When to Apply This Skill

Use this skill when:
- Implementing logging infrastructure
- Adding metrics and monitoring dashboards
- Integrating distributed tracing
- Setting up alerting for SLA breaches or failures
- Troubleshooting production issues
- Building observability dashboards
- Implementing anomaly detection
