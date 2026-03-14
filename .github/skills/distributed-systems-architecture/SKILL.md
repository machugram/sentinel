---
name: distributed-systems-architecture
description: Building reliable, scalable distributed schedulers with high availability patterns, consensus algorithms, scalability patterns, and fault tolerance for job orchestration systems.
---

# Distributed Systems Architecture

You are an expert in building reliable, scalable distributed job schedulers with proper high availability, consistency guarantees, and fault tolerance.

## High Availability Patterns

### Leader Election

Implement leader election for active-passive scheduler deployments:

```csharp
public interface ILeaderElectionService
{
    Task<bool> TryAcquireLeadershipAsync(CancellationToken ct);
    Task ReleaseLeadershipAsync();
    Task<bool> IsLeaderAsync();
    event EventHandler<LeadershipChangedEventArgs> LeadershipChanged;
}

// Using distributed locks (Redis, PostgreSQL advisory locks)
public class PostgreSqlLeaderElection : ILeaderElectionService
{
    private readonly string _instanceId = Guid.NewGuid().ToString();
    private Timer _heartbeatTimer;
    
    public async Task<bool> TryAcquireLeadershipAsync(CancellationToken ct)
    {
        // PostgreSQL advisory lock
        var acquired = await _connection.ExecuteScalarAsync<bool>(
            "SELECT pg_try_advisory_lock(@lockId)", 
            new { lockId = SCHEDULER_LOCK_ID });
        
        if (acquired)
        {
            _logger.LogInformation("Instance {InstanceId} acquired leadership", _instanceId);
            StartHeartbeat();
        }
        
        return acquired;
    }
    
    private void StartHeartbeat()
    {
        _heartbeatTimer = new Timer(async _ =>
        {
            // Keep lock alive and detect split-brain
            var stillLeader = await _connection.ExecuteScalarAsync<bool>(
                "SELECT pg_try_advisory_lock(@lockId)", 
                new { lockId = SCHEDULER_LOCK_ID });
            
            if (!stillLeader)
            {
                _logger.LogWarning("Lost leadership!");
                LeadershipChanged?.Invoke(this, new LeadershipChangedEventArgs(false));
            }
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }
}
```

### Split-Brain Prevention

```csharp
public class SchedulerCoordinator
{
    private readonly ILeaderElectionService _election;
    private readonly IFencingTokenService _fencing;
    
    public async Task RunSchedulerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (await _election.IsLeaderAsync())
            {
                var token = await _fencing.GetCurrentTokenAsync();
                
                try
                {
                    // All DB writes include fencing token
                    await ScheduleJobsAsync(token, ct);
                }
                catch (FencingTokenExpiredException)
                {
                    _logger.LogWarning("Fencing token expired, stepping down");
                    await _election.ReleaseLeadershipAsync();
                }
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
    }
    
    private async Task ScheduleJobsAsync(FencingToken token, CancellationToken ct)
    {
        var jobs = await GetSchedulableJobsAsync();
        
        foreach (var job in jobs)
        {
            // Include fencing token to prevent split-brain writes
            await DispatchJobWithFencingAsync(job, token);
        }
    }
}
```

### Graceful Shutdown

```csharp
public class SchedulerHostedService : IHostedService
{
    private readonly ISchedulerEngine _scheduler;
    private readonly IHostApplicationLifetime _lifetime;
    
    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Scheduler starting...");
        await _scheduler.StartAsync(ct);
    }
    
    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Scheduler stopping gracefully...");
        
        // Stop accepting new jobs
        await _scheduler.StopAcceptingJobsAsync();
        
        // Wait for in-flight operations to complete
        var timeout = TimeSpan.FromSeconds(30);
        await _scheduler.WaitForInflightOperationsAsync(timeout);
        
        // Release leadership
        await _leaderElection.ReleaseLeadershipAsync();
        
        _logger.LogInformation("Scheduler stopped");
    }
}
```

## Consistency & Consensus

### CAP Theorem Trade-offs

For job orchestration systems:
- **Consistency**: Job state transitions must be strongly consistent
- **Availability**: Prefer availability for read operations (monitoring, logs)
- **Partition Tolerance**: Must handle network partitions gracefully

**Decision Matrix:**
```
Operation              | Consistency Level | Rationale
-----------------------|-------------------|---------------------------
Job state update       | Strong (CP)       | Prevent duplicate execution
Job dispatch           | Strong (CP)       | Exactly-once dispatch
Dashboard queries      | Eventual (AP)     | Stale data acceptable
Log streaming          | Eventual (AP)     | Real-time not critical
Dependency check       | Strong (CP)       | Prevent incorrect triggers
```

### Vector Clocks for Causality

```csharp
public class VectorClock
{
    private readonly ConcurrentDictionary<string, long> _clocks = new();
    
    public void Increment(string nodeId)
    {
        _clocks.AddOrUpdate(nodeId, 1, (_, val) => val + 1);
    }
    
    public bool HappensBefore(VectorClock other)
    {
        bool anyLess = false;
        bool anyGreater = false;
        
        foreach (var node in _clocks.Keys.Union(other._clocks.Keys))
        {
            var thisClock = _clocks.GetValueOrDefault(node, 0);
            var otherClock = other._clocks.GetValueOrDefault(node, 0);
            
            if (thisClock < otherClock) anyLess = true;
            if (thisClock > otherClock) anyGreater = true;
        }
        
        return anyLess && !anyGreater;
    }
    
    public bool IsConcurrent(VectorClock other)
    {
        return !HappensBefore(other) && !other.HappensBefore(this);
    }
}
```

## Scalability Patterns

### Horizontal Scaling with Consistent Hashing

```csharp
public class ConsistentHashSchedulerPartitioner
{
    private readonly SortedDictionary<int, string> _ring = new();
    private readonly int _virtualNodesPerScheduler = 150;
    
    public void AddScheduler(string schedulerId)
    {
        for (int i = 0; i < _virtualNodesPerScheduler; i++)
        {
            var hash = GetHash($"{schedulerId}:{i}");
            _ring[hash] = schedulerId;
        }
    }
    
    public string GetSchedulerForWorkflow(Guid workflowId)
    {
        var hash = GetHash(workflowId.ToString());
        
        // Find first scheduler >= hash
        var scheduler = _ring
            .Where(kvp => kvp.Key >= hash)
            .Select(kvp => kvp.Value)
            .FirstOrDefault() ?? _ring.Values.First();
        
        return scheduler;
    }
    
    private int GetHash(string key)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt32(hashBytes, 0);
    }
}
```

### Work-Stealing for Load Balancing

```csharp
public class WorkStealingScheduler
{
    private readonly ConcurrentQueue<WorkflowRun>[] _perSchedulerQueues;
    private readonly int _schedulerCount;
    
    public async Task<WorkflowRun?> TryStealWorkAsync(int stealerIndex)
    {
        // Try local queue first
        if (_perSchedulerQueues[stealerIndex].TryDequeue(out var work))
            return work;
        
        // Try stealing from other schedulers (round-robin)
        for (int i = 1; i < _schedulerCount; i++)
        {
            int victimIndex = (stealerIndex + i) % _schedulerCount;
            
            if (_perSchedulerQueues[victimIndex].TryDequeue(out work))
            {
                _logger.LogDebug("Scheduler {Stealer} stole work from {Victim}", 
                    stealerIndex, victimIndex);
                return work;
            }
        }
        
        return null;
    }
    
    public void EnqueueWork(WorkflowRun run, int schedulerIndex)
    {
        _perSchedulerQueues[schedulerIndex].Enqueue(run);
    }
}
```

### Backpressure and Flow Control

```csharp
public class BackpressureAwareDispatcher
{
    private readonly SemaphoreSlim _globalConcurrencyLimit;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _perAgentLimits;
    
    public BackpressureAwareDispatcher(int maxConcurrentJobs)
    {
        _globalConcurrencyLimit = new SemaphoreSlim(maxConcurrentJobs);
        _perAgentLimits = new ConcurrentDictionary<string, SemaphoreSlim>();
    }
    
    public async Task<bool> TryDispatchJobAsync(WorkflowRun run, string agentId, CancellationToken ct)
    {
        // Check global capacity
        if (!await _globalConcurrencyLimit.WaitAsync(0, ct))
        {
            _logger.LogWarning("Global capacity limit reached, applying backpressure");
            return false;
        }
        
        try
        {
            // Check per-agent capacity
            var agentLimit = _perAgentLimits.GetOrAdd(agentId, 
                _ => new SemaphoreSlim(MAX_JOBS_PER_AGENT));
            
            if (!await agentLimit.WaitAsync(0, ct))
            {
                _logger.LogWarning("Agent {AgentId} at capacity", agentId);
                return false;
            }
            
            try
            {
                await DispatchToAgentAsync(run, agentId, ct);
                return true;
            }
            finally
            {
                agentLimit.Release();
            }
        }
        finally
        {
            _globalConcurrencyLimit.Release();
        }
    }
}
```

## Fault Tolerance

### Circuit Breaker Pattern

```csharp
public class AgentCircuitBreaker
{
    private readonly ConcurrentDictionary<string, CircuitState> _circuits = new();
    
    public async Task<T> ExecuteAsync<T>(string agentId, Func<Task<T>> action)
    {
        var state = _circuits.GetOrAdd(agentId, _ => new CircuitState());
        
        if (state.IsOpen && DateTime.UtcNow < state.OpenUntil)
        {
            throw new CircuitBreakerOpenException($"Circuit open for agent {agentId}");
        }
        
        try
        {
            var result = await action();
            state.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            state.RecordFailure();
            
            if (state.ShouldOpen())
            {
                state.Open(TimeSpan.FromMinutes(1));
                _logger.LogWarning("Circuit breaker opened for agent {AgentId}", agentId);
            }
            
            throw;
        }
    }
    
    private class CircuitState
    {
        private int _consecutiveFailures;
        private const int FAILURE_THRESHOLD = 5;
        
        public bool IsOpen { get; private set; }
        public DateTime OpenUntil { get; private set; }
        
        public void RecordSuccess()
        {
            _consecutiveFailures = 0;
            IsOpen = false;
        }
        
        public void RecordFailure()
        {
            Interlocked.Increment(ref _consecutiveFailures);
        }
        
        public bool ShouldOpen() => _consecutiveFailures >= FAILURE_THRESHOLD;
        
        public void Open(TimeSpan duration)
        {
            IsOpen = true;
            OpenUntil = DateTime.UtcNow + duration;
        }
    }
}
```

### Retry with Exponential Backoff

```csharp
public class RetryPolicy
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        int maxRetries = 3,
        TimeSpan? initialDelay = null)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);
        var random = new Random();
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                // Exponential backoff with jitter
                var jitter = random.Next(0, 1000);
                var backoff = delay * Math.Pow(2, attempt) + TimeSpan.FromMilliseconds(jitter);
                
                _logger.LogWarning(ex, "Attempt {Attempt} failed, retrying in {Backoff}ms", 
                    attempt + 1, backoff.TotalMilliseconds);
                
                await Task.Delay(backoff);
            }
        }
        
        throw new Exception($"Operation failed after {maxRetries} retries");
    }
}
```

### Bulkhead Pattern

```csharp
public class BulkheadIsolation
{
    private readonly Dictionary<string, SemaphoreSlim> _bulkheads = new();
    
    public BulkheadIsolation()
    {
        // Separate resource pools for different job types
        _bulkheads["HighPriority"] = new SemaphoreSlim(50);
        _bulkheads["Normal"] = new SemaphoreSlim(100);
        _bulkheads["LowPriority"] = new SemaphoreSlim(30);
    }
    
    public async Task<T> ExecuteInBulkheadAsync<T>(
        string bulkheadName, 
        Func<Task<T>> action,
        CancellationToken ct)
    {
        var semaphore = _bulkheads[bulkheadName];
        
        await semaphore.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            semaphore.Release();
        }
    }
}
```

### Dead Letter Queue

```csharp
public class DeadLetterQueueHandler
{
    public async Task HandleFailedEventAsync(Event failedEvent, Exception exception)
    {
        var dlqEntry = new DeadLetterEvent
        {
            OriginalEvent = failedEvent,
            FailureReason = exception.Message,
            FailureTime = DateTime.UtcNow,
            RetryCount = failedEvent.RetryCount,
            StackTrace = exception.StackTrace
        };
        
        await _context.DeadLetterQueue.AddAsync(dlqEntry);
        await _context.SaveChangesAsync();
        
        // Alert operations team
        await _alertService.SendAlertAsync(new Alert
        {
            Severity = AlertSeverity.High,
            Title = "Event moved to Dead Letter Queue",
            Description = $"Event {failedEvent.Id} failed after {failedEvent.RetryCount} retries"
        });
    }
    
    public async Task ReprocessDeadLetterAsync(Guid dlqId)
    {
        var dlqEntry = await _context.DeadLetterQueue.FindAsync(dlqId);
        if (dlqEntry == null) return;
        
        // Reset retry count and resubmit
        dlqEntry.OriginalEvent.RetryCount = 0;
        await _eventQueue.EnqueueAsync(dlqEntry.OriginalEvent);
        
        await _context.DeadLetterQueue.Remove(dlqEntry);
        await _context.SaveChangesAsync();
    }
}
```

## When to Apply This Skill

Use this skill when:
- Implementing multi-instance scheduler deployments
- Adding high availability and failover capabilities
- Scaling the system horizontally
- Designing fault-tolerant agent communication
- Implementing distributed locks and coordination
- Troubleshooting split-brain or consistency issues
- Planning disaster recovery and chaos testing
