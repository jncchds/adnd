# Design: Replace MediatR with RabbitMQ Event Bus

**Date:** 2026-06-13  
**Status:** ❌ Abandoned  
**Priority:** High — removes a heavy external dependency in favor of a battle-tested, durable pub/sub system
**Reason:** Superseded by MassTransit approach. MediatR dependency removed (commit `31d3cb5`). MassTransit 8.3.6 used instead of raw RabbitMQ.Client.

---

## 1. Problem Statement

The current event bus uses MediatR `INotification` records with in-memory dispatch. This works for development but has limitations:

- **No durability:** Events published during a crash are lost — handlers never fire
- **No retry semantics:** A failing handler blocks the entire game queue (queue poisoning)
- **No observability:** Can't query "what events were published for this game?"
- **Heavy dependency:** MediatR adds ~100KB to the binary and a runtime reflection cost for assembly scanning
- **No ordering guarantees:** MediatR doesn't guarantee in-order delivery across async handlers

## 2. Solution Overview

Replace MediatR with a custom `IEventBus` backed by RabbitMQ. Each game gets its own queue with a dead-letter queue for failed handlers. Events are persisted to an `EventRecord` table for crash recovery.

**Key benefits:**
- ✅ Durable event storage — survives restarts
- ✅ Per-game queue isolation — broken handlers don't block other games
- ✅ Automatic retry with exponential backoff (max 3)
- ✅ DLQ for manual inspection and replay
- ✅ Queryable event log in the database
- ✅ Single lightweight dependency (RabbitMQ client)

## 3. Architecture

### 3.1 Components

```
┌─────────────────────────────────────────────────────────┐
│                     Adnd.Server                        │
│                                                         │
│  ┌──────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │Controllers│  │  GameHub     │  │  Services       │  │
│  │  /Hubs    │  │              │  │                 │  │
│  └────┬─────┘  └──────┬───────┘  └────────┬────────┘  │
│       │               │                    │           │
│       ▼               ▼                    │           │
│  ┌──────────────────────────────────────────┤           │
│  │         IEventBus (abstraction)         │           │
│  └──────────────────┬───────────────────────┤           │
│                     │                       │           │
│      ┌──────────────┴──────────────┐        │           │
│      ▼                             ▼        │           │
│ ┌──────────┐              ┌───────────────┐ │           │
│ │RabbitMq  │              │EventBusWorker │ │           │
│ │EventBus  │              │(background)   │ │           │
│ └────┬─────┘              └───────┬───────┘ │           │
│      │                            │          │           │
│      ▼                            ▼          │           │
│  ┌──────────┐              ┌───────────────┐ │           │
│  │ RabbitMQ │◄─────────────│EventRecord    │ │           │
│  │  (MQ)    │              │(DB table)     │ │           │
│  └────┬─────┘              └───────────────┘ │           │
│       │                                      │           │
└───────┼──────────────────────────────────────┼───────────┘
        │                                      │
        ▼                                      ▼
┌──────────────────┐              ┌───────────────────────┐
│  RabbitMQ        │              │  PostgreSQL           │
│  Container       │              │  (EventRecord table)  │
│                  │              │                       │
│  game.{GameId}   │              │  EventRecord          │
│  dlq.game.{GameId}│             │  ──────────────────   │
│  dlq.worker      │              │  Id, GameId,          │
│                  │              │  EventType, Payload,  │
│                  │              │  Status, RetryCount,  │
│                  │              │  CreatedAt            │
└──────────────────┘              └───────────────────────┘
```

### 3.2 Data Flow

1. **Publish:** Controller/Hub calls `eventBus.PublishAsync(new GameStarted(...))`
2. **Store:** `RabbitMqEventBus` writes `EventRecord` with status `Pending`, generates `CorrelationId`
3. **Dispatch:** `EventBusWorker` (background service) polls `Pending` records, publishes to RabbitMQ, updates status to `Published`
4. **Process:** RabbitMQ delivers to `game.{GameId}` queue, handlers receive and process
5. **Ack/Nack:** Handler acks → status `Acknowledged`. Handler nacks/timeout → retry count +1, max 3, then `Failed` → DLQ
6. **Recovery:** On startup, `EventBusWorker` replays all `Pending` records from the previous run

### 3.3 RabbitMQ Topology

| Entity | Name | Purpose |
|--------|------|---------|
| Exchange | `adnd.events` | Direct exchange, routing key = `game.{GameId}` |
| Game queue | `game.{GameId}` | Events for a specific game |
| DLQ | `dlq.game.{GameId}` | Failed messages after retry exhaustion |
| DLQ worker | `dlq.worker` | Aggregates all DLQs for admin review |
| Routing key | `game.{GameId}` | Routes events to the correct game queue |

**Queue declaration:** Queues are declared lazily on first publish for a game. Cleaned up when the game is archived.

## 4. Event Record Model

### 4.1 New Table

```csharp
public class EventRecord
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public EventStatus Status { get; set; }
    public string? CorrelationId { get; set; }
    public int RetryCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? AckedAt { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum EventStatus
{
    Pending,
    Published,
    Acknowledged,
    Failed
}
```

**Indexes:**
- `(GameId, Status)` — for `EventBusWorker` polling
- `(Status, CreatedAt)` — for pending/failed event queries
- `(CorrelationId)` — for deduplication

### 4.2 Existing Tables to Remove

Since the project is not yet in production:

- **`AgentCall` table** — currently used for `AgentCallQueued` events. The wakeup pattern is replaced by RabbitMQ's native delivery mechanism. The `AgentCall` model will be repurposed for GM agent tool calls (separate concern).
- **`GMTool` table** — no longer needed if tool calls are handled via the event bus. (Verify this is safe.)

**Decision:** Remove `AgentCall` and `GMTool` tables. Replace their functionality with the event bus + new `ToolCall` model if needed.

## 5. Handler Registration

### 5.1 Interface

```csharp
public interface IGameEvent
{
    Guid GameId { get; }
}

public interface IEventHandler<TEvent> where TEvent : IGameEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct = default);
}
```

### 5.2 Registration (Assembly Scanning)

```csharp
builder.Services.Scan(scan => scan
    .FromAssemblyOf<GameStarted>()
    .AddClasses(c => c.AssignableTo(typeof(IEventHandler<>)))
    .AsImplementedInterfaces()
    .WithSingletonLifetime());
```

This registers all `IEventHandler<T>` implementations from the same assembly as `GameStarted` (the events assembly). No manual registration needed.

### 5.3 Handler Migration

All existing MediatR handlers migrate to `IEventHandler<>`:

| Old Handler | New Handler |
|-------------|-------------|
| `GameLifecycleHandler` | `GameLifecycleHandler : IEventHandler<GameStarted>, IEventHandler<GamePaused>, ...` |
| `PlayerHandler` | `PlayerHandler : IEventHandler<PlayerJoined>, IEventHandler<PlayerLeft>` |
| `GameActionHandler` | `GameActionHandler : IEventHandler<CombatStarted>, IEventHandler<CombatEnded>, ...` |
| `ChatHandler` | `ChatHandler : IEventHandler<MessageSent>, IEventHandler<WhisperSent>, ...` |
| `SessionHandler` | `SessionHandler : IEventHandler<SessionCreated>, IEventHandler<SessionClosed>` |
| `PlotWeaverHandler` | `PlotWeaverHandler : IEventHandler<GameStarted>, IEventHandler<CombatStarted>, ...` |
| `AgentCallQueuedHandler` | **Removed** — wakeup handled by RabbitMQ delivery |

**Note:** Each handler class can implement multiple `IEventHandler<>` interfaces. The `RabbitMqEventBus` routes by event type.

## 6. EventBus Interface

```csharp
public interface IEventBus
{
    /// <summary>
    /// Publish an event to the specified game's queue.
    /// Event is persisted to EventRecord table and dispatched via RabbitMQ.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : IGameEvent;
    
    /// <summary>
    /// Subscribe a handler to a specific event type.
    /// Used for dynamic subscription (e.g., per-game handlers).
    /// </summary>
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent;
    
    /// <summary>
    /// Unsubscribe a handler from a specific event type.
    /// </summary>
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent;
}
```

## 7. EventBusWorker (Background Service)

```csharp
public class EventBusWorker : BackgroundService
{
    private readonly AppDbContext _context;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EventBusWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Phase 1: Replay pending events from previous run
        await ReplayPendingEvents(stoppingToken);
        
        // Phase 2: Continuous polling
        while (!stoppingToken.IsCancellationRequested)
        {
            var pending = await _context.EventRecords
                .Where(e => e.Status is EventStatus.Pending or EventStatus.Failed)
                .OrderBy(e => e.CreatedAt)
                .Take(100)
                .ToListAsync(stoppingToken);
            
            foreach (var record in pending)
            {
                await DispatchEvent(record, stoppingToken);
            }
            
            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
    
    private async Task ReplayPendingEvents(CancellationToken ct)
    {
        var pending = await _context.EventRecords
            .Where(e => e.Status == EventStatus.Pending)
            .ToListAsync(ct);
        
        _logger.LogInformation("Replaying {Count} pending events from previous run", pending.Count);
        
        foreach (var record in pending)
        {
            await DispatchEvent(record, ct);
        }
    }
    
    private async Task DispatchEvent(EventRecord record, CancellationToken ct)
    {
        // ... publish to RabbitMQ, update status
    }
}
```

## 8. Error Handling & Retry

### 8.1 Retry Policy

- **Max retries:** 3
- **Backoff:** Exponential (1s, 2s, 4s)
- **On failure:** Event moves to `dlq.game.{GameId}` queue
- **EventRecord status:** → `Failed`, `Error` column populated with last exception

### 8.2 DLQ Worker

```csharp
public class DlqWorker : BackgroundService
{
    // Polls dlq.worker queue every 30s
    // Moves failed events to EventRecord with status Failed for admin review
    // Admin endpoint to retry or delete DLQ events
}
```

### 8.3 Idempotency

- `CorrelationId` column prevents duplicate processing
- Handlers check for duplicate `CorrelationId` before processing
- Existing guards (e.g., `HasInitialThreadsAsync`) remain in place

## 9. Event Types (No Changes)

Event records keep their current `record` types — we just remove the `: INotification` base class. This preserves all existing event type definitions.

**Changes to event types:**
- Remove `: INotification` from all event records
- Add `: IGameEvent` base (requires `Guid GameId` property on all events)
- All events already have `GameId` — no structural changes needed

## 10. Cleanup: AgentCallQueued Pattern

The `AgentCallQueued` event wakes up the GameAgent when a new call is queued. This pattern is replaced by RabbitMQ's native message delivery:

**Before:**
```csharp
// Controller/Service
await _mediator.Publish(new AgentCallQueued(gameId, callId));

// Handler
_agentManager.OnAgentCallQueued(gameId, callId);
```

**After:**
```csharp
// Controller/Service
await _eventBus.PublishAsync(new AgentCallQueued(gameId, callId));

// EventBusWorker dispatches to RabbitMQ queue
// GameAgent polls RabbitMQ directly (or uses the delivery callback)
```

The `AgentCall` model is repurposed for GM tool calls. The `GMTool` table is removed.

## 11. Migration Plan

### Phase 1: Add RabbitMQ Infrastructure

1. Add RabbitMQ to `docker-compose.yml`
2. Add `RabbitMQ.Client` NuGet package
3. Add `EventRecord` table and migration
4. Implement `RabbitMqEventBus` and `EventBusWorker`
5. Add `IEventHandler<>` interface and registration
6. Add configuration for RabbitMQ connection

### Phase 2: Migrate Handlers

1. Migrate each handler class to implement `IEventHandler<>`
2. Update `RabbitMqEventBus` to route events to handlers
3. Test each handler migration individually

### Phase 3: Remove MediatR

1. Remove all `using MediatR` statements
2. Remove `: INotification` from event records
3. Remove MediatR registration from `Program.cs`
4. Remove `AgentCallQueuedHandler` (replaced by RabbitMQ delivery)
5. Remove `AgentCall` table (repurpose for tool calls)
6. Remove `GMTool` table (repurpose for tool calls)
7. Remove MediatR NuGet package
8. Update `docker-compose.yml` if any cleanup needed

### Phase 4: Verify & Clean Up

1. Run full test suite
2. Verify event durability (kill server, check replay)
3. Verify DLQ handling
4. Update documentation
5. Verify all controllers/hubs use `IEventBus` instead of `IMediator`

## 12. Docker Configuration

```yaml
services:
  rabbitmq:
    image: rabbitmq:3-management
    container_name: adnd-rabbitmq
    networks:
      - adnd-network
    environment:
      RABBITMQ_DEFAULT_USER: adnd
      RABBITMQ_DEFAULT_PASS: adnd
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    ports:
      - "15672:15672"   # Management UI (exposed for debugging)
      - "5672:5672"     # AMQP (exposed for debugging)
    deploy:
      resources:
        limits:
          memory: 256M
```

**Note:** Both ports are exposed in `docker-compose.yml` for debugging. In production, only `15672` (management) should be exposed — `5672` should be internal-only.

## 13. App Settings

```json
{
  "RabbitMq": {
    "Host": "rabbitmq",
    "Port": 5672,
    "Username": "adnd",
    "Password": "adnd",
    "VirtualHost": "/adnd",
    "RetryDelayMs": 1000,
    "MaxRetries": 3,
    "HeartbeatSeconds": 60
  }
}
```

## 14. Testing Strategy

1. **Unit tests:** Mock `IEventBus` in controllers/hubs
2. **Integration tests:** Use TestContainers for RabbitMQ
3. **Durability tests:** Publish events, kill server, verify replay
4. **DLQ tests:** Simulate handler failure, verify DLQ routing
5. **Per-game isolation tests:** Verify events for Game A don't reach Game B

## 15. Risk Assessment

| Risk | Mitigation |
|------|------------|
| RabbitMQ connection instability | Polly retry policies, graceful degradation |
| Event ordering within a game | RabbitMQ guarantees per-queue ordering |
| Queue poisoning by one game | Per-game DLQ isolates failures |
| EventRecord table bloat | Periodic cleanup of `Acknowledged` events (>7 days) |
| Handler deserialization failures | `EventType` column enables type-safe deserialization |
| RabbitMQ single point of failure | Not in scope for v1 — add clustering later if needed |

## 16. Open Questions

1. **EventRecord retention:** How long to keep acknowledged events? Suggestion: 7 days, then purge.
2. **GMTool table removal:** Should we keep `GMTool` and repurpose `AgentCall` instead? Need to verify current usage.
3. **Health check:** Add RabbitMQ health check to the app's health endpoint.
4. **Monitoring:** Should we add metrics (events published, events acked, DLQ size)?

---

*Design validated by stakeholder on 2026-06-13.*
