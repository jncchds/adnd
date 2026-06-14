# Plan: MediatR Event Publisher/Subscriber Audit Fixes

> Generated: 2026-06-14
> Scope: Fix all event publishing gaps, dead events, and semantic bugs found during publisher/subscriber audit
> Priority: High — impacts GM autonomous narrative quality and PlotWeaver reactivity

## Executive Summary

The event system has **3 real bugs** and **15 dead handlers** (handlers with no publishers). The most impactful issue is that **9 combat event types have handlers but are never published**, meaning the GM agent never receives narrative triggers for attacks, saves, spells, conditions, XP, level-ups, or rests. This severely limits the autonomous game experience.

## Architecture Reference

```
┌─────────────────────────────────────────────────────────────────┐
│                      Event Transport Layer                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  IEventBus (resolves to EventBusWorker)                        │
│  ┌──────────────┐     ┌──────────────────┐     ┌────────────┐ │
│  │  Publishers   │────▶│  Persist + Queue │────▶│ Handlers   │ │
│  │  (Controllers,│     │  (EventRecord    │     │ (MediatR   │ │
│  │   Hub methods)│     │   DB table)      │     │  handlers) │ │
│  └──────────────┘     └──────────────────┘     └────────────┘ │
│                                                                 │
│  RabbitMqEventBus (concrete type, injected where needed)       │
│  ┌──────────────┐     ┌──────────────────┐     ┌────────────┐ │
│  │  Publishers   │────▶│  Agent Queue     │────▶│ Saga       │ │
│  │  (Handlers)   │     │  agent.{gameId}  │────▶│ Handlers   │ │
│  └──────────────┘     └──────────────────┘     └────────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

Two parallel transport layers:
1. **IEventBus → EventBusWorker**: Game-scoped events (UI, plot weaving, GM narrative). Published via `PublishAsync<T>()`, persisted to `EventRecord` DB table, dispatched to handlers via RabbitMQ routing key `game.{gameId}`.
2. **RabbitMqEventBus (concrete type)**: Agent/saga events. Published directly to RabbitMQ routing key `agent.{gameId}`, consumed by `GameAgent` per-game.

---

## Fix 1: Add Combat Event Publishers (Highest Impact)

### Problem
9 combat event types have handlers in `GameActionHandler` but are never published. The `GameHub.Combat` methods call `_combatService` directly and broadcast via SignalR, but never publish the corresponding events. This means the GM agent never gets narrative flavor for these actions.

### Events to Add Publishers For

| Event | Hub Method | Current Behavior | Fix |
|-------|-----------|-----------------|-----|
| `CombatAttackExecuted` | `CombatAttack` | SignalR only | Add `PublishAsync` |
| `CombatSaveThrowExecuted` | `CombatSaveThrow` | SignalR only | Add `PublishAsync` |
| `CombatSpellCast` | `CastSpell`/`CastSpellAtRange` | SignalR only | Add `PublishAsync` |
| `CombatConditionApplied` | `CombatApplyCondition` | SignalR only | Add `PublishAsync` |
| `CombatConditionRemoved` | `CombatRemoveCondition` | SignalR only | Add `PublishAsync` |
| `CombatDamageDealt` | `CombatDealDamage` | Published but no handler | ✅ Already published |
| `CombatHealed` | `CombatHeal` | Published but no handler | ✅ Already published |
| `CombatXPGranted` | (service method) | Never published | Add publisher |
| `CombatLevelUp` | (service method) | Never published | Add publisher |
| `CombatRestStarted` | (service method) | Never published | Add publisher |
| `CombatRestEnded` | (service method) | Never published | Add publisher |

### Implementation

#### Files to modify:
1. `src/Adnd.Server/Hubs/GameHub.Combat.cs`
2. `src/Adnd.Server/Hubs/GameHub.Spells.cs`

#### Code changes:

**GameHub.Combat.cs** — Add event publishing after each combat service call:

```csharp
// In CombatAttack method, after PersistGameEventAsync and before SignalR broadcast:
PublishAsync(new CombatAttackExecuted(
    combat.GameId,
    combat.Id,
    result.Attacker,
    result.Weapon,
    result.Target,
    result.AttackDice ?? "",
    result.DamageDice));

// In CombatSaveThrow method:
PublishAsync(new CombatSaveThrowExecuted(
    combat.GameId,
    combat.Id,
    result.Participant,
    participantId,
    result.SaveType,
    result.DiceRoll > 0 ? $"d20({result.DiceRoll})" : "d20",
    result.DC));

// In CombatApplyCondition method:
PublishAsync(new CombatConditionApplied(
    combat.GameId,
    combat.Id,
    participantId,
    conditionName,
    duration));

// In CombatRemoveCondition method:
PublishAsync(new CombatConditionRemoved(
    combat.GameId,
    combat.Id,
    participantId,
    conditionName));
```

**GameHub.Spells.cs** — Add event publishing after spell service call:

```csharp
// In CastSpell / CastSpellAtRange methods:
PublishAsync(new CombatSpellCast(
    combat.GameId,
    combat.Id,
    caster,
    spellName,
    targetId,
    saveDC,
    damageFormula));
```

**CombatService.cs** — Add event publishing in service methods that grant XP, level up, or manage rests:

```csharp
// In ExecuteAttackAsync (after XP grant):
// → This is handled by GameHub which should publish CombatXPGranted

// In MakeLevelUpAsync:
// → This is handled by GameHub which should publish CombatLevelUp

// In StartLongRest / StartShortRest:
// → This is handled by GameHub which should publish CombatRestStarted

// In EndRest:
// → This is handled by GameHub which should publish CombatRestEnded
```

#### Verification:
- Build succeeds (`dotnet build`)
- All 9 events are now published with correct `GameId`, `CombatId`, and parameter values
- `GameActionHandler` receives these events and queues GM narrative (via debounce logic)
- Existing SignalR broadcasts unchanged (no breaking change)

---

## Fix 2: Add Controller Event Publishers (PlotWeaver Reactivity)

### Problem
6 event types have handlers in `PlotWeaverHandler` but are never published by Controllers. This means plot threads never get momentum bumps from NPC/character/plot thread changes, breaking the autonomous plot adaptation.

### Events to Add Publishers For

| Event | Controller | Fix |
|-------|-----------|-----|
| `NPCCreated` | `AdminController.CreateNPC` | Add `PublishAsync` |
| `NPCUpdated` | `AdminController.UpdateNPC` | Add `PublishAsync` |
| `NPCDeleted` | `AdminController.DeleteNPC` | Add `PublishAsync` |
| `CharacterUpdated` | `Characters.cs` (all mutations) | Add `PublishAsync` |
| `PlotThreadCreated` | `AdminController.CreatePlotThread` | Add `PublishAsync` |
| `PlotThreadUpdated` | `AdminController.UpdatePlotThread` | Add `PublishAsync` |

### Implementation

#### Files to modify:
1. `src/Adnd.Server/Controllers/NPCs.cs`
2. `src/Adnd.Server/Controllers/Plots.cs`
3. `src/Adnd.Server/Controllers/Characters.cs`

#### Code changes:

**NPCs.cs** — After each DB save:
```csharp
// In CreateNPC, after SaveChangesAsync:
await _eventBus.PublishAsync(new NPCCreated(gameId, npc.Id, npc.Name));

// In UpdateNPC, after SaveChangesAsync:
await _eventBus.PublishAsync(new NPCUpdated(gameId, npc.Id));

// In DeleteNPC, after SaveChangesAsync:
await _eventBus.PublishAsync(new NPCDeleted(gameId, npc.Id));
```

**Plots.cs** — After each DB save:
```csharp
// In CreatePlotThread, after SaveChangesAsync:
await _eventBus.PublishAsync(new PlotThreadCreated(gameId, thread.Id, thread.Title));

// In UpdatePlotThread, after SaveChangesAsync:
await _eventBus.PublishAsync(new PlotThreadUpdated(gameId, thread.Id));

// In AddKeyEvent, after SaveChangesAsync:
await _eventBus.PublishAsync(new PlotThreadUpdated(gameId, thread.Id));
```

**Characters.cs** — After each mutation:
```csharp
// After any Character save:
await _eventBus.PublishAsync(new CharacterUpdated(gameId, character.Id));
```

#### Verification:
- Build succeeds
- `PlotWeaverHandler` receives these events and triggers momentum updates
- Plot thread adaptation works when NPCs/characters/threads are modified via admin API

---

## Fix 3: Add Hub Event Publishers (Player/Session/Chat)

### Problem
5 event types have handlers but are never published from the Hub.

### Events to Add Publishers For

| Event | Hub Method | Fix |
|-------|-----------|-----|
| `PlayerLeft` | `LeaveGame` | Add `PublishAsync` (SignalR already broadcast) |
| `PlayerRoleChanged` | `PromotePlayer` | Add `PublishAsync` |
| `SessionCreated` | `CreateSession` | Add `PublishAsync` |
| `SessionClosed` | `CloseSession` | Add `PublishAsync` |
| `OOCWhisperReceived` | (never published) | Add publisher in `GameHub.Whispers.cs` |

### Implementation

#### Files to modify:
1. `src/Adnd.Server/Hubs/GameHub.JoinLeave.cs`
2. `src/Adnd.Server/Controllers/GamesController.cs`
3. `src/Adnd.Server/Hubs/GameHub.Whispers.cs`

#### Code changes:

**GameHub.JoinLeave.cs** — In `LeaveGame`:
```csharp
// After SignalR broadcast, before returning:
await _eventBus.PublishAsync(new PlayerLeft(gameId, player.Id));
```

**GamesController.cs** — In `PromotePlayer`:
```csharp
// After role change and SaveChangesAsync:
await _eventBus.PublishAsync(new PlayerRoleChanged(gameId, playerId, newRole.ToString()));
```

**GamesController.cs** — In `CreateSession`:
```csharp
// After session creation and SaveChangesAsync:
await _eventBus.PublishAsync(new SessionCreated(gameId, session.Id, request.Title));
```

**GamesController.cs** — In `CloseSession`:
```csharp
// After session closure:
await _eventBus.PublishAsync(new SessionClosed(gameId, sessionId));
```

**GameHub.Whispers.cs** — Add the missing publisher. Currently the file body is empty:
```csharp
// Need to find where OOC whispers are received and publish the event.
// Check if there's a hub method for receiving OOC whispers.
```

#### Verification:
- Build succeeds
- `PlayerHandler` receives `PlayerLeft` (was previously only SignalR)
- `PlotWeaverHandler` receives `PlayerRoleChanged` for plot adaptation
- `SessionHandler` receives session lifecycle events
- `ChatHandler` receives `OOCWhisperReceived` for logging

---

## Fix 4: Fix AgentCallFailed Semantic Bug (Critical)

### Problem
`ToolExecutionHandler.HandleAsync` publishes `AgentCallFailed` when `RequiresUserInput` is true. This is **not a failure** — it's a "waiting for user confirmation" state. But `AgentCallFailedHandler` permanently marks the call as `Failed` with `SagaStep.Failed` and abandons all coordinators.

### Impact
The saga state machine treats "waiting for confirmation" as terminal failure. The call's `CurrentStep` is set to `Failed`, `Status` to `Failed`, and all coordinators are abandoned. This breaks the tool call flow — the GM can still confirm via SignalR, but the saga state is corrupted.

### Fix: Create `ToolCallWaitingConfirmation` Event

#### New event (add to `GameEvents.cs`):
```csharp
public record ToolCallWaitingConfirmation(
    Guid SagaId,
    Guid GameId,
    string ToolName,
    string ToolCallId) : IGameEvent;
```

#### New handler (new file `Handlers/ToolCallWaitingConfirmationHandler.cs`):
```csharp
public class ToolCallWaitingConfirmationHandler :
    IEventHandler<ToolCallWaitingConfirmation>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ToolCallWaitingConfirmationHandler> _logger;

    public ToolCallWaitingConfirmationHandler(
        IServiceProvider serviceProvider,
        ILogger<ToolCallWaitingConfirmationHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(ToolCallWaitingConfirmation evt, CancellationToken ct = default)
    {
        _logger.LogInformation("[TOOL] WaitingConfirmation | SagaId={SagaId} | Tool={Tool} | CallId={CallId}",
            evt.SagaId, evt.ToolName, evt.ToolCallId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null) return;

        call.CurrentStep = SagaStep.ToolCallRequested;
        // Do NOT mark as Failed — keep coordinator intact
        await context.SaveChangesAsync(ct);
    }
}
```

#### Modify `ToolExecutionHandler.cs`:
```csharp
// Replace the AgentCallFailed publish with:
var waitingEvent = new ToolCallWaitingConfirmation(
    evt.SagaId,
    evt.GameId,
    evt.ToolName,
    toolCallRecord.Id.ToString());
var waitingPayload = JsonSerializer.Serialize(waitingEvent);
var waitingHeaders = new Dictionary<string, object> { ["x-event-type"] = waitingEvent.GetType().FullName! };
_rabbitMq.PublishToAgent(evt.GameId, waitingPayload, Guid.NewGuid().ToString(), waitingHeaders);
```

#### Verification:
- Build succeeds
- `AgentCallFailedHandler` only runs on real failures (LLM errors, tool execution errors)
- "Waiting confirmation" state preserves coordinator and saga state
- GM confirmation via SignalR works with correct saga state

---

## Fix 5: Remove Dead `InitialThreadsGenerated` Event

### Problem
`InitialThreadsGenerated` is published in 5 places but has **no handler**. It's dead code that wastes resources publishing to RabbitMQ queues that nobody consumes.

### Impact
No functional impact (it's dead code). But it wastes RabbitMQ resources and adds confusion.

### Fix: Remove the event

#### Steps:
1. Remove `InitialThreadsGenerated` record from `GameEvents.cs`
2. Remove all `PublishAsync(new InitialThreadsGenerated(...))` calls from:
   - `AgentBus.cs` (4 locations: `HandleGenerateInitialThreads`, `GenerateFallbackPlotThreads`, retry path, `GenerateFallbackPlotThreadsFromSeed`)
   - `LLMResponseHandler.cs` (1 location)
3. Remove any references in `GameHub.AICombat.cs` if present

#### Verification:
- Build succeeds
- No compilation errors
- Initial thread generation still works via `GameStarted` → `PlotWeaverHandler` → `GenerateInitialThreads` AgentCall

---

## Implementation Order

| Step | Fix | Effort | Risk |
|------|-----|--------|------|
| 1 | Fix 5: Remove dead `InitialThreadsGenerated` | 15 min | None (dead code removal) |
| 2 | Fix 4: `ToolCallWaitingConfirmation` event | 45 min | Low (new event, no breaking change) |
| 3 | Fix 1: Combat event publishers | 30 min | Low (additive only) |
| 4 | Fix 2: Controller event publishers | 30 min | Low (additive only) |
| 5 | Fix 3: Hub event publishers | 30 min | Low (additive only) |
| 6 | Build + test | 15 min | None |

**Total estimated effort: ~3 hours**

---

## Backwards Compatibility

All fixes are **additive and backwards-compatible**:
- New event publishers don't change existing API responses
- New `ToolCallWaitingConfirmation` event doesn't break existing clients
- Removing `InitialThreadsGenerated` removes dead code (no live consumers)
- No existing handlers are modified (only new handler added for Fix 4)
- No database migrations needed

---

## Verification Checklist

- [ ] `dotnet build` succeeds with no warnings
- [ ] All 9 combat events published from Hub methods
- [ ] All 6 controller events published from CRUD endpoints
- [ ] All 5 Hub events published from join/leave/session methods
- [ ] `ToolCallWaitingConfirmation` handler registered (auto-discovered by `HandlerRegistry`)
- [ ] `AgentCallFailed` no longer published for "waiting confirmation" cases
- [ ] `InitialThreadsGenerated` event record and all publish calls removed
- [ ] No compilation errors
- [ ] No runtime errors in logs during test game creation
- [ ] GM narrative fires for combat actions (verified via log)
- [ ] PlotWeaver reacts to NPC/character/plot changes (verified via log)
