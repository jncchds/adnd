# Game State Stabilization Design

**Date:** 2026-06-11
**Priority:** P0 → P1 → P2
**Scope:** Backend game state flow, disconnect detection, event handler coordination, frontend state management

---

## Problem Statement

The game state system is difficult to work with and unreliable:

1. **Game lifecycle is a black box** — Game created → started → nothing visible. UI has no way to see what's happening or influence the narrative except a start button.
2. **Disconnect detection is triple-implemented and fragile** — `PlayerDisconnectDetector` uses reflection to access `GameHub._playerConnections`. Three separate implementations do slightly different things.
3. **Event handler timing is unclear** — Handlers are mostly no-ops (just logging). Real logic lives in `AgentBus` and `PlotWeaver`. MediatR dispatches synchronously, blocking the Hub on slow handlers.
4. **Background services vs event handlers lack coordination** — `GameAgent` auto-narrate duplicates what `GameActionHandler` does. Neither knows what the other is doing.
5. **Frontend state is broken** — Navigation leads to empty pages or errors. Actions are hidden in wrong places. No single source of truth for game state.

---

## Design Goals

- **Make game state visible** — Players and creator always know what's happening
- **Eliminate duplicate disconnect detection** — Single source of truth via SignalR
- **Fix event handler timing** — Async for slow operations, sync for critical ones
- **Stabilize frontend navigation** — Unified state hook, proper guards, graceful error recovery
- **Backwards-compatible** — No breaking changes to existing API contracts or DB schema

---

## Section 1: Game State Lifecycle

### Current State

```
Game.Status: Created → Active → Archived
Game.GMStatus: Idle → Running → Paused
```

No `Starting` state. No UI feedback during the gap between "started" and "GM is running."

### Proposed State Machine

```
Game.Status (lifecycle):
  Created → Starting → Active → Ending → Archived

Game.GMStatus (activity):
  Idle → Running → Paused
  (auto-narrate transitions to Running when triggered)
```

### UI State Indicators

| Game.Status | GMStatus | UI Display |
|-------------|----------|------------|
| Created | — | "Waiting for creator to start" |
| Starting | Running | "Opening narrative being generated..." |
| Starting | Idle | "Starting game..." |
| Active | Running | "GM is running" (green dot) |
| Active | Idle | "GM is idle — waiting for players" |
| Active | Paused | "GM paused" (orange dot) |
| Archived | — | "Game archived" (gray banner) |

### Concrete Changes

**Backend:**
1. Add `Starting` and `Ending` to `GameStatus` enum
2. `GameLifecycleHandler.Handle(GameStarted)`: set `Status = Starting`, then transition to `Active` when first narrative is broadcast
3. `BroadcastNarrationAsync` in `AgentBus`: emit a `GameNarrationStarted` event that the UI can subscribe to
4. `AgentBus.HandleGMCall`: when GM goes from Idle to Running via auto-narrate, broadcast `GMStatusChanged`

**Frontend:**
5. New `useGameGameState` hook (see Section 4)
6. Game room page shows current state indicator + last GM action + time since last action
7. Creator-only "Nudge GM" button in admin tab (visible when GM is idle)

### Data Flow: Game Start

```
Creator clicks "Start Game"
  → POST /admin/games/{id}/start
  → DB: Status=Active (transiently Starting), GMStatus=Running
  → MediatR: GameStarted
  → GameLifecycleHandler: GetOrCreate(agent) → StartAsync()
  → AgentBus.ActivateGameAgentAsync: queues initial GM call
  → GameAgent picks it up → LLM generates opening narrative
  → AgentBus.BroadcastNarrationAsync:
    - Saves Message(entity)
    - Emits GameNarrationStarted event
    - SignalR: "NewNarration" to game group
  → UI: receives narration → sets status to Active
```

### Creator Nudge Flow

```
Creator clicks "Nudge GM" (admin tab)
  → POST /admin/games/{id}/nudge
  → AgentBus.SendSwayAsync:
    - Validates creator role
    - Validates GMStatus != Paused
    - Queues AgentCall(ToAgent=GM, Action=Nudge)
  → GameAgent picks it up → LLM incorporates direction
  → AgentBus.BroadcastNarrationAsync → SignalR: "NewNarration"
  → UI: shows new narrative in chat
```

---

## Section 2: Disconnect Detection Cleanup

### Current State

Three implementations:
1. `GameHub.OnDisconnectedAsync` — marks player disconnected, broadcasts event
2. `GameHub.CheckDisconnectedPlayersAsync` — polls `_playerConnections` for stale players
3. `PlayerDisconnectDetector` — background service, uses **reflection** to access `GameHub._playerConnections`

### Proposed State

Single source of truth: SignalR's connection lifecycle.

```
OnConnectedAsync:
  → Register player in _playerConnections
  → Emit PlayerJoined event
  → Broadcast to game group

OnDisconnectedAsync:
  → Unregister from _playerConnections
  → Mark player as Disconnected in DB
  → Emit PlayerDisconnected event
  → Broadcast to game group

SendHeartbeat (reconnect):
  → If player was Disconnected, mark Active
  → Emit PlayerReconnected event
  → Broadcast to game group
```

### Concrete Changes

**Backend:**
1. **Remove `PlayerDisconnectDetector` class and registration** — delete the file, remove from `Program.cs`
2. **Consolidate disconnect logic in `GameHub`**:
   - `OnConnectedAsync`: add player to `_playerConnections`, emit `PlayerJoined`
   - `OnDisconnectedAsync`: remove from `_playerConnections`, mark disconnected, emit `PlayerDisconnected`
   - `SendHeartbeat`: handle reconnection, emit `PlayerReconnected`
3. **Remove `CheckDisconnectedPlayersAsync`** — no longer needed
4. **Update `PlayerHandler`** — add combat-aware logic: if disconnected during combat, mark as AFK and pause their turn

**Frontend:**
5. No changes needed — the SignalR events (`PlayerJoined`, `PlayerDisconnected`, `PlayerReconnected`) are already consumed by the existing hooks

### Why This Works

- SignalR's connection lifecycle is the ground truth
- No polling, no reflection, no race conditions
- WebSocket: near-instant disconnect detection
- SSE: may take a few seconds (acceptable for TTRPG use case)

---

## Section 3: Event Handler Coordination

### Current State

- MediatR dispatches synchronously by default
- `GameActionHandler` queues GM narrative calls (DB writes) — blocks the Hub
- `PlayerHandler`, `ChatHandler` are no-ops (just logging)
- `GameAgent` auto-narrate duplicates `GameActionHandler` logic
- No event ordering guarantees for same-game events

### Proposed State

**A. Handler responsibility matrix:**

| Handler | Responsibility | Action |
|---------|---------------|--------|
| `GameLifecycleHandler` | Start/pause/resume agent | ✅ Keep as-is |
| `PlayerHandler` | Player lifecycle | Add combat-aware disconnect logic |
| `GameActionHandler` | Queue GM narrative | Make async for DB writes |
| `ChatHandler` | Chat logging | ✅ Keep as-is (lightweight) |
| `SessionHandler` | Session lifecycle | ✅ Keep as-is |

**B. Async handler changes:**

```csharp
// In GameHub, change from:
await _mediator.Publish(event);

// To:
if (isSlowOperation)
{
    _ = Task.Run(() => _mediator.Publish(event));
}
else
{
    await _mediator.Publish(event);
}
```

- **Sync** (critical): `PlayerJoined`, `PlayerLeft`, `PlayerDisconnected`, `PlayerReconnected` — DB writes must be consistent
- **Async** (slow): `SkillCheckRequested`, `AttackRequested`, `CombatStarted`, etc. — queue GM calls, DB writes

**C. Event ordering:**

Add a per-game event queue to `GameHub`:

```csharp
private readonly ConcurrentDictionary<Guid, Channel<GameEvent>> _gameEventQueues = new();
```

Events for the same game are processed in order. Rapid combat actions within 500ms are batched into a single notification.

---

## Section 4: Frontend State Stabilization

### Current State

- Each component manages its own state independently
- No single source of truth for game state
- SignalR events aren't consistently consumed
- Navigation guards don't handle partial state
- API failures lead to white pages

### Proposed State

**A. `useGameGameState` hook — single source of truth:**

```typescript
interface GameGameState {
  // From DB (via API)
  gameStatus: 'Created' | 'Starting' | 'Active' | 'Ending' | 'Archived';
  gmStatus: 'Idle' | 'Running' | 'Paused';
  lastGMAction: { action: string; at: Date };
  
  // From SignalR (real-time)
  players: PlayerState[];
  combat: CombatState | null;
  
  // UI state
  isLoading: boolean;
  error: string | null;
  isReconnecting: boolean;
  
  // Actions
  refreshState: () => Promise<void>;
  reconnect: () => Promise<void>;
}
```

**B. Navigation guards:**

- When navigating to `/game/:id`, always fetch current state from API
- Show loading skeleton while fetching
- If game is `Created` but not started, show "Waiting for GM..." state
- If game is `Archived`, show archive banner + link to dashboard
- If API call fails, show retry button (not white page)

**C. Error recovery:**

- SignalR disconnect → reconnect banner (not crash)
- API failure → retry button with error message
- Navigation mid-operation → queue operation, replay on reconnect

**D. Action placement by role:**

- **Creator**: Settings tab → "Nudge GM" button (visible when GM idle)
- **Players**: Chat bar → dice roll, skill check, attack buttons (always visible in combat)
- **No hidden actions**: Every available action is visible in the chat bar or a clearly labeled section

---

## Implementation Order

| Priority | Area | Changes | Effort |
|----------|------|---------|--------|
| P0 | Game state visibility | Add `Starting`/`Ending` status, UI indicators, creator nudge | 2-3 hours |
| P0 | Frontend state | `useGameGameState` hook, navigation guards, error recovery | 2-3 hours |
| P1 | Disconnect detection | Remove `PlayerDisconnectDetector`, consolidate in `GameHub` | 1 hour |
| P1 | Event timing | Async handlers for slow operations, per-game event queue | 1-2 hours |
| P2 | Combat-aware disconnect | `PlayerHandler` adds AFK logic during combat | 30 min |
| P2 | Loading states | Skeletons, banners, retry buttons | 1 hour |

**Total: ~8-10 hours**

---

## Verification Criteria

- [ ] Game room shows clear state indicator (Starting/Active/Paused/Archived)
- [ ] Opening narrative transitions state from Starting → Active
- [ ] Creator can nudge GM from admin tab when GM is idle
- [ ] `PlayerDisconnectDetector` is removed, disconnects work via SignalR
- [ ] Navigation to `/game/:id` always shows correct state (no empty pages)
- [ ] SignalR disconnect shows reconnect banner (not crash)
- [ ] API failures show retry button (not white page)
- [ ] All existing API contracts unchanged
- [ ] Build succeeds, no regressions in existing functionality
