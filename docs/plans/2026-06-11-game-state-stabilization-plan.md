# Game State Stabilization Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Stabilize the game state system by making state visible, eliminating duplicate disconnect detection, fixing event handler timing, and unifying frontend state.

**Architecture:** Add a `Starting` lifecycle state with UI indicators, consolidate disconnect detection into `GameHub` (remove `PlayerDisconnectDetector`), make slow event handlers async, and create a `useGameGameState` hook for frontend state unification.

**Tech Stack:** ASP.NET Core 10, MediatR, SignalR, React 19, TypeScript, MUI, EF Core, PostgreSQL

---

## Task 1: Add `Starting` and `Ending` to `GameStatus` enum

**Files:**
- Modify: `src/Adnd.Server/Models/Game.cs` (enum definition)

**Step 1: Read the current enum**

```bash
grep -n "enum GameStatus" src/Adnd.Server/Models/Game.cs -A 10
```

**Step 2: Add `Starting` and `Ending` values**

Add between `Created` and `Active`:
```csharp
Starting,
```

Add between `Active` and `Archived`:
```csharp
Ending,
```

Result should be:
```csharp
public enum GameStatus
{
    Created,
    Starting,
    Active,
    Ending,
    Archived
}
```

**Step 3: Verify the change compiles**

```bash
cd src/Adnd.Server && dotnet build --no-restore 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Warning(s), 0 Error(s)`

**Step 4: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Server/Models/Game.cs
git commit -m "feat: add Starting and Ending states to GameStatus enum"
```

---

## Task 2: Update `GameLifecycleHandler` to use `Starting` state

**Files:**
- Modify: `src/Adnd.Server/Handlers/GameEventHandlers.cs`

**Step 1: Read current `Handle(GameStarted)` method**

```bash
grep -n "Handle(GameStarted" src/Adnd.Server/Handlers/GameEventHandlers.cs -A 20
```

**Step 2: Update `Handle(GameStarted)` to set `Status = Starting`**

In `GameLifecycleHandler.Handle(GameStarted)`, after setting `GMStatus = Running`:
```csharp
game.Status = GameStatus.Starting;
```

Then change the transition in the log message from `Created→Active` to `Created→Starting`.

**Step 3: Add a new handler for `GameNarrationStarted` event**

First, add the event to `Events/GameEvents.cs`:
```csharp
public record GameNarrationStarted(Guid GameId, Guid MessageId) : INotification;
```

Then add handler to `GameLifecycleHandler`:
```csharp
public async Task Handle(GameNarrationStarted notification, CancellationToken ct)
{
    _logger.LogInformation("[STATE] GameNarrationStarted | GameId={GameId} | MessageId={MessageId} | Transition: Starting→Active",
        notification.GameId, notification.MessageId);

    using var scope = _serviceScopeFactory.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var game = await context.Games.FindAsync(notification.GameId);
    if (game != null && game.Status == GameStatus.Starting)
    {
        game.Status = GameStatus.Active;
        await context.SaveChangesAsync(ct);
        _logger.LogInformation("[STATE] GameActive | GameId={GameId} | Transition: Starting→Active", notification.GameId);
    }
}
```

**Step 4: Add `_serviceScopeFactory` field to `GameLifecycleHandler`**

Add to the class:
```csharp
private readonly IServiceScopeFactory _serviceScopeFactory;
```

And inject it in the constructor.

**Step 5: Verify compiles**

```bash
cd src/Adnd.Server && dotnet build --no-restore 2>&1 | tail -5
```

**Step 6: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Server/Handlers/GameEventHandlers.cs src/Adnd.Server/Events/GameEvents.cs
git commit -m "feat: use Starting state for game lifecycle, add GameNarrationStarted event"
```

---

## Task 3: Emit `GameNarrationStarted` from `BroadcastNarrationAsync`

**Files:**
- Modify: `src/Adnd.Server/Services/AgentBus.cs`

**Step 1: Read `BroadcastNarrationAsync` method**

```bash
grep -n "BroadcastNarrationAsync" src/Adnd.Server/Services/AgentBus.cs -A 30
```

**Step 2: Add `IMediator` parameter to `AgentBus` constructor**

Add to constructor parameters:
```csharp
private readonly IMediator _mediator;
```

And add to the constructor body:
```csharp
_mediator = mediator;
```

**Step 3: In `BroadcastNarrationAsync`, after saving the message and before broadcasting via SignalR:**

```csharp
// Emit event so GameLifecycleHandler can transition Starting → Active
await _mediator.Publish(new GameNarrationStarted(gameId, message.Id));
```

**Step 4: Verify compiles**

```bash
cd src/Adnd.Server && dotnet build --no-restore 2>&1 | tail -5
```

**Step 5: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Server/Services/AgentBus.cs
git commit -m "feat: emit GameNarrationStarted event to transition Starting→Active"
```

---

## Task 4: Add GM status change broadcast

**Files:**
- Modify: `src/Adnd.Server/Hubs/GameHub.cs`

**Step 1: Add a new event to `Events/GameEvents.cs`:**

```csharp
public record GMStatusChanged(Guid GameId, GMStatus NewStatus, string? LastAction) : INotification;
```

**Step 2: In `GameHub`, add a method to broadcast GM status changes:**

```csharp
public async Task BroadcastGMStatusAsync(Guid gameId, GMStatus status, string? lastAction)
{
    await _mediator.Publish(new GMStatusChanged(gameId, status, lastAction));
    await Clients.Group(gameId.ToString()).SendAsync("GMStatusChanged", new
    {
        GameId = gameId,
        Status = status,
        LastAction = lastAction,
        ChangedAt = DateTime.UtcNow
    });
}
```

**Step 3: In `AgentBus.HandleGMCall`, after updating `GMStatus` in the DB, call `BroadcastGMStatusAsync`:**

In the `ManageState` branch:
```csharp
await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
{
    GameId = game.Id,
    Status = game.GMStatus,
    LastAction = game.LastGMAction,
    ChangedAt = game.LastGMActionAt
});
```

In the `Narrate/Generate` branch (both normal and tool-call paths):
```csharp
await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GMStatusChanged", new
{
    GameId = game.Id,
    Status = game.GMStatus,
    LastAction = game.LastGMAction,
    ChangedAt = game.LastGMActionAt
});
```

**Step 4: Verify compiles**

```bash
cd src/Adnd.Server && dotnet build --no-restore 2>&1 | tail -5
```

**Step 5: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Server/Hubs/GameHub.cs src/Adnd.Server/Events/GameEvents.cs
git commit -m "feat: add GMStatusChanged event and broadcast method to GameHub"
```

---

## Task 5: Remove `PlayerDisconnectDetector` and consolidate into `GameHub`

**Files:**
- Delete: `src/Adnd.Server/Services/PlayerDisconnectDetector.cs`
- Modify: `src/Adnd.Server/Hubs/GameHub.cs`
- Modify: `src/Adnd.Server/Program.cs` (remove registration)
- Modify: `src/Adnd.Server/Handlers/GameEventHandlers.cs` (update PlayerHandler)

**Step 1: Delete the detector**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
rm src/Adnd.Server/Services/PlayerDisconnectDetector.cs
```

**Step 2: Update `GameHub.OnConnectedAsync` to register player:**

```csharp
public override async Task OnConnectedAsync()
{
    _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

    var userId = Context.UserIdentifier;
    if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
    {
        // Register player connection
        _playerConnections.AddOrUpdate(uid.ToString(), Context.ConnectionId, (k, oldValue) => Context.ConnectionId);

        // Find the player and join their game group
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, player.GameId.ToString());
            await _mediator.Publish(new PlayerJoined(player.GameId, player.Id, player.UserId, player.CharacterName ?? "Unknown"));
        }
    }

    await base.OnConnectedAsync();
}
```

**Step 3: Update `GameHub.OnDisconnectedAsync` to be the single source of truth:**

```csharp
public override async Task OnDisconnectedAsync(Exception? exception)
{
    _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

    // Find the player connected via this connection
    var player = await _context.Players
        .FirstOrDefaultAsync(p => p.Status == PlayerStatus.Active &&
            _playerConnections.GetValueOrDefault(p.Id.ToString()) == Context.ConnectionId);

    if (player != null)
    {
        // Remove from connections
        _playerConnections.TryRemove(player.Id.ToString(), out _);

        // Mark as disconnected
        player.Status = PlayerStatus.Disconnected;
        player.LeftAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Leave game group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, player.GameId.ToString());

        // Broadcast disconnection
        await _mediator.Publish(new PlayerDisconnected(player.GameId, player.Id, player.UserId, player.CharacterName ?? "Unknown", player.LeftAt));
        await Clients.Group(player.GameId.ToString()).SendAsync("PlayerDisconnected", new
        {
            PlayerId = player.Id,
            UserId = player.UserId,
            CharacterName = player.CharacterName,
            GameId = player.GameId,
            Message = $"{player.CharacterName} has been disconnected",
            DisconnectedAt = player.LeftAt
        });

        _logger.LogInformation("Player {CharacterName} ({UserId}) disconnected from game {GameId}",
            player.CharacterName, player.UserId, player.GameId);
    }

    await base.OnDisconnectedAsync(exception);
}
```

**Step 4: Remove `CheckDisconnectedPlayersAsync` from `GameHub.cs`**

Delete the entire method (it's no longer needed).

**Step 5: Remove `PlayerDisconnectDetector` registration from `Program.cs`:**

```bash
grep -n "PlayerDisconnectDetector" src/Adnd.Server/Program.cs
```

Remove the line(s) that register it (likely `builder.Services.AddHostedService<PlayerDisconnectDetector>()`).

**Step 6: Update `PlayerHandler` to add combat-aware disconnect logic:**

In `PlayerHandler.Handle(PlayerDisconnected)`:
```csharp
public async Task Handle(PlayerDisconnected notification, CancellationToken ct)
{
    _logger.LogInformation("[PLAYER] Disconnected | GameId={GameId} | PlayerId={PlayerId} | Character={Character} | UserId={UserId} | DisconnectedAt={DisconnectedAt}",
        notification.GameId, notification.PlayerId, notification.CharacterName, notification.UserId, notification.DisconnectedAt);

    // If during combat, mark as AFK
    try
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var combat = await context.Combats
            .FirstOrDefaultAsync(c => c.GameId == notification.GameId && c.Status == CombatStatus.Active, ct);

        if (combat != null)
        {
            var participant = await context.CombatParticipants
                .FirstOrDefaultAsync(p => p.CombatId == combat.Id && p.PlayerId == notification.PlayerId, ct);

            if (participant != null)
            {
                participant.IsAFK = true;
                participant.AFKAt = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("[PLAYER] AFK | GameId={GameId} | PlayerId={PlayerId} | CombatId={CombatId}",
                    notification.GameId, notification.PlayerId, combat.Id);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "[PLAYER] Failed to check combat state for disconnected player {PlayerId}", notification.PlayerId);
    }
}
```

Add `_scopeFactory` field and constructor parameter to `PlayerHandler`.

**Step 7: Verify compiles**

```bash
cd src/Adnd.Server && dotnet build --no-restore 2>&1 | tail -5
```

**Step 8: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git rm src/Adnd.Server/Services/PlayerDisconnectDetector.cs
git add src/Adnd.Server/Hubs/GameHub.cs src/Adnd.Server/Program.cs src/Adnd.Server/Handlers/GameEventHandlers.cs src/Adnd.Server/Events/GameEvents.cs
git commit -m "refactor: remove PlayerDisconnectDetector, consolidate disconnect into GameHub, add combat-aware disconnect"
```

---

## Task 6: Make slow event handlers async

**Files:**
- Modify: `src/Adnd.Server/Handlers/GameEventHandlers.cs`

**Step 1: Add a helper method to `GameActionHandler`:**

```csharp
/// <summary>
/// Publishes a notification asynchronously to avoid blocking the SignalR Hub.
/// Used for slow operations like GM narrative queueing.
/// </summary>
private async Task PublishAsync<T>(T notification, CancellationToken ct) where T : INotification
{
    _ = Task.Run(async () => await _mediator.Publish(notification, ct));
}
```

Wait — `GameActionHandler` doesn't have `IMediator`. Let me check the constructor. Actually, handlers already receive `CancellationToken` via MediatR. The better approach is to change the handler interface.

**Better approach: Change handlers to use MediatR's `IAsyncNotificationHandler` pattern.**

Actually, the simplest fix is to make the Hub publish asynchronously:

**Step 1: In `GameHub.cs`, find all `await _mediator.Publish()` calls and change to async:**

```csharp
// Find these patterns in GameHub.cs:
await _mediator.Publish(new SomeEvent(...));

// Change to:
_ = Task.Run(async ct => await _mediator.Publish(new SomeEvent(...), ct);
```

But this is messy. A cleaner approach:

**Step 2: Add a `IMediator` field to `GameHub` constructor (it already has one via the constructor).**

**Step 3: Create a helper method in `GameHub`:**

```csharp
/// <summary>
/// Publish a notification asynchronously to avoid blocking the Hub caller.
/// Critical events (player join/leave) should still use await.
/// Slow events (GM narrative queue) should use this.
/// </summary>
private void PublishAsync<T>(T notification, CancellationToken ct = default) where T : INotification
{
    _ = Task.Run(async () =>
    {
        try
        {
            await _mediator.Publish(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing async event {EventType}", typeof(T).Name);
        }
    });
}
```

**Step 4: In `GameHub` hub methods, change slow event publishes:**

Find all places where `SkillCheckRequested`, `AttackRequested`, `CombatStarted`, `CombatEnded`, and other combat/action events are published. Change:
```csharp
await _mediator.Publish(new CombatStarted(...));
```
to:
```csharp
PublishAsync(new CombatStarted(...));
```

**Keep synchronous** (with `await`):
- `PlayerJoined`
- `PlayerLeft`
- `PlayerDisconnected`
- `PlayerReconnected`
- `SessionCreated`
- `SessionClosed`

**Step 5: Verify compiles**

```bash
cd src/Adnd.Server && dotnet build --no-restore 2>&1 | tail -5
```

**Step 6: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Server/Hubs/GameHub.cs
git commit -m "perf: publish slow game action events asynchronously to unblock SignalR Hub"
```

---

## Task 7: Create `useGameGameState` hook

**Files:**
- Create: `src/Adnd.Client/src/api/gameStateHook.ts`
- Modify: `src/Adnd.Client/src/api/hubHook.ts` (add GM status subscription)

**Step 1: Read existing hooks for patterns**

```bash
head -50 src/Adnd.Client/src/api/gameHooks.ts
head -50 src/Adnd.Client/src/api/hubHook.ts
```

**Step 2: Create the hook**

```typescript
import { useState, useEffect, useCallback, useRef } from 'react';
import { APIClient } from './client';
import { useGameHub } from './hubHook';
import { GameStatus, GMStatus } from './types';

export interface PlayerState {
  id: string;
  userId: string;
  characterName: string;
  status: 'Active' | 'Disconnected';
  leftAt?: string;
}

export interface GameGameState {
  gameStatus: GameStatus;
  gmStatus: GMStatus;
  players: PlayerState[];
  lastGMAction: { action: string; at: string } | null;
  isLoading: boolean;
  error: string | null;
  isReconnecting: boolean;
  refreshState: () => Promise<void>;
  reconnect: () => Promise<void>;
}

export function useGameGameState(gameId: string | undefined): GameGameState {
  const [gameStatus, setGameStatus] = useState<GameStatus>(GameStatus.Created);
  const [gmStatus, setGmStatus] = useState<GMStatus>(GMStatus.Idle);
  const [players, setPlayers] = useState<PlayerState[]>([]);
  const [lastGMAction, setLastGMAction] = useState<{ action: string; at: string } | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isReconnecting, setIsReconnecting] = useState(false);

  const hub = useGameHub(gameId);
  const prevGMStatusRef = useRef<GMStatus>(GMStatus.Idle);

  // Fetch game state from API
  const refreshState = useCallback(async () => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    try {
      const game = await APIClient.getGame(gameId);
      setGameStatus(game.status);
      setGmStatus(game.gmStatus);
      setLastGMAction(game.lastGMAction ? {
        action: game.lastGMAction,
        at: game.lastGMActionAt || ''
      } : null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load game state');
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  // Initial fetch
  useEffect(() => {
    refreshState();
  }, [refreshState]);

  // Subscribe to SignalR events
  useEffect(() => {
    if (!hub) return;

    hub.on('GMStatusChanged', (data: { gameId: string; status: string; lastAction: string; changedAt: string }) => {
      setGmStatus(data.status as GMStatus);
      setLastGMAction({ action: data.lastAction, at: data.changedAt });

      // Track GM going from Idle to Running (auto-narrate triggered)
      if (prevGMStatusRef.current === GMStatus.Idle && data.status === GMStatus.Running) {
        // Refresh state to pick up any status changes
        refreshState();
      }
      prevGMStatusRef.current = data.status;
    });

    hub.on('PlayerJoined', (data: { playerId: string; userId: string; characterName: string; gameId: string }) => {
      setPlayers(prev => {
        const exists = prev.find(p => p.id === data.playerId);
        if (exists) return prev;
        return [...prev, { ...data, status: 'Active' } as PlayerState];
      });
    });

    hub.on('PlayerLeft', (data: { playerId: string; gameId: string }) => {
      setPlayers(prev => prev.filter(p => p.id !== data.playerId));
    });

    hub.on('PlayerDisconnected', (data: { playerId: string; userId: string; characterName: string; gameId: string; disconnectedAt: string }) => {
      setPlayers(prev => prev.map(p =>
        p.id === data.playerId ? { ...p, status: 'Disconnected', leftAt: data.disconnectedAt } : p
      ));
    });

    hub.on('PlayerReconnected', (data: { playerId: string; userId: string; characterName: string }) => {
      setPlayers(prev => prev.map(p =>
        p.id === data.playerId ? { ...p, status: 'Active', leftAt: undefined } : p
      ));
    });

    hub.on('GameStatusChanged', (data: { gameId: string; status: string }) => {
      setGameStatus(data.status as GameStatus);
    });

    hub.on('Reconnected', () => {
      setIsReconnecting(false);
      refreshState();
    });

    hub.on('Reconnecting', () => {
      setIsReconnecting(true);
    });

    return () => {
      hub.off('GMStatusChanged');
      hub.off('PlayerJoined');
      hub.off('PlayerLeft');
      hub.off('PlayerDisconnected');
      hub.off('PlayerReconnected');
      hub.off('GameStatusChanged');
      hub.off('Reconnected');
      hub.off('Reconnecting');
    };
  }, [hub, refreshState]);

  const reconnect = useCallback(async () => {
    if (hub) {
      setIsReconnecting(true);
      await hub.reconnect();
    }
  }, [hub]);

  return {
    gameStatus,
    gmStatus,
    players,
    lastGMAction,
    isLoading,
    error,
    isReconnecting,
    refreshState,
    reconnect,
  };
}
```

**Step 3: Verify TypeScript compiles**

```bash
cd src/Adnd.Client && npx tsc --noEmit 2>&1 | head -20
```

**Step 4: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Client/src/api/gameStateHook.ts
git commit -m "feat: add useGameGameState hook for unified frontend state management"
```

---

## Task 8: Update GameRoomPage to use `useGameGameState`

**Files:**
- Modify: `src/Adnd.Client/src/pages/GameRoomPage.tsx`

**Step 1: Read the current GameRoomPage**

```bash
head -100 src/Adnd.Client/src/pages/GameRoomPage.tsx
```

**Step 2: Add the hook import and state**

```typescript
import { useGameGameState } from '../api/gameStateHook';

// In the component:
const gameState = useGameGameState(gameId);
```

**Step 3: Add state indicator to the page header**

Add a visual indicator showing current state:
```typescript
{gameState.isLoading && <CircularProgress size={20} />}
{gameState.error && <Alert severity="error">{gameState.error}</Alert>}
{gameState.isReconnecting && <Alert severity="warning">Reconnecting...</Alert>}

{gameState.gameStatus === 'Created' && (
  <Chip label="Created" color="default" size="small" />
)}
{gameState.gameStatus === 'Starting' && (
  <Chip label="Starting..." color="info" size="small" />
)}
{gameState.gameStatus === 'Active' && gameState.gmStatus === 'Running' && (
  <Chip label="GM Running" color="success" size="small" />
)}
{gameState.gameStatus === 'Active' && gameState.gmStatus === 'Idle' && (
  <Chip label="GM Idle" color="warning" size="small" />
)}
{gameState.gameStatus === 'Active' && gameState.gmStatus === 'Paused' && (
  <Chip label="GM Paused" color="error" size="small" />
)}
{gameState.gameStatus === 'Archived' && (
  <Chip label="Archived" color="default" size="small" />
)}

{gameState.lastGMAction && (
  <Typography variant="caption" color="text.secondary">
    Last action: {gameState.lastGMAction.action} ({formatDistance(new Date(gameState.lastGMAction.at), new Date())} ago)
  </Typography>
)}
```

**Step 4: Add creator nudge button in admin/settings area**

Only show when:
- User is the creator
- GM status is `Idle`
- Game status is `Active`

```typescript
{isCreator && gameState.gmStatus === GMStatus.Idle && gameState.gameStatus === GameStatus.Active && (
  <Button
    variant="outlined"
    size="small"
    onClick={handleNudge}
    sx={{ mt: 1 }}
  >
    Nudge GM
  </Button>
)}
```

**Step 5: Add `handleNudge` function**

```typescript
const handleNudge = async () => {
  if (!gameId) return;
  try {
    await APIClient.post(`/admin/games/${gameId}/nudge`, { direction: '' });
  } catch (err) {
    console.error('Nudge failed:', err);
  }
};
```

**Step 6: Verify TypeScript compiles**

```bash
cd src/Adnd.Client && npx tsc --noEmit 2>&1 | head -20
```

**Step 7: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Client/src/pages/GameRoomPage.tsx
git commit -m "feat: update GameRoomPage with useGameGameState hook and state indicators"
```

---

## Task 9: Add `/admin/games/{id}/nudge` endpoint

**Files:**
- Modify: `src/Adnd.Server/Controllers/AdminController.cs`

**Step 1: Add the endpoint**

```csharp
[HttpPost("games/{gameId}/nudge")]
[Authorize]
public async Task<IActionResult> NudgeGM(Guid gameId, [FromBody] NudgeRequest request)
{
    var creatorId = UserIdProvider.GetUserId(User);
    if (creatorId == Guid.Empty) return Unauthorized();

    var game = await _context.Games.FindAsync(gameId);
    if (game == null || game.CreatorId != creatorId)
        return Forbid();

    if (game.GMStatus != GMStatus.Idle)
        return BadRequest("GM is not idle. Cannot nudge.");

    var call = await _agentBus.SendSwayAsync(gameId, creatorId, request.Direction);
    return Ok(new { callId = call.Id, status = call.Status });
}

public record NudgeRequest(string Direction);
```

**Step 2: Verify compiles**

```bash
cd src/Adnd.Server && dotnet build --no-restore 2>&1 | tail -5
```

**Step 3: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Server/Controllers/AdminController.cs
git commit -m "feat: add nudge GM endpoint for creator"
```

---

## Task 10: Add loading skeletons and error recovery to GameRoomPage

**Files:**
- Modify: `src/Adnd.Client/src/pages/GameRoomPage.tsx`

**Step 1: Add loading skeleton**

When `gameState.isLoading`, show a skeleton layout:
```typescript
if (gameState.isLoading) {
  return (
    <Box sx={{ p: 3 }}>
      <Skeleton variant="rectangular" width="100%" height={40} sx={{ mb: 2 }} />
      <Skeleton variant="rectangular" width="100%" height={200} sx={{ mb: 2 }} />
      <Skeleton variant="rectangular" width="100%" height={100} />
    </Box>
  );
}
```

**Step 2: Add error recovery with retry**

When `gameState.error`, show an alert with retry:
```typescript
{gameState.error && (
  <Alert severity="error" sx={{ mb: 2 }}>
    {gameState.error}
    <Button size="small" onClick={() => gameState.refreshState()} sx={{ ml: 2 }}>
      Retry
    </Button>
  </Alert>
)}
```

**Step 3: Add archived game banner**

When `gameState.gameStatus === 'Archived'`:
```typescript
{gameState.gameStatus === 'Archived' && (
  <Alert severity="info" sx={{ mb: 2 }}>
    This game has been archived.
    <Button size="small" onClick={() => navigate('/dashboard')} sx={{ ml: 1 }}>
      Back to Dashboard
    </Button>
  </Alert>
)}
```

**Step 4: Verify TypeScript compiles**

```bash
cd src/Adnd.Client && npx tsc --noEmit 2>&1 | head -20
```

**Step 5: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Client/src/pages/GameRoomPage.tsx
git commit -m "feat: add loading skeletons, error recovery, and archived banner to GameRoomPage"
```

---

## Task 11: Add `GameStatusChanged` event and broadcast

**Files:**
- Modify: `src/Adnd.Server/Events/GameEvents.cs`
- Modify: `src/Adnd.Server/Hubs/GameHub.cs`

**Step 1: Add event to `GameEvents.cs`:**

```csharp
public record GameStatusChanged(Guid GameId, GameStatus NewStatus) : INotification;
```

**Step 2: Add broadcast method to `GameHub`:**

```csharp
public async Task BroadcastGameStatusAsync(Guid gameId, GameStatus status)
{
    await _mediator.Publish(new GameStatusChanged(gameId, status));
    await Clients.Group(gameId.ToString()).SendAsync("GameStatusChanged", new
    {
        GameId = gameId,
        Status = status,
        ChangedAt = DateTime.UtcNow
    });
}
```

**Step 3: Call from `AgentBus` when game status changes:**

In `ActivateGameAgentAsync`, after setting `Status = Active`:
```csharp
await _hubContext.Clients.Group(game.Id.ToString()).SendAsync("GameStatusChanged", new
{
    GameId = game.Id,
    Status = game.Status,
    ChangedAt = DateTime.UtcNow
});
```

**Step 4: Verify compiles**

```bash
cd src/Adnd.Server && dotnet build --no-restore 2>&1 | tail -5
```

**Step 5: Commit**

```bash
cd C:/git/adnd/.worktrees/game-state-stabilization
git add src/Adnd.Server/Events/GameEvents.cs src/Adnd.Server/Hubs/GameHub.cs src/Adnd.Server/Services/AgentBus.cs
git commit -m "feat: add GameStatusChanged event and broadcast for frontend state sync"
```

---

## Verification

### Build verification
```bash
cd C:/git/adnd/.worktrees/game-state-stabilization/src/Adnd.Server && dotnet build
cd C:/git/adnd/.worktrees/game-state-stabilization/src/Adnd.Client && npx tsc --noEmit
```

### Functional verification checklist
- [ ] Game room shows "Starting..." when game is created and being started
- [ ] Opening narrative transitions state from "Starting" to "Active"
- [ ] GM status indicator shows Running/Idle/Paused correctly
- [ ] Creator can see "Nudge GM" button when GM is idle
- [ ] `PlayerDisconnectDetector` is removed (no longer runs)
- [ ] Disconnects are detected via SignalR (check logs)
- [ ] Navigation to `/game/:id` shows loading skeleton, not empty page
- [ ] API failures show retry button, not white page
- [ ] Archived games show banner with dashboard link
- [ ] All existing API contracts unchanged
- [ ] `dotnet build` succeeds
- [ ] TypeScript compiles without errors

---

## Summary

**11 tasks, ~8-10 hours total.** Each task is self-contained and backwards-compatible.

| Priority | Tasks | Area |
|----------|-------|------|
| P0 | 1-4 | Game state visibility (Starting state, UI indicators, creator nudge) |
| P0 | 7-8, 10 | Frontend state unification (hook, page updates, error recovery) |
| P1 | 5 | Disconnect detection cleanup (remove detector, consolidate in Hub) |
| P1 | 6 | Event timing (async handlers) |
| P2 | 9, 11 | Creator nudge endpoint, GameStatusChanged broadcast |
