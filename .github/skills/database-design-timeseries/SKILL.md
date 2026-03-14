---
name: database-design-timeseries
description: Specialized database schema design and optimization for time-series workloads in job orchestration systems. Expertise in partitioning, indexing, query optimization, and event sourcing patterns.
---

# Database Design for Time-Series Workloads

You are an expert in designing and optimizing database schemas for job execution data with millions of workflow runs, focusing on time-series patterns and high-throughput workloads.

## Schema Optimization for Job Orchestration

### Core Entities Design

**Workflows Table:**
```sql
CREATE TABLE Workflows (
    Id UUID PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT,
    Schedule VARCHAR(100),  -- Cron expression
    Command TEXT NOT NULL,
    Status VARCHAR(50) NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    UpdatedAt TIMESTAMPTZ NOT NULL,
    CreatedBy VARCHAR(100),
    
    INDEX idx_workflows_status (Status),
    INDEX idx_workflows_name (Name)
);
```

**WorkflowRuns Table (Time-Series):**
```sql
CREATE TABLE WorkflowRuns (
    Id UUID PRIMARY KEY,
    WorkflowId UUID NOT NULL,
    Status VARCHAR(50) NOT NULL,
    ScheduledTime TIMESTAMPTZ,
    StartTime TIMESTAMPTZ,
    EndTime TIMESTAMPTZ,
    ExitCode INTEGER,
    AgentId VARCHAR(100),
    Attempt INTEGER DEFAULT 1,
    
    INDEX idx_runs_workflow_time (WorkflowId, ScheduledTime DESC),
    INDEX idx_runs_status_time (Status, ScheduledTime DESC),
    INDEX idx_runs_scheduled_time (ScheduledTime DESC)
) PARTITION BY RANGE (ScheduledTime);

-- Monthly partitions
CREATE TABLE WorkflowRuns_2026_03 PARTITION OF WorkflowRuns
    FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
```

### Denormalization Strategies

- Store frequently accessed workflow metadata in WorkflowRuns for read performance
- Materialized views for dashboard queries
- Pre-compute aggregations for analytics

```sql
CREATE MATERIALIZED VIEW WorkflowRunSummary AS
SELECT 
    WorkflowId,
    DATE_TRUNC('day', ScheduledTime) AS run_date,
    COUNT(*) as total_runs,
    COUNT(*) FILTER (WHERE Status = 'SUCCESS') as success_count,
    COUNT(*) FILTER (WHERE Status = 'FAILED') as failure_count,
    AVG(EXTRACT(EPOCH FROM (EndTime - StartTime))) as avg_duration_seconds,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (EndTime - StartTime))) as p95_duration
FROM WorkflowRuns
WHERE EndTime IS NOT NULL
GROUP BY WorkflowId, DATE_TRUNC('day', ScheduledTime);

CREATE UNIQUE INDEX ON WorkflowRunSummary (WorkflowId, run_date);
```

## Time-Series Data Management

### Partitioning Strategies

**Range Partitioning by Date:**
```csharp
// Entity Framework configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<WorkflowRun>()
        .ToTable("WorkflowRuns", b => b.IsPartitioned());
}
```

**Automatic Partition Management:**
```sql
-- Create next month's partition
CREATE OR REPLACE FUNCTION create_next_partition()
RETURNS void AS $$
DECLARE
    next_month DATE := DATE_TRUNC('month', NOW() + INTERVAL '2 months');
    partition_name TEXT := 'WorkflowRuns_' || TO_CHAR(next_month, 'YYYY_MM');
BEGIN
    EXECUTE format('CREATE TABLE IF NOT EXISTS %I PARTITION OF WorkflowRuns 
                   FOR VALUES FROM (%L) TO (%L)',
                   partition_name, next_month, next_month + INTERVAL '1 month');
END;
$$ LANGUAGE plpgsql;
```

### Retention Policies and Archival

```sql
-- Archive old partitions to cold storage
CREATE PROCEDURE archive_old_runs(months_to_keep INTEGER)
LANGUAGE plpgsql AS $$
DECLARE
    cutoff_date DATE := DATE_TRUNC('month', NOW() - (months_to_keep || ' months')::INTERVAL);
    partition_name TEXT;
BEGIN
    FOR partition_name IN 
        SELECT tablename FROM pg_tables 
        WHERE tablename LIKE 'WorkflowRuns_%' 
        AND tablename < 'WorkflowRuns_' || TO_CHAR(cutoff_date, 'YYYY_MM')
    LOOP
        -- Export to archive storage
        EXECUTE format('COPY %I TO ''/archive/%I.csv'' CSV HEADER', partition_name, partition_name);
        -- Drop old partition
        EXECUTE format('DROP TABLE %I', partition_name);
    END LOOP;
END;
$$;
```

## Query Optimization

### Composite Indexes for Common Patterns

```sql
-- For dashboard queries: recent runs by workflow
CREATE INDEX idx_runs_workflow_recent 
ON WorkflowRuns (WorkflowId, ScheduledTime DESC) 
WHERE ScheduledTime > NOW() - INTERVAL '7 days';

-- For monitoring: active runs
CREATE INDEX idx_runs_active 
ON WorkflowRuns (Status, StartTime) 
WHERE Status IN ('RUNNING', 'DISPATCHED');

-- For dependency checking
CREATE INDEX idx_runs_workflow_status_time 
ON WorkflowRuns (WorkflowId, Status, EndTime DESC);
```

### Window Functions for Analytics

```sql
-- Get last 5 runs per workflow with trend
SELECT 
    WorkflowId,
    ScheduledTime,
    Status,
    EXTRACT(EPOCH FROM (EndTime - StartTime)) as duration,
    AVG(EXTRACT(EPOCH FROM (EndTime - StartTime))) 
        OVER (PARTITION BY WorkflowId ORDER BY ScheduledTime 
              ROWS BETWEEN 4 PRECEDING AND CURRENT ROW) as moving_avg_duration,
    ROW_NUMBER() OVER (PARTITION BY WorkflowId ORDER BY ScheduledTime DESC) as run_rank
FROM WorkflowRuns
WHERE EndTime IS NOT NULL
QUALIFY run_rank <= 5;
```

### Connection Pooling Configuration

```csharp
// Startup.cs
services.AddDbContext<SentinelDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30);
        npgsqlOptions.EnableRetryOnFailure(3);
        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
    });
}, ServiceLifetime.Scoped, ServiceLifetime.Singleton);

// Connection pool settings in connection string
// "Minimum Pool Size=10;Maximum Pool Size=50;Pooling=true;"
```

## Transaction Management

### Isolation Levels for Different Operations

```csharp
// Read operations: Read Committed (default)
public async Task<WorkflowRun?> GetRunAsync(Guid id)
{
    return await _context.WorkflowRuns.FindAsync(id);
}

// State transitions: Serializable to prevent conflicts
public async Task<bool> UpdateRunStatusAsync(Guid id, RunStatus newStatus)
{
    using var transaction = await _context.Database.BeginTransactionAsync(
        IsolationLevel.Serializable);
    
    try
    {
        var run = await _context.WorkflowRuns.FindAsync(id);
        if (run == null) return false;
        
        // Validate state transition
        if (!IsValidTransition(run.Status, newStatus))
            throw new InvalidOperationException($"Invalid transition: {run.Status} -> {newStatus}");
        
        run.Status = newStatus;
        run.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### Optimistic Locking with Row Versioning

```csharp
public class Workflow
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    
    [Timestamp]
    public byte[] RowVersion { get; set; }
}

// Update with concurrency check
try
{
    workflow.Name = "Updated Name";
    await _context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    // Handle concurrent modification
    var entry = ex.Entries.Single();
    var databaseValues = await entry.GetDatabaseValuesAsync();
    // Resolve conflict or retry
}
```

## Event Sourcing Patterns

### Append-Only Event Log

```csharp
public class WorkflowEvent
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string EventType { get; set; }  // Created, Updated, Deleted, Executed
    public DateTime OccurredAt { get; set; }
    public string UserId { get; set; }
    public JsonDocument Payload { get; set; }
    public JsonDocument? Metadata { get; set; }
}

// Never update or delete events, only insert
public async Task LogEventAsync(WorkflowEvent evt)
{
    evt.OccurredAt = DateTime.UtcNow;
    _context.WorkflowEvents.Add(evt);
    await _context.SaveChangesAsync();
}
```

### Event Replay for State Reconstruction

```csharp
public async Task<Workflow> ReconstructWorkflowStateAsync(Guid workflowId, DateTime? asOf = null)
{
    var events = await _context.WorkflowEvents
        .Where(e => e.WorkflowId == workflowId)
        .Where(e => asOf == null || e.OccurredAt <= asOf)
        .OrderBy(e => e.OccurredAt)
        .ToListAsync();
    
    var workflow = new Workflow { Id = workflowId };
    
    foreach (var evt in events)
    {
        workflow = ApplyEvent(workflow, evt);
    }
    
    return workflow;
}
```

### CQRS Pattern

```csharp
// Write model: Commands modify state and emit events
public class CreateWorkflowCommand : IRequest<Guid>
{
    public string Name { get; set; }
    public string Schedule { get; set; }
}

// Read model: Optimized projections for queries
public class WorkflowListProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }
    public DateTime LastRunTime { get; set; }
    public int TotalRuns { get; set; }
    public double SuccessRate { get; set; }
}
```

## When to Apply This Skill

Use this skill when:
- Designing or optimizing database schemas for Sentinel
- Implementing Entity Framework migrations
- Troubleshooting slow queries or database performance issues
- Planning data retention and archival strategies
- Implementing audit trails and event sourcing
- Scaling the database for high-volume workloads
- Setting up partitioning or sharding strategies
