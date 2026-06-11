# Design: Async Game Creation + Iterative Tool Calling

**Date:** 2026-06-11  
**Status:** Implemented

## Problem

### 1. Game creation taking too long
The game creation flow was synchronous and blocking:
1. `GamesController.CreateGame()` saves game → returns
2. `GameStarted` event fires → `PlotWeaverHandler` calls LLM to generate plot threads (blocks)
3. `GameLifecycleHandler` queues opening narrative → GameAgent picks it up → LLM call with tool calling
4. After tool call results, a **second** LLM call via `CompleteAsync` (not `CompleteWithToolsAsync`)

The API didn't return until both LLM calls completed — 2+ blocking LLM calls.

### 2. Tool calling not being forwarded for iterative execution
After the GM LLM returned tool calls (e.g., `narrate`), the system:
1. Executed the tool calls synchronously
2. Fed results back to the LLM via `CompleteAsync()` — **not** `CompleteWithToolsAsync()`
3. So the LLM could never make a **second round** of tool calls

The `narrate` tool was essentially a no-op (returned metadata only), forcing a redundant follow-up call.

## Design

### Part 1: Async Game Creation

**New flow:**
```
CreateGame API → save game → GameStarted event → PlotWeaverHandler queues thread generation via AgentCall → API returns
GameAgent picks up "GenerateInitialThreads" call → LLM generates threads → saves to DB
GameAgent picks up "OpenNarrative" call → iterative tool calling → saves narrative → transitions Starting → Active
```

**Changes:**
- `PlotWeaverHandler.Handle(GameStarted)` — queues initial thread generation via `AgentCall` instead of calling LLM directly
- `GameLifecycleHandler.Handle(GameStarted)` — queues opening narrative via `AgentCall` with `Action = OpenNarrative`
- `AgentBus.HandleGMCall` — adds `GenerateInitialThreads` and `OpenNarrative` handlers
- `InitialThreadsGenerated` event — published when threads are generated

### Part 2: Iterative Tool Calling Loop

**New loop in `HandleOpenNarrative`:**
```
Loop (max N rounds):
  1. Call CompleteWithToolsAsync(systemPrompt, followUpPrompt, tools)
  2. If no tool calls → return content as final narrative
  3. Execute each tool call
     - If requires user input → pause (existing behavior)
     - If error → log, skip, continue
  4. Format tool results
  5. Set followUpPrompt = tool results + "continue the narrative"
  6. Reset systemPrompt to original
  7. Repeat (increment depth counter)
```

**Config:** `ToolCallingMaxDepth` via appsettings.json or env var (default: 5)

### Part 3: Narrate Tool Calls LLM

The `narrate` tool now calls the LLM with `context`, `tone`, `focus` params and returns the generated narrative as `Output`. This makes the tool a real action, not just metadata.

### Part 4: Remove Auto-Narrate Idle Logic

Auto-narrate after 3 minutes of idle time was removed. Idle time = players thinking or between sessions, not a signal to narrate. PlotWeaver handles dynamic story development via event-driven momentum tracking.

## Files Modified

| File | Changes |
|------|---------|
| `Models/AgentCall.cs` | Added `OpenNarrative` and `GenerateInitialThreads` to `AgentAction` enum |
| `Events/GameEvents.cs` | Added `InitialThreadsGenerated` event |
| `Handlers/GameEventHandlers.cs` | Added `AppDbContext` to `GameLifecycleHandler`; updated `Handle(GameStarted)` to queue via AgentCall |
| `Handlers/PlotWeaverHandler.cs` | Updated `Handle(GameStarted)` to queue thread generation via AgentCall |
| `Services/AgentBus.cs` | Added `IConfiguration` dependency; added `GenerateInitialThreads` and `OpenNarrative` handlers; replaced single `CompleteAsync` follow-up with iterative tool loop |
| `Services/GMToolRegistry.cs` | Added `ILLMProviderFactory` and `IApiKeyEncryptionService` dependencies; updated `ExecuteNarrate` to call LLM |
| `Agent/GameAgent.cs` | Removed auto-narrate idle logic |

## Config

```json
{
  "ToolCallingMaxDepth": 5
}
```

Or via env var: `ToolCalling__MaxDepth=5`

## Testing

Build succeeds with no new errors. Pre-existing warnings remain (nullable reference warnings in unrelated files).
