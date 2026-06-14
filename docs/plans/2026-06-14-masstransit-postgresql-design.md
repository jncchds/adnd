# MassTransit PostgreSQL Transport Migration — Design Document

> **Date:** 2026-06-14  
> **Status:** Validated by stakeholder  
> **Goal:** Replace RabbitMQ entirely with MassTransit's PostgreSQL transport, implement full saga-based GM agent lifecycle, and provide comprehensive admin UI for monitoring.

---

## 1. Current State (on `main` branch)

### What's already done (commit `af6795a`)
- MassTransit 8.3.6 + MassTransit.RabbitMQ packages installed
- 7 MassTransit consumers written (`Consumers/` directory)
- `AgentSagaData` model — inherits `SagaStateMachineInstance` ✓
- `SagaDbContext` — extends `DbContext` (needs update to `MassTransit.EntityFramework.SagaDbContext`)
- Saga event types added to `GameEvents.cs`
- All handler code migrated from `RabbitMqEventBus.PublishToAgent()` → `IPublishEndpoint.Publish()`
- `RabbitMQ.Client` package removed (MassTransit.RabbitMQ provides its own)
- `RabbitMqEventBus.cs` deleted

### What's blocked / incomplete
- ❌ Saga registration in `Program.cs` is **commented out** (deferred due to MassTransit 8.x API)
- ❌ `SagaDbContext` extends `DbContext` — should extend `MassTransit.EntityFramework.SagaDbContext`
- ❌ `EventBusWorker` still uses RabbitMQ transport (not PostgreSQL)
- ❌ RabbitMQ still in `docker-compose.yml`
- ❌ `EventRecord` table still used for persistence
- ❌ No admin UI for saga/DLQ/queue monitoring
- ❌ `GameAgent.cs` still has 531 lines of direct RabbitMQ code

---

## 2. Architecture Overview

### 2.1 What Stays the Same

| Component | Status |
|-----------|--------|
| All 30+ event record types in `GameEvents.cs` | ✓ No changes |
| All handler logic (`GameLifecycleHandler`, `GameActionHandler`, etc.) | ✓ No changes |
| Controllers calling `IEventBus.PublishAsync()` | ✓ Interface stays |
| SignalR hub broadcasts | ✓ No changes |
| LLM provider system, tool registry, RAG, dice engine | ✓ No changes |
| MediatR event bus (parallel path) | ✓ No changes |
| AgentBus (agentic framework) | ✓ Refactored to use MassTransit |

### 2.2 What Changes

| Before | After |
|--------|-------|
| MassTransit.RabbitMQ transport | **MassTransit.PostgreSQL transport** |
| RabbitMQ in docker-compose | **Removed** |
| EventBusWorker (BackgroundService) | **Replaced by saga-driven architecture** |
| EventRecord table for persistence | **MassTransit message storage** |
| GameAgentManager (in-memory per-game agents) | **Saga recovery via MassTransit** |
| Manual retry logic in EventBusWorker | **MassTransit built-in retry** |
| No admin visibility into messaging | **Comprehensive admin UI** |
| AgentBus → direct AgentCall table writes | **AgentBus → MassTransit endpoints** |

### 2.3 High-Level Flow

```
GameHub / Controllers
  → IEventBus.PublishAsync(new GameStarted(...))
    → IPublishEndpoint.Publish()  [MassTransit → PostgreSQL queue]

PostgreSQL queue (game.events)
  → MassTransit consumer
    → Saga (GmAgentSaga) keyed by GameId
      → Route event to handler based on event type
      → Handler calls AgentBus for LLM/tool calls
      → AgentBus publishes LLM calls via MassTransit
      → Saga transitions to WaitingForConfirmation or Complete

Startup recovery:
  → MassTransit recovers sagas from PostgreSQL message storage
  → Any saga in Processing/WaitingForLLM is reactivated
  → Any saga in WaitingForConfirmation stays paused
```

---

## 3. Data Models & Saga

### 3.1 GmAgentSaga (Saga Data)

```
GmAgentSaga (saga data, persisted in PostgreSQL)
├── Id (Guid) — saga instance ID
├── GameId (Guid) — saga partition key
├── State (enum: Created, Processing, WaitingForConfirmation, WaitingForLLM, Complete, Failed)
├── CurrentStep (string: "llm_call", "tool_execution", "confirmation", "follow_up")
├── PendingCalls (List<ToolCallInfo>) — serialized tool calls awaiting confirmation
├── CurrentToolIndex (int) — which tool we're on
├── LlmProviderConfig (string: serialized LLMPreset ref)
├── CreatedAt, UpdatedAt
└── CorrelationId — links to the original AgentCall that started this saga
```

### 3.2 What This Replaces

| Before | After |
|--------|-------|
| `AgentCall.CurrentStep` | Saga state *is* the step tracking |
| `ToolCallCoordinator` | `PendingCalls` + `CurrentToolIndex` in the saga |
| `GameAgent` in-memory state | Saga is always persisted |

### 3.3 Key Design Decisions

- Saga is keyed by `GameId` — one active saga per game at a time
- `WaitingForConfirmation` state means the saga is paused, waiting for a player to confirm/decline a roll
- When confirmation arrives, the saga resumes from `CurrentToolIndex`
- On app restart, MassTransit's recovery mechanism replays any saga that was in `Processing` or `WaitingForLLM`

---

## 4. Data Flow

### 4.1 Event Publishing (from Hub/Controllers)

```
GameHub/GamesController
  → IEventBus.PublishAsync(new GameStarted(...))
  → EventBusWorker (new implementation)
    → IPublishEndpoint.Publish()  [MassTransit → PostgreSQL queue]
    → No EventRecord table — MassTransit handles durability
```

### 4.2 Event Consumption (per-game saga)

```
PostgreSQL queue (game.events)
  → MassTransit consumer
    → Saga (GmAgentSaga) keyed by GameId
      → If saga is "Created" → transition to "Processing"
      → If saga is "WaitingForConfirmation" → resume
      → Route event to handler based on event type:
        - GameLifecycleConsumer → GameLifecycleHandler
        - GameActionConsumer → GameActionHandler
        - ChatConsumer → ChatHandler
        - etc.
      → Handler calls AgentBus for LLM/tool calls
      → AgentBus publishes LLM calls via MassTransit
      → Saga transitions to "WaitingForConfirmation" or "Complete"
```

### 4.3 Agent Call Flow (LLM/tool orchestration)

```
AgentBus.SendCallAsync(call)
  → Publish<AgentCallQueued> to MassTransit
    → Saga receives AgentCallQueued
      → If saga is "WaitingForConfirmation" → resume from current tool
      → If saga is "Complete" → start new saga instance
      → Call LLM via provider (direct, not via mass transit — LLM calls are external)
      → If LLM returns tool calls → save to saga.PendingCalls
      → Transition to "WaitingForConfirmation"
      → Wait for ConfirmPlayerRoll/DeclinePlayerRoll event
      → Resume, execute tools, call LLM again for follow-up
      → Transition to "Complete"
```

### 4.4 Startup Recovery

```
App startup
  → MassTransit recovers sagas from PostgreSQL message storage
  → Any saga in "Processing" or "WaitingForLLM" is reactivated
  → Any saga in "WaitingForConfirmation" stays paused (waiting for user input)
  → GameAgentManager.StartAllActiveGamesAsync() removed — MassTransit handles it
```

---

## 5. Error Handling & Retries

### 5.1 Retry Policy per Endpoint

```
EventBusWorker.PublishAsync()
  → IPublishEndpoint.Publish()
    → If publish fails → MassTransit retry (3 × 500ms)
      → If all retries fail → message goes to masstransit.dead_letter queue
```

### 5.2 Saga Failure Handling

```
Saga processes event
  → Handler throws exception
    → MassTransit retries (3 × 500ms)
      → If all retries fail → saga transitions to "Failed" state
      → Message goes to masstransit.dead_letter queue
      → Saga instance stays in DB with Failed state
      → Admin UI can see failed sagas and retry
```

### 5.3 Key Changes from Current System

| Before | After |
|--------|-------|
| Manual retry logic in `EventBusWorker.PublishWithRetry()` | **MassTransit handles it** |
| Manual cleanup of old EventRecords | **MassTransit's message storage handles retention** |
| Manual dead-letter queue | **MassTransit provides `masstransit.dead_letter` automatically** |
| Saga failures invisible | **Visible in `mt_saga` PostgreSQL table** |
| Circuit breaker commented out | **Can be re-enabled** |

---

## 6. Admin UI

### 6.1 Active Sagas (per game)

| Field | Source |
|-------|--------|
| Game ID | `mt_saga.GameId` |
| Saga State | `mt_saga.CurrentState` |
| Current Step | `mt_saga.CurrentStep` |
| Pending Tool Calls count | `mt_saga.PendingCalls` (JSON) |
| Last Updated timestamp | `mt_saga.UpdatedAt` |

**Actions:** Pause, Resume, Force Complete, Restart

### 6.2 Dead Letter Queue

| Field | Source |
|-------|--------|
| Message type | `mt_message.MessageType` |
| Game ID | `mt_message.Headers['x-game-id']` |
| Error message | `masstransit.dead_letter.Error` |
| Failed count | `masstransit.dead_letter.RetryCount` |
| Timestamp of last failure | `masstransit.dead_letter.LastArrived` |

**Actions:** Retry (re-publishes to queue), Delete (removes from DLQ)

### 6.3 Queue Status

| Field | Source |
|-------|--------|
| Queue name | MassTransit queue topology |
| Message count | `pg_stat_user_tables` or MassTransit API |
| Depth | Queue length via MassTransit API |
| Consumer status | Active/inactive via MassTransit API |

### 6.4 Message History

| Field | Source |
|-------|--------|
| Per-game event log | `mt_message` table |
| Event type, timestamp, status | `mt_message` columns |
| Filtered by event type, date range | Query parameters |
| Click to see full message payload | `mt_message.Body` |

### 6.5 Admin Endpoints

```
GET    /admin/:id/saga-status       → Current saga state for this game
POST   /admin/:id/saga-pause        → Pause saga (transition to Paused)
POST   /admin/:id/saga-resume       → Resume saga (transition to Processing)
POST   /admin/:id/saga-force-complete → Force saga to Complete
POST   /admin/:id/saga-restart      → Restart saga from beginning
GET    /admin/:id/dlq               → Dead letter messages for this game
POST   /admin/:id/dlq/retry         → Retry a specific DLQ message
POST   /admin/:id/dlq/delete        → Delete a specific DLQ message
GET    /admin/:id/queue-status      → Queue depths and consumer status
GET    /admin/:id/message-history   → Event message history for this game
```

---

## 7. Implementation Phases

### Phase 1: PostgreSQL Transport Setup

1. Add `MassTransit.PostgreSQL` package to `Adnd.Server.csproj`
2. Replace `AddRabbitMq` with `AddPostgresMessageStorage` + `AddPostgresQueue` in `Program.cs`
3. Update `IEventBus` interface comment (remove RabbitMQ reference)
4. Update `docker-compose.yml` — remove RabbitMQ service
5. Update `docker-compose.yml` — remove RabbitMQ env vars from app service
6. Update `EventBusWorker` — remove RabbitMQ-specific code

### Phase 2: Saga Implementation

1. Update `SagaDbContext` to extend `MassTransit.EntityFramework.SagaDbContext`
2. Create `GmAgentSaga` state machine class
3. Register saga in `Program.cs` with EF Core repository
4. Implement saga recovery on startup (via MassTransit's built-in recovery)
5. Create migration for saga tables

### Phase 3: EventBusWorker Refactoring

1. Replace `EventBusWorker` to use MassTransit directly (no manual EventRecord persistence)
2. Remove `PublishWithRetry` (MassTransit handles retry)
3. Remove `CleanupOldAcknowledgedRecords` (MassTransit handles retention)
4. Remove `ReplayPendingEvents` (MassTransit handles recovery)
5. Keep `IEventBus` interface for compatibility

### Phase 4: Consumer Updates

1. Update consumers to work with PostgreSQL transport
2. Configure receive endpoints for PostgreSQL
3. Update `GameAgent.cs` — remove direct RabbitMQ code
4. Update `GameAgentManager.cs` — remove startup recovery (MassTransit handles it)

### Phase 5: Admin UI

1. Add saga status endpoints to `AdminController.cs`
2. Add DLQ management endpoints
3. Add queue status endpoints
4. Add message history endpoints
5. Update frontend to show new admin sections

### Phase 6: Cleanup

1. Remove `EventRecord` table and `EventStatus` enum
2. Remove `EventBusWorker` (replaced by saga-driven architecture)
3. Remove old consumers if replaced by saga
4. Update `AGENTS.md` with new architecture documentation
5. Update `README.md` with new architecture documentation

---

## 8. Risk Checklist

- [ ] PostgreSQL transport may need additional configuration for production
- [ ] Saga events need saga-specific correlation (SagaId → CorrelationId)
- [ ] Handler constructor signatures change (add IPublishEndpoint) — already done
- [ ] Saga registration order (must be after consumers)
- [ ] Parallel run testing (verify no duplicate events during transition)
- [ ] DLQ consumer for failed events (manual inspection)
- [ ] EventRecord table cleanup — must be done carefully to avoid data loss
- [ ] docker-compose.yml update — ensure no other services depend on RabbitMQ
- [ ] MassTransit 8.3.6 is MIT licensed (9.x became commercial-only) — stay on 8.x

---

## 9. Files Modified

### Existing files modified:

| File | Changes |
|------|---------|
| `Adnd.Server.csproj` | Add `MassTransit.PostgreSQL`, remove `MassTransit.RabbitMQ` |
| `Program.cs` | Replace RabbitMQ config with PostgreSQL config, register saga |
| `EventBusWorker.cs` | Replace RabbitMQ transport with PostgreSQL, remove manual retry/cleanup |
| `IEventBus.cs` | Update comment (remove RabbitMQ reference) |
| `GameAgent.cs` | Remove direct RabbitMQ code, simplify to saga-aware wrapper |
| `GameAgentManager.cs` | Remove startup recovery (MassTransit handles it) |
| `AdminController.cs` | Add saga/DLQ/queue admin endpoints |
| `docker-compose.yml` | Remove RabbitMQ service, remove RabbitMQ env vars |
| `Consumers/*.cs` | Update to work with PostgreSQL transport |

### New files:

| File | Purpose |
|------|---------|
| `Services/GmAgentSaga.cs` | Saga state machine (replaces GameAgent as GM orchestrator) |
| `Services/SagaStatusService.cs` | Admin UI saga status queries |
| `Services/DlqService.cs` | Admin UI DLQ management |
| `Services/QueueStatusService.cs` | Admin UI queue monitoring |
| `Services/MessageHistoryService.cs` | Admin UI message history |

---

*Validated by stakeholder on 2026-06-14.*
