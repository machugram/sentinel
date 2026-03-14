---
name: event-driven-architecture
description: Designing reactive, loosely-coupled systems with message queues, event schemas, and publish-subscribe patterns for job orchestration event processing.
---

# Event-Driven Architecture

You are an expert in designing reactive, loosely-coupled event-driven systems for job orchestration platforms.

## Message Queue Integration

### RabbitMQ for Event Distribution

```csharp
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    
    public async Task PublishAsync<T>(T eventData, string routingKey) where T : class
    {
        var message = JsonSerializer.Serialize(new EventEnvelope<T>
        {
            EventId = Guid.NewGuid(),
            EventType = typeof(T).Name,
            Timestamp = DateTime.UtcNow,
            CorrelationId = Activity.Current?.Id,
            Payload = eventData
        });
        
        var body = Encoding.UTF8.GetBytes(message);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.MessageId = Guid.NewGuid().ToString();
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        
        _channel.BasicPublish(
            exchange: "sentinel.events",
            routingKey: routingKey,
            basicProperties: properties,
            body: body);
    }
}

public class RabbitMqEventConsumer : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var envelope = JsonSerializer.Deserialize<EventEnvelope>(message);
                
                await ProcessEventAsync(envelope);
                
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event");
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };
        
        _channel.BasicConsume(
            queue: "workflow.triggers",
            autoAck: false,
            consumer: consumer);
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

### Kafka for High-Throughput Event Streaming

```csharp
public class KafkaEventProducer
{
    private readonly IProducer<string, string> _producer;
    
    public async Task PublishEventAsync(WorkflowEvent evt)
    {
        var message = new Message<string, string>
        {
            Key = evt.WorkflowId.ToString(),  // Partition by workflow
            Value = JsonSerializer.Serialize(evt),
            Headers = new Headers
            {
                { "event-type", Encoding.UTF8.GetBytes(evt.EventType) },
                { "correlation-id", Encoding.UTF8.GetBytes(evt.CorrelationId) }
            },
            Timestamp = new Timestamp(evt.OccurredAt)
        };
        
        var result = await _producer.ProduceAsync("workflow-events", message);
        _logger.LogDebug("Event published to partition {Partition} at offset {Offset}", 
            result.Partition, result.Offset);
    }
}
```

### Topic-Based Routing

```csharp
// Exchange binding for routing patterns
public class EventTopologyConfigurator
{
    public void ConfigureTopology(IModel channel)
    {
        // Topic exchange for flexible routing
        channel.ExchangeDeclare(
            exchange: "sentinel.events",
            type: ExchangeType.Topic,
            durable: true);
        
        // Queues for different event types
        var queues = new[]
        {
            ("workflow.created", "workflow.created"),
            ("workflow.updated", "workflow.updated"),
            ("workflow.triggered", "workflow.#.triggered"),
            ("workflow.completed", "workflow.*.completed"),
            ("workflow.all", "workflow.#")
        };
        
        foreach (var (queue, routingPattern) in queues)
        {
            channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(queue, "sentinel.events", routingPattern);
        }
    }
}
```

### Exactly-Once Delivery Semantics

```csharp
public class ExactlyOnceEventProcessor
{
    private readonly HashSet<string> _processedMessageIds = new();
    private readonly IDistributedCache _cache;
    
    public async Task<bool> ProcessEventWithIdempotencyAsync(EventEnvelope envelope)
    {
        var messageId = envelope.EventId.ToString();
        var cacheKey = $"processed:event:{messageId}";
        
        // Check if already processed
        var alreadyProcessed = await _cache.GetStringAsync(cacheKey);
        if (alreadyProcessed != null)
        {
            _logger.LogDebug("Event {MessageId} already processed, skipping", messageId);
            return false;
        }
        
        try
        {
            // Process the event
            await HandleEventAsync(envelope);
            
            // Mark as processed (TTL 24 hours)
            await _cache.SetStringAsync(cacheKey, "1", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process event {MessageId}", messageId);
            throw;
        }
    }
}
```

## Event Schema Design

### Versioned Event Schemas

```csharp
// Base event envelope
public class EventEnvelope<T> where T : class
{
    public Guid EventId { get; set; }
    public string EventType { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public DateTime Timestamp { get; set; }
    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public T Payload { get; set; }
}

// V1 of workflow triggered event
public class WorkflowTriggeredEventV1
{
    public Guid WorkflowId { get; set; }
    public string TriggerType { get; set; }
    public DateTime ScheduledTime { get; set; }
}

// V2 with breaking change (add required field)
public class WorkflowTriggeredEventV2
{
    public Guid WorkflowId { get; set; }
    public string TriggerType { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string TriggeredBy { get; set; }  // New required field
    public Dictionary<string, string>? Parameters { get; set; }
}

// Schema evolution handler
public class EventSchemaUpgrader
{
    public object UpgradeSchema(EventEnvelope<object> envelope)
    {
        return envelope.EventType switch
        {
            "WorkflowTriggered" when envelope.SchemaVersion == 1 =>
                UpgradeWorkflowTriggeredV1ToV2(envelope.Payload),
            _ => envelope.Payload
        };
    }
    
    private WorkflowTriggeredEventV2 UpgradeWorkflowTriggeredV1ToV2(object v1Event)
    {
        var v1 = JsonSerializer.Deserialize<WorkflowTriggeredEventV1>(
            JsonSerializer.Serialize(v1Event));
        
        return new WorkflowTriggeredEventV2
        {
            WorkflowId = v1.WorkflowId,
            TriggerType = v1.TriggerType,
            ScheduledTime = v1.ScheduledTime,
            TriggeredBy = "system"  // Default for migrated events
        };
    }
}
```

### Correlation and Causation Tracking

```csharp
public class EventCorrelation
{
    public static EventEnvelope<T> CreateChildEvent<T>(
        EventEnvelope parentEvent, 
        T payload) where T : class
    {
        return new EventEnvelope<T>
        {
            EventId = Guid.NewGuid(),
            EventType = typeof(T).Name,
            Timestamp = DateTime.UtcNow,
            CorrelationId = parentEvent.CorrelationId,  // Same correlation chain
            CausationId = parentEvent.EventId.ToString(),  // Direct parent
            Payload = payload
        };
    }
    
    public async Task<List<EventEnvelope>> GetEventChainAsync(string correlationId)
    {
        return await _context.Events
            .Where(e => e.CorrelationId == correlationId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
    }
}
```

## Publish-Subscribe Patterns

### Fan-Out for Parallel Execution

```csharp
public class WorkflowFanOutExecutor
{
    public async Task ExecuteParallelWorkflowsAsync(Guid parentWorkflowId)
    {
        var parent = await _repository.GetWorkflowAsync(parentWorkflowId);
        var children = await _repository.GetChildWorkflowsAsync(parentWorkflowId);
        
        // Create run event for parent
        var parentRunId = Guid.NewGuid();
        var parentEvent = new EventEnvelope<WorkflowTriggeredEvent>
        {
            EventId = Guid.NewGuid(),
            CorrelationId = parentRunId.ToString(),
            Payload = new WorkflowTriggeredEvent { WorkflowId = parentWorkflowId }
        };
        
        // Fan-out: trigger all children in parallel
        var childEvents = children.Select(child => 
            EventCorrelation.CreateChildEvent(parentEvent, new WorkflowTriggeredEvent
            {
                WorkflowId = child.Id,
                TriggerType = "FanOut",
                ParentRunId = parentRunId
            })).ToList();
        
        // Publish all child events
        foreach (var childEvent in childEvents)
        {
            await _eventPublisher.PublishAsync(childEvent, $"workflow.{childEvent.Payload.WorkflowId}.triggered");
        }
        
        _logger.LogInformation("Fanned out to {Count} child workflows", childEvents.Count);
    }
}
```

### Request-Reply Pattern

```csharp
public class RequestReplyHandler
{
    public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
        TRequest request, 
        TimeSpan timeout)
    {
        var correlationId = Guid.NewGuid().ToString();
        var replyQueue = $"reply.{correlationId}";
        
        // Create temporary reply queue
        _channel.QueueDeclare(replyQueue, exclusive: true, autoDelete: true);
        
        var tcs = new TaskCompletionSource<TResponse>();
        
        // Setup reply consumer
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var response = JsonSerializer.Deserialize<TResponse>(
                Encoding.UTF8.GetString(ea.Body.ToArray()));
            tcs.TrySetResult(response);
        };
        
        _channel.BasicConsume(replyQueue, autoAck: true, consumer: consumer);
        
        // Send request
        var props = _channel.CreateBasicProperties();
        props.CorrelationId = correlationId;
        props.ReplyTo = replyQueue;
        
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        _channel.BasicPublish("", "request.queue", props, body);
        
        // Wait for response with timeout
        using var cts = new CancellationTokenSource(timeout);
        cts.Token.Register(() => tcs.TrySetCanceled());
        
        return await tcs.Task;
    }
}
```

### Long-Running Async Operations

```csharp
public class AsyncOperationHandler
{
    public async Task<Guid> StartLongRunningOperationAsync(WorkflowDefinition workflow)
    {
        var operationId = Guid.NewGuid();
        
        // Create operation tracking
        var operation = new AsyncOperation
        {
            Id = operationId,
            Status = OperationStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        
        await _context.AsyncOperations.AddAsync(operation);
        await _context.SaveChangesAsync();
        
        // Publish event to start processing
        await _eventPublisher.PublishAsync(new OperationStartedEvent
        {
            OperationId= operationId,
            WorkflowId = workflow.Id
        }, "operation.started");
        
        return operationId;
    }
    
    public async Task<OperationResult> GetOperationResultAsync(Guid operationId)
    {
        var operation = await _context.AsyncOperations.FindAsync(operationId);
        
        return operation.Status switch
        {
            OperationStatus.Running => new OperationResult { Status = "InProgress" },
            OperationStatus.Completed => new OperationResult 
            { 
                Status = "Completed",
                Result = operation.Result 
            },
            OperationStatus.Failed => new OperationResult 
            { 
                Status = "Failed",
                Error = operation.ErrorMessage 
            },
            _ => throw new InvalidOperationException()
        };
    }
}
```

## When to Apply This Skill

Use this skill when:
- Implementing event-driven workflows and triggers
- Integrating message queues (RabbitMQ, Kafka, Azure Service Bus)
- Designing event schemas with versioning
- Building fan-out/fan-in patterns for parallel execution
- Implementing async request-reply patterns
- Ensuring exactly-once or at-least-once delivery semantics
- Troubleshooting event ordering or lost messages
