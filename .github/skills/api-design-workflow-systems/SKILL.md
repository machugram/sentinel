---
name: api-design-workflow-systems
description: RESTful and real-time API design for workflow management systems including resource modeling, idempotency, WebSocket communication, and API versioning strategies.
---

# API Design for Workflow Systems

You are an expert in designing RESTful and real-time APIs specifically for job orchestration and workflow management systems.

## REST API Best Practices

### Resource Modeling

```csharp
// Clean resource hierarchy
[ApiController]
[Route("api/v1/workflows")]
public class WorkflowsController : ControllerBase
{
    // GET /api/v1/workflows
    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkflowDto>>> ListWorkflows(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var query = _repository.GetWorkflowsQuery();
        
        if (!string.IsNullOrEmpty(status))
            query = query.Where(w => w.Status == status);
        
        if (!string.IsNullOrEmpty(search))
            query = query.Where(w => w.Name.Contains(search));
        
        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => _mapper.Map<WorkflowDto>(w))
            .ToListAsync();
        
        return Ok(new PagedResult<WorkflowDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }
    
    // GET /api/v1/workflows/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowDto>> GetWorkflow(Guid id)
    {
        var workflow = await _repository.GetWorkflowAsync(id);
        if (workflow == null)
            return NotFound(new ProblemDetails 
            { 
                Title = "Workflow not found",
                Detail = $"No workflow exists with ID {id}"
            });
        
        return Ok(_mapper.Map<WorkflowDto>(workflow));
    }
    
    // POST /api/v1/workflows
    [HttpPost]
    public async Task<ActionResult<WorkflowDto>> CreateWorkflow(
        [FromBody] CreateWorkflowRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
    {
        // Idempotency check
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await _cache.GetAsync<WorkflowDto>($"idempotency:{idempotencyKey}");
            if (existing != null)
                return Ok(existing);
        }
        
        var workflow = await _workflowService.CreateAsync(request);
        var dto = _mapper.Map<WorkflowDto>(workflow);
        
        // Cache for idempotency
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            await _cache.SetAsync($"idempotency:{idempotencyKey}", dto, TimeSpan.FromHours(24));
        }
        
        return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, dto);
    }
    
    // PATCH /api/v1/workflows/{id}
    [HttpPatch("{id}")]
    public async Task<ActionResult<WorkflowDto>> UpdateWorkflow(
        Guid id, 
        [FromBody] JsonPatchDocument<WorkflowDto> patch)
    {
        var workflow = await _repository.GetWorkflowAsync(id);
        if (workflow == null)
            return NotFound();
        
        var dto = _mapper.Map<WorkflowDto>(workflow);
        patch.ApplyTo(dto, ModelState);
        
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        _mapper.Map(dto, workflow);
        await _repository.UpdateAsync(workflow);
        
        return Ok(dto);
    }
}

// Nested resources
[ApiController]
[Route("api/v1/workflows/{workflowId}/runs")]
public class WorkflowRunsController : ControllerBase
{
    // GET /api/v1/workflows/{workflowId}/runs
    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkflowRunDto>>> ListRuns(
        Guid workflowId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var runs = await _repository.GetRunsForWorkflowAsync(workflowId, page, pageSize);
        return Ok(runs);
    }
    
    // POST /api/v1/workflows/{workflowId}/runs
    [HttpPost]
    public async Task<ActionResult<WorkflowRunDto>> TriggerRun(
        Guid workflowId,
        [FromBody] TriggerWorkflowRequest request)
    {
        var run = await _workflowService.TriggerAsync(workflowId, request);
        return CreatedAtAction("GetRun", "Runs", new { id = run.Id }, run);
    }
}
```

### Idempotency Keys for Safe Retries

```csharp
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        
        if (!string.IsNullOrEmpty(idempotencyKey) && 
            context.Request.Method != "GET")
        {
            var cacheKey = $"idempotency:{idempotencyKey}";
            var cachedResponse = await _cache.GetStringAsync(cacheKey);
            
            if (cachedResponse != null)
            {
                // Return cached response
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(cachedResponse);
                return;
            }
            
            // Capture response
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            
            await _next(context);
            
            // Cache successful responses (2xx)
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                var responseText = await new StreamReader(responseBody).ReadToEndAsync();
                await _cache.SetStringAsync(cacheKey, responseText, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                });
                
                responseBody.Seek(0, SeekOrigin.Begin);
            }
            
            await responseBody.CopyToAsync(originalBodyStream);
        }
        else
        {
            await _next(context);
        }
    }
}
```

### Cursor-Based Pagination

```csharp
public class CursorPaginationService
{
    public async Task<CursorPagedResult<WorkflowRunDto>> GetRunsAsync(
        string? cursor = null,
        int limit = 20)
    {
        var query = _context.WorkflowRuns
            .OrderByDescending(r => r.ScheduledTime)
            .ThenByDescending(r => r.Id);
        
        // Apply cursor filter
        if (!string.IsNullOrEmpty(cursor))
        {
            var (timestamp, id) = DecodeCursor(cursor);
            query = query.Where(r => 
                r.ScheduledTime < timestamp || 
                (r.ScheduledTime == timestamp && r.Id.CompareTo(id) < 0));
        }
        
        var items = await query.Take(limit + 1).ToListAsync();
        var hasMore = items.Count > limit;
        
        if (hasMore)
            items = items.Take(limit).ToList();
        
        var nextCursor = hasMore 
            ? EncodeCursor(items.Last().ScheduledTime, items.Last().Id)
            : null;
        
        return new CursorPagedResult<WorkflowRunDto>
        {
            Items = items.Select(r => _mapper.Map<WorkflowRunDto>(r)).ToList(),
            NextCursor = nextCursor,
            HasMore = hasMore
        };
    }
    
    private string EncodeCursor(DateTime timestamp, Guid id)
    {
        var data = $"{timestamp:O}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
    }
    
    private (DateTime, Guid) DecodeCursor(string cursor)
    {
        var data = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        var parts = data.Split('|');
        return (DateTime.Parse(parts[0]), Guid.Parse(parts[1]));
    }
}
```

### Field Selection and Sparse Fieldsets

```csharp
[HttpGet]
public async Task<ActionResult<List<object>>> ListWorkflows(
    [FromQuery] string? fields = null)
{
    var query = _context.Workflows.AsQueryable();
    
    if (string.IsNullOrEmpty(fields))
    {
        // Return full objects
        return Ok(await query.Select(w => new WorkflowDto
        {
            Id = w.Id,
            Name = w.Name,
            Description = w.Description,
            Schedule = w.Schedule,
            Status = w.Status,
            CreatedAt = w.CreatedAt
        }).ToListAsync());
    }
    
    // Parse requested fields
    var requestedFields = fields.Split(',').Select(f => f.Trim()).ToList();
    
    // Build dynamic projection
    var result = await query.Select(w => new
    {
        Id = requestedFields.Contains("id") ? w.Id : Guid.Empty,
        Name = requestedFields.Contains("name") ? w.Name : null,
        Status = requestedFields.Contains("status") ? w.Status : null
    }).ToListAsync();
    
    return Ok(result);
}
```

### Rate Limiting

```csharp
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString();
        var key = $"ratelimit:{clientId}:{DateTime.UtcNow:yyyyMMddHHmm}";
        
        var currentCount = await _cache.GetStringAsync(key);
        var count = int.Parse(currentCount ?? "0");
        
        const int maxRequests = 60;  // 60 per minute
        
        if (count >= maxRequests)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 429,
                Title = "Too Many Requests",
                Detail = $"Rate limit exceeded. Maximum {maxRequests} requests per minute."
            });
            return;
        }
        
        await _cache.SetStringAsync(key, (count + 1).ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        });
        
        context.Response.Headers["X-RateLimit-Limit"] = maxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = (maxRequests - count - 1).ToString();
        
        await _next(context);
    }
}
```

## Real-Time Communication

### SignalR for Live Updates

```csharp
public class WorkflowHub : Hub
{
    public async Task SubscribeToWorkflow(Guid workflowId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"workflow:{workflowId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to workflow {WorkflowId}", 
            Context.ConnectionId, workflowId);
    }
    
    public async Task UnsubscribeFromWorkflow(Guid workflowId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workflow:{workflowId}");
    }
}

// Broadcasting updates
public class WorkflowUpdateNotifier
{
    private readonly IHubContext<WorkflowHub> _hubContext;
    
    public async Task NotifyWorkflowStatusChangedAsync(Guid workflowId, string newStatus)
    {
        await _hubContext.Clients
            .Group($"workflow:{workflowId}")
            .SendAsync("WorkflowStatusChanged", new
            {
                WorkflowId = workflowId,
                Status = newStatus,
                Timestamp = DateTime.UtcNow
            });
    }
    
    public async Task StreamLogsAsync(Guid runId, string logLine)
    {
        await _hubContext.Clients
            .Group($"run:{runId}")
            .SendAsync("LogReceived", new
            {
                RunId = runId,
                Log = logLine,
                Timestamp = DateTime.UtcNow
            });
    }
}
```

### Server-Sent Events for Log Streaming

```csharp
[HttpGet("runs/{runId}/logs/stream")]
public async Task StreamLogs(Guid runId)
{
    Response.ContentType = "text/event-stream";
    Response.Headers.Add("Cache-Control", "no-cache");
    Response.Headers.Add("Connection", "keep-alive");
    
    await foreach (var logLine in _logService.StreamLogsAsync(runId, HttpContext.RequestAborted))
    {
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { log = logLine, timestamp = DateTime.UtcNow })}\n\n");
        await Response.Body.FlushAsync();
    }
}
```

## API Versioning & Evolution

```csharp
// Startup.cs
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Controllers
[ApiController]
[Route("api/v{version:apiVersion}/workflows")]
[ApiVersion("1.0")]
public class WorkflowsV1Controller : ControllerBase
{
    // V1 implementation
}

[ApiController]
[Route("api/v{version:apiVersion}/workflows")]
[ApiVersion("2.0")]
public class WorkflowsV2Controller : ControllerBase
{
    // V2 with breaking changes
}

// Deprecation
[ApiVersion("1.0", Deprecated = true)]
public class WorkflowsV1Controller : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        Response.Headers.Add("X-API-Deprecation-Date", "2026-12-31");
        Response.Headers.Add("X-API-Deprecation-Link", "https://docs.sentinel.io/migration/v2");
        // ... implementation
    }
}
```

## When to Apply This Skill

Use this skill when:
- Implementing REST API controllers and endpoints
- Adding real-time features with SignalR or WebSockets
- Designing API versioning strategies
- Implementing idempotency for critical operations
- Adding pagination, filtering, and field selection
- Building rate limiting and throttling
- Streaming logs or real-time status updates
