# Reactive Saga Architecture — Design Document

> **Date:** 2026-06-13
> **Status:** ✅ Completed
> **Goal:** Eliminate all polling loops in GameAgent, replace with fully reactive RabbitMQ choreography using small composable saga handlers.
> **Implementation:** Implemented (commit `69f0ca8`). GameAgent rewritten as RabbitMQ consumer, saga handlers created, ToolCallCoordinator persisted.

---

## 1. Problem Statement

The current GameAgent uses an in-memory loop (`ManualResetEventSlim` + `ConcurrentQueue` + `ProcessLoopAsync`) to process agent calls. This design has several limitations:

- **Polling despite "event-driven" claims** — the loop blocks on `_signal.Wait()` but still requires a background thread per game
- **Monolithic handlers** — `HandleGMCall` is ~400 lines doing LLM calls, tool execution, follow-ups, and narrative in one method
- **No crash recovery for sagas** — multi-step workflows (LLM → tools → follow-up LLM → narrative) are in-memory; a crash loses the entire flow
- **No multi-instance support** — in-memory queue can't cross process boundaries

---

## 2. Core Design: Saga Choreography via RabbitMQ

### 2.1 High-Level Flow

```
AgentCallQueued
  └─► SagaOrchestratorHandler (decides what to do)
      └─► Publishes: LLMDispatchRequested
      └─► Publishes: ToolCallCoordinatorCreated (for multi-tool)
  └─► LLMDispatchHandler (calls LLM)
      └─► Publishes: LLMResponseReceived
  └─► LLMResponseHandler (parses response)
      └─► Publishes: ToolCallRequested (if tools needed)
      └─► Publishes: LLMFollowUpRequested (if follow-up needed)
      └─► Publishes: NarrativeReady (if narrative complete)
  └─► ToolExecutionHandler (executes tool)
      └─► Publishes: ToolCallCompleted
  └─► CoordinatorHandler (multi-tool coordinator)
      └─► Publishes: ToolCallRequested (next tool)
      └─► Publishes: LLMFollowUpRequested (all tools done)
  └─► LLMFollowUpHandler (follow-up LLM with tool results)
      └─► Publishes: NarrativeReady
  └─► NarrativeHandler (saves result, broadcasts to players)
      └─► Publishes: AgentCallCompleted
```

Each handler is 20-50 lines. Each publishes exactly one next event. The **AgentCall entity becomes a saga state machine** with a `CurrentStep` column tracking progress.

### 2.2 GameAgent as RabbitMQ Consumer

The GameAgent is refactored from a loop-based processor into a **consumer wrapper**:

```
GameAgent (new design)
├── RabbitMQ channel (per-game, durable)
├── BasicConsume on "agent.{gameId}" queue (prefetch=1)
├── Event handler: on BasicDeliver → deserialize → dispatch to handler
├── Auto-recovery: on channel close → redeclare queue + re-consume
└── Graceful shutdown: CancelBasicConsume on Dispose
```

**On startup**, the GameAgent:
1. Connects to RabbitMQ
2. Declares `agent.{gameId}` queue (durable, prefetch=1)
3. Queries DB for pending sagas (`CurrentStep != Completed/Failed`)
4. Resumes each saga from `CurrentStep` (emits appropriate event)

**On message receipt:**
1. Deserialize event type from RabbitMQ message
2. Dispatch to registered handler (via `HandlerRegistry`)
3. Handle handler result (success → continue, failure → DLQ or retry)

**Key properties:**
- `prefetch=1` ensures sequential processing (same as old loop)
- `autoAck=false` + manual `BasicAck` after successful processing
- On `BasicNack` (handler throws), message goes to DLQ

### 2.3 Multi-Tool Coordinator (Persisted)

The `ToolCallCoordinator` entity survives crashes:

```csharp
public class ToolCallCoordinator
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public Guid GameId { get; set; }
    public int TotalTools { get; set; }
    public int CurrentIndex { get; set; }    // next tool to execute (0-based)
    public string? ToolName { get; set; }    // current tool name
    public string? ToolArgs { get; set; }    // current tool args
    public List<ToolCallResult> CompletedTools { get; set; } = new();
    public CoordinatorStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum CoordinatorStatus { Active, Completed, Abandoned }

public class ToolCallResult
{
    public int Index { get; set; }
    public string ToolName { get; set; } = "";
    public string Result { get; set; } = "";
    public string? Error { get; set; }
}
```

**Flow with persistence:**
```
LLMResponseHandler:
  → Creates ToolCallCoordinator (total=3, index=0)
  → Publishes ToolCallRequested (tool #0)

ToolExecutionHandler (tool #0):
  → Executes tool
  → Updates coordinator: CompletedTools += result, CurrentIndex = 1
  → Publishes ToolCallCompleted

CoordinatorHandler (listener):
  → "Index 1/3, publish next"
  → Updates coordinator: CurrentIndex = 2
  → Publishes ToolCallRequested (tool #1)

[... crash here ...]

GameAgent on startup:
  → Finds coordinator: total=3, index=2, completed=[#0, #1]
  → Resumes: publishes ToolCallRequested (tool #2)
```

---

## 3. New Event Types

All in `GameEvents.cs`:

```csharp
// Saga lifecycle
public record AgentCallQueued(Guid SagaId, Guid GameId) : IGameEvent;

// LLM dispatch
public record LLMDispatchRequested(
    Guid SagaId,
    string SystemPrompt,
    string UserPrompt,
    string? Options) : IGameEvent;

// LLM response
public record LLMResponseReceived(
    Guid SagaId,
    string Response,
    bool HasToolCalls,
    int ToolCallCount) : IGameEvent;

// Tool execution
public record ToolCallRequested(
    Guid SagaId,
    int ToolIndex,
    string ToolName,
    string ToolArgs) : IGameEvent;

public record ToolCallCompleted(
    Guid SagaId,
    int ToolIndex,
    string Result,
    string? Error) : IGameEvent;

// Coordinator (multi-tool)
public record CoordinatorUpdated(
    Guid SagaId,
    int CurrentIndex,
    int TotalTools) : IGameEvent;

// Follow-up
public record LLMFollowUpRequested(
    Guid SagaId,
    List<ToolResult> ToolResults) : IGameEvent;

public record ToolResult(int Index, string ToolName, string Result, string? Error);

// Narrative
public record NarrativeReady(
    Guid SagaId,
    string Narrative) : IGameEvent;

// Finalization
public record AgentCallCompleted(Guid SagaId) : IGameEvent;
public record AgentCallFailed(Guid SagaId, string Error) : IGameEvent;
```

---

## 4. Handler Interface & Registry

### 4.1 Handler Interface

```csharp
public interface IEventHandler<TEvent> where TEvent : IGameEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct);
}
```

### 4.2 Handler Registry

Startup scan of `Adnd.Server.Handlers` namespace:

```csharp
public class HandlerRegistry
{
    private readonly Dictionary<string, List<Type>> _handlers;

    public HandlerRegistry()
    {
        var assembly = typeof(GameLifecycleHandler).Assembly;
        foreach (var type in assembly.GetTypes()
            .Where(t => t.Namespace == "Adnd.Server.Handlers"
                     && !t.IsAbstract && !t.IsInterface
                     && t.GetInterfaces().Any(i =>
                         i.IsGenericType &&
                         i.GetGenericTypeDefinition() == typeof(IEventHandler<>)))
        )
        {
            var eventType = type.GetInterfaces()
                .First(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .GetGenericArguments()[0];

            var key = eventType.FullName!;
            _handlers.GetOrAdd(key, _ => new List<Type>()).Add(type);
        }
    }

    public IReadOnlyDictionary<string, List<Type>> Handlers => _handlers;
}
```

### 4.3 Handler Classes

| Handler | Consumes | Produces | Lines |
|---------|----------|----------|-------|
| `SagaOrchestratorHandler` | `AgentCallQueued` | `LLMDispatchRequested` or `ToolCallCoordinatorCreated` | 30-50 |
| `LLMDispatchHandler` | `LLMDispatchRequested` | `LLMResponseReceived` | 30-50 |
| `LLMResponseHandler` | `LLMResponseReceived` | `ToolCallRequested` / `LLMFollowUpRequested` / `NarrativeReady` | 30-50 |
| `ToolExecutionHandler` | `ToolCallRequested` | `ToolCallCompleted` | 20-40 |
| `CoordinatorHandler` | `ToolCallCompleted` | `ToolCallRequested` (next) / `LLMFollowUpRequested` | 20-30 |
| `LLMFollowUpHandler` | `LLMFollowUpRequested` | `NarrativeReady` | 30-50 |
| `NarrativeHandler` | `NarrativeReady` | `AgentCallCompleted` | 20-30 |
| `AgentCallCompletedHandler` | `AgentCallCompleted` | (none — finalizes saga) | 10-20 |
| `AgentCallFailedHandler` | `AgentCallFailed` | (none — marks saga as failed) | 10-20 |

---

## 5. Error Handling

| Mode | What happens | Where |
|------|-------------|-------|
| **Transient** (LLM timeout, DB lock) | Retry the same event (up to 3x) | EventBusWorker wraps dispatch |
| **Permanent** (bad input, validation fail) | Mark saga as `Failed`, publish `AgentCallFailed` | Handler catches and publishes failure event |
| **Catastrophic** (process crash mid-saga) | Saga restarts from `CurrentStep` or coordinator state | GameAgent on startup checks saga/coordinator state |

**DLQ for handlers** — if a handler fails 3 times, the event goes to a `dlq.events` RabbitMQ queue. A separate `DLQProcessor` background service can replay them later (or alert the admin).

---

## 6. Files Modified

### Existing files modified:

| File | Changes |
|------|---------|
| `AgentCall.cs` | Add `CurrentStep` column, `SagaStep` enum, `ToolCallCoordinator` model, `ToolCallResult` class |
| `GameAgent.cs` | Complete rewrite: RabbitMQ consumer, saga resume logic |
| `IGameAgent.cs` | Remove `EnqueueCall`, `OnAgentCallQueued` |
| `GameAgentManager.cs` | Remove `OnAgentCallQueued`, `EnqueuePendingCallsAsync` |
| `AgentBus.cs` | Replace `_gameAgentManager.OnAgentCallQueued()` with `IEventBus.PublishAsync(new AgentCallQueued(...))` |
| `EventBusWorker.cs` | Remove `AgentCallQueued` special case, keep generic handler dispatch |
| `GameEvents.cs` | Add new saga event types |
| `Program.cs` | Register `HandlerRegistry`, new services, remove dead registrations |

### New files:

| File | Purpose |
|------|---------|
| `Handlers/SagaOrchestratorHandler.cs` | `AgentCallQueued` → determines action, emits `LLMDispatchRequested` |
| `Handlers/LLMDispatchHandler.cs` | Calls LLM provider, emits `LLMResponseReceived` |
| `Handlers/LLMResponseHandler.cs` | Parses response, creates coordinator or follow-up |
| `Handlers/ToolExecutionHandler.cs` | Executes tool via `_toolRegistry`, emits `ToolCallCompleted` |
| `Handlers/CoordinatorHandler.cs` | `ToolCallCompleted` listener, emits next tool or `LLMFollowUpRequested` |
| `Handlers/LLMFollowUpHandler.cs` | Follow-up LLM with tool results |
| `Handlers/NarrativeHandler.cs` | Saves narrative, broadcasts to players via SignalR |
| `Handlers/AgentCallCompletedHandler.cs` | Finalizes saga |
| `Handlers/AgentCallFailedHandler.cs` | Marks saga as failed |

---

## 7. What We Gain

| Before | After |
|--------|-------|
| `ProcessLoopAsync` with `ManualResetEventSlim` + `ConcurrentQueue` | RabbitMQ consumer with `prefetch=1` |
| One monolithic `HandleGMCall` (~400 lines) | 8-10 handlers, each 20-50 lines |
| In-memory queue bypasses persistence | Every event is a DB record + RabbitMQ message |
| Crash = lost saga state | Crash = resume from `CurrentStep` + coordinator state |
| Multi-instance impossible | Any instance can consume from any queue |
| No type-safe events | Structured events with typed payloads |

---

## 8. Migration Strategy

1. **Phase 1:** Add new event types, saga step enum, coordinator model, handler interface
2. **Phase 2:** Create all new handler classes
3. **Phase 3:** Rewrite GameAgent as RabbitMQ consumer
4. **Phase 4:** Update AgentBus to publish via IEventBus
5. **Phase 5:** Clean up dead code (remove `EnqueueCall`, `OnAgentCallQueued`, `EnqueuePendingCallsAsync`, special-case branches)
6. **Phase 6:** Create EF migration for new columns/entities
7. **Phase 7:** Integration test the full saga flow

---

## 9. Open Decisions

- [ ] DLQ processor: built into EventBusWorker or separate background service?
- [ ] Handler retry: per-handler config or global default (3x)?
- [ ] EventRecord cleanup: keep existing 7-day TTL or adjust for saga events?

---

*Draft — awaiting validation before implementation.*
