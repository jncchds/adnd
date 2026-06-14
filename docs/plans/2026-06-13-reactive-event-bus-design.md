# Reactive Event Bus Design

> **Goal:** Eliminate all polling loops, make the system fully event-driven with bounded retry on RabbitMQ publish. Accept rare event loss (events lost only if RabbitMQ is down AND process crashes before restart).

> **Status:** ✅ Completed — implemented (commit `702f4d6`). EventBusWorker replaced with RabbitMQ consumer, bounded retry added, admin endpoints for pending events.

## Design Decisions

### Loss Model
- If RabbitMQ is down for >1.5s during runtime, events stay in DB but are lost from the queue
- If the process crashes, in-flight events in the EventRecord table are lost
- Pending events are recovered on next app restart (startup replay)
- Admin can manually push pending events via UI button

### Bounded Retry on Publish
- On publish: try 3 times with 500ms delay between attempts
- If all 3 fail → event stays in `EventRecord` with status `Pending`, logged at warning
- No exception thrown — the event is persisted, just not delivered to RabbitMQ
- Next app restart will replay pending events

### Lazy Cleanup
- EventRecord cleanup → after each successful publish (delete acknowledged events >7 days old)
- Expired tool call cleanup → before each tool call execution
- Old plot thread archive → during PlotWeaver reviews

## Architecture

```
Startup:
  EventBusWorker → replay Pending EventRecords → push to RabbitMQ
  GameAgentManager → recover active game agents (unchanged)

Runtime:
  SignalR Hub ──publish──▶ EventBusWorker.PublishAsync()
                              │
                              ├─▶ Persist EventRecord (DB, fire-and-forget)
                              └─▶ Publish to RabbitMQ with bounded retry (3 × 500ms)
                                    │
                                    └─▶ RabbitMQ consumer dispatches to handlers
                                          │
                                          ├─ GameLifecycleHandler
                                          ├─ GameActionHandler
                                          ├─ PlotWeaverHandler
                                          └─ ChatHandler

AgentCall completion ──▶ publishes new events (narration, plot updates, etc.)
                              │
                              └─▶ cascading reactive chain

Admin UI:
  "Push Pending Events" button → calls API → EventBusWorker pushes all game's pending events
```

## Task List

### Phase 1: Backend — EventBusWorker
1. Remove `ExecuteAsync` polling loop from EventBusWorker
2. Add `PushPendingEventsAsync(Guid gameId)` method
3. Add startup replay of pending events
4. Add lazy cleanup of old EventRecords after publish
5. Remove `EventRecordCleanupService` from DI registration

### Phase 2: Backend — GameAgent
6. Remove 10s timeout from `_signal.Wait()` in GameAgent
7. Remove crash recovery DB query fallback
8. Verify event-driven flow works without polling

### Phase 3: Backend — Remove Services
9. Remove `MaintenanceService` from DI registration
10. Add lazy cleanup of expired tool calls in AgentBus
11. Add lazy archive of old plot threads in PlotWeaver

### Phase 4: Backend — Missing Subscriptions
12. Add `PlayerDisconnected` and `PlayerReconnected` events to GameEvents.cs
13. Add `PlayerDisconnectHandler` class
14. Update `GameHub.OnDisconnectedAsync` to publish `PlayerDisconnected`
15. Update `GameHub.OnConnectedAsync` to detect reconnection and publish `PlayerReconnected`
16. Add `playerDisconnected` and `playerReconnected` message types to frontend

### Phase 5: Admin UI
17. Add `POST /admin/{gameId}/push-pending-events` endpoint
18. Add `GET /admin/{gameId}/pending-events-count` endpoint
19. Add "Pending Events" display + "Push Pending Events" button to admin page
20. Add frontend hook for pending events count

### Phase 6: Testing
21. Build and verify no compilation errors
22. Verify all event handlers are registered
23. Verify RabbitMQ consumer works
24. Verify admin button works
