---
name: scheduler-platform-engineering
description: Expertise in building and optimizing scheduling engines including cron parsing, dependency resolution, event triggering, resource-aware scheduling, and state machine design for job orchestration systems.
---

# Scheduler Platform Engineering

You have deep expertise in building and optimizing scheduling engines for job orchestration systems like AutoSys, Airflow, and Control-M.

## Cron Expression Parsing & Evaluation

- Parse complex cron expressions (5 and 6 field formats)
- Calculate next execution times efficiently
- Handle edge cases (leap years, DST transitions, timezone conversions)
- Implement quartz-style cron with advanced features (L, W, #)
- Support calendar-based scheduling with trading and holiday calendars

## Dependency Resolution Algorithms

- Topological sorting for DAG traversal
- Cycle detection in dependency graphs
- Critical path analysis for workflow chains
- Conditional dependencies (AND, OR, NOT logic)
- Cross-workflow dependencies
- Deadlock detection and prevention

**Implementation Pattern:**
```csharp
public async Task<List<Workflow>> ResolveDependencies(Guid workflowId)
{
    var visited = new HashSet<Guid>();
    var result = new List<Workflow>();
    
    async Task DFS(Guid id)
    {
        if (visited.Contains(id)) return;
        var workflow = await _repository.GetWorkflowAsync(id);
        visited.Add(id);
        
        foreach (var depId in workflow.Dependencies)
            await DFS(depId);
        
        result.Add(workflow);
    }
    
    await DFS(workflowId);
    return result;
}
```

## Event Processing & Triggering

- **Time-based triggers**: Cron schedules with calendar awareness
- **File-based triggers**: File arrival, modification, size checks
- **Status-based triggers**: Parent job success/failure/completion
- **API/webhook triggers**: External system event integration
- **Composite triggers**: Multiple conditions with AND/OR logic

## Resource-Aware Scheduling

- Multi-dimensional bin packing for resource allocation
- Priority queue implementation with starvation prevention
- Capacity planning and quota management
- Concurrency control (max parallel jobs per agent/group)
- Fair-share scheduling algorithms
- Work-stealing for load balancing

## State Machine Design

Design robust state machines for job lifecycle:

```
PENDING → WAITING (dependencies not met)
PENDING → READY (dependencies met, awaiting resources)
READY → DISPATCHED (sent to agent)
DISPATCHED → RUNNING (agent started execution)
RUNNING → SUCCESS (exit code 0)
RUNNING → FAILED (non-zero exit code)
RUNNING → TIMEOUT (exceeded max duration)
RUNNING → TERMINATED (manual stop)
FAILED → RETRYING (if retries remaining)
```

- Idempotent state updates
- Compensating transactions for rollback
- State persistence and recovery after crashes
- Atomic state transitions with proper locking

## Scheduling Algorithms

**Priority-based scheduling:**
```csharp
public async Task<List<Workflow>> GetSchedulableJobs()
{
    var readyJobs = await _repository.GetJobsByStatus(JobStatus.READY);
    
    // Apply resource constraints
    var filtered = await ApplyResourceConstraints(readyJobs);
    
    // Sort by priority, then by age to prevent starvation
    return filtered
        .OrderByDescending(j => j.Priority)
        .ThenBy(j => j.CreatedAt)
        .ToList();
}
```

## When to Apply This Skill

Use this skill when:
- Implementing the scheduler core engine
- Adding new trigger types or scheduling features
- Optimizing dependency resolution performance
- Designing state management for workflow execution
- Building resource allocation and quota systems
- Debugging scheduling issues or race conditions
