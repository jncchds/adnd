# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Working in this repo

Rules for anyone (or anything) making changes here:

- **Build with `docker compose build --no-cache`.** Layer caching has produced misleading
  green builds in this repo; a cold build is the only trusted signal.
- **Do not start, stop or recreate containers.** No `docker compose up`, `restart`, `down`
  or `stop` — running the stack is the maintainer's call. Build, report the result, and stop.
- **Never run `dotnet ef database update`.** Migrations apply on startup (see below).
- Use the `sdk` profile for anything that needs the .NET toolchain
  (`docker compose run --rm sdk …`); it does not touch the running app.
- The backend must build with **0 warnings** (`TreatWarningsAsErrors`), and the frontend
  must pass `tsc --noEmit && vite build`. Never suppress a warning to get green.

## Build & Run

**Docker Compose is the only supported workflow.** There is no `dotnet run` or Vite dev server path.

```bash
# First run: copy and fill in required env vars
cp .env.example .env

# Build the images (always --no-cache; see "Working in this repo")
docker compose build --no-cache

# Start the full stack
docker compose up

# Endpoints once running
# App:      http://localhost:5010
# Health:   http://localhost:5010/health        (liveness — database only)
# Ready:    http://localhost:5010/health/ready  (also probes LLM provider + pgvector)
# Swagger:  http://localhost:5010/swagger       (Development only unless SWAGGER__ENABLED=true)
# Hangfire: http://localhost:5010/hangfire      (requires HANGFIRE__DASHBOARD_ENABLED=true)
```

Required `.env` values: `POSTGRES_PASSWORD`, `JWT_SECRET_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `ENCRYPTION_MASTER_KEY`.

`JWT_SECRET_KEY` must be ≥32 bytes and not the `OVERRIDE_IN_ENVIRONMENT` placeholder — the app refuses to start otherwise. See `.env.example` for optional security settings (`SECURITY__ALLOW_PRIVATE_LLM_ENDPOINTS`, `SWAGGER__ENABLED`, `HANGFIRE__DASHBOARD_ENABLED`).

## EF Core Migrations

Always use the `sdk` Docker profile — never run `dotnet ef` against a local toolchain.

```bash
docker compose run --rm sdk dotnet ef migrations add <Name> --project src/Adnd.Server
```

Migrations are applied automatically on startup by `MigrationService.ApplyMigrationsAsync()`. **Never run `dotnet ef database update`** — starting the app is how migrations get applied.

`DesignTimeDbContextFactory` builds the context for the EF tooling so `dotnet ef` does not boot the full host (which would demand a real JWT key and encryption key it has no need for).

Current migrations: `InitialCreate`, `AddCharacterBackstory`, `AgentLoopToolCallState`, `PlotThreadResolvedAt`, `AddLlmInteractionLogStatus`, `AddAgentCallStepHistoryAndLlmReasoning`, `AddAgentCallRequestedByPlayer`, `NPCStatusAndLastSeen`, `ClearPrivateMessageEmbeddings`.

**Check scaffolded migrations before accepting them.** EF emits `jsonb NOT NULL DEFAULT ''` for new `JsonElement` columns; `''` is not valid JSON and the `ALTER TABLE` fails. The converter's sentinel is the literal `"null"`.

## Project Structure

```
src/
  Adnd.Server/          # .NET 10 ASP.NET Core Web API (sole backend project)
    Agent/              # GameAgent, GameAgentManager
    Controllers/        # REST API endpoints
    Data/               # AppDbContext, MigrationService, migrations
    Events/             # GameEvents records (Wolverine message types)
    Handlers/           # Wolverine message handlers
    Hubs/               # SignalR GameHub (partial class, ~19 files)
    Models/             # EF entities + Enums.cs
    Services/           # All domain services
      Llm/              # ILLMProvider + per-provider implementations
      Combat*/          # 12 scoped combat sub-services
  Adnd.Client/          # React 19 + TypeScript + Vite 6 (compiled into wwwroot at Docker build time)
docker-compose.yml
DESIGN_DOCUMENT.md
```

The React frontend is compiled during the Docker build stage and served as static files; there is no separate frontend container.

## Architecture

**ADnD** is a multiplayer TTRPG platform where an AI Game Master runs campaigns in real time.

```
Browser (React 19 + MUI v7)
    ├── REST   /api/**     — CRUD, auth, admin
    └── WS     /gamehub   — SignalR real-time events
                    |
            Adnd.Server (.NET 10)
                    |
        ┌───────────┴──────────────┐
   PostgreSQL 17             Wolverine message bus
   + pgvector                (PostgreSQL-backed sagas)
```

### Event-Driven Core (Wolverine)

All GM actions flow through durable Wolverine messages persisted to PostgreSQL. The handler chain per GM action:

```
AgentCallQueued → AgentSaga.Start
    → LLMDispatchHandler → AgentSaga.Handle(LLMResponseReceived)
    → [ToolExecutionHandler → AgentSaga.Handle(ToolCallCompleted) → LLMFollowUpHandler] (loop if tools)
    → AgentSaga.Handle(NarrativeReady) → SignalR push to players
```

`AgentSaga` is the state machine keyed on `AgentCall.Id`. States: `Init → LLMDispatch → LLMResponse → ToolExecution → LLMFollowUp → NarrativeReady → Completed | Failed`. 5-minute timeout → `Failed`.

**A saga-routed message (one carrying `[property: SagaIdentity]`) is delivered only to that saga's `Start`/`Handle` methods.** A separate plain-handler class registered for the same message type is silently never invoked — no exception, no log, the message just doesn't reach it. This cost a full gameplay loop, four separate times over: `SagaOrchestratorHandler` (for `AgentCallQueued`), `LLMResponseHandler` (for `LLMResponseReceived`), `CoordinatorHandler` (for `ToolCallCompleted`), and `NarrativeHandler` (for `NarrativeReady`) each used to sit *alongside* the saga's own `Handle` method for the same message, silently dead on arrival every time — Wolverine gave no error, so each one shipped, got tested with a code path that happened not to exercise it, and only surfaced later. In the `NarrativeReady` case specifically, this meant the saga reached "Completed" while the chat message was never saved or broadcast and `AgentCall.Status` never left `Running` — from the admin UI it looked identical to a hung LLM call. All four are now merged directly into `AgentSaga`'s own `Handle` methods, and the standalone handler classes are deleted. Do not add a standalone handler for a message type a saga already owns — put the logic in the saga's `Handle` method, and grep for an existing plain handler on the same message type before assuming one doesn't already conflict.

**Any message routed to `AgentSaga` must carry `[property: SagaIdentity]` on `AgentCallId`.** Wolverine only recognises a property named `Id`, `SagaId` or `AgentSagaId` by convention; without the attribute it cannot correlate the message to its saga and the whole chain silently stops. See `Events/GameEvents.cs`.

**Terminal state has exactly one owner.** `AgentCallFailed` is a *non-terminal* signal handled only by `AgentCallFailedHandler`, which decides whether to re-dispatch or give up. When it gives up it publishes `AgentCallAbandoned`, which the saga handles and which is the only path (besides `SagaTimeout`) that writes `AgentCallStatus.Failed`. Do not have both a handler and the saga write `AgentCall.Status` — they run on separate `DbContext`s.

### GM Tool Registry

15 registered tools: `narrate`, `rollDice`, `skillCheck`, `requestPlayerRoll`, `queryCharacter`, `queryNPCs`, `registerNPC`, `updateNPCStatus`, `searchPlotContext`, `updateGameState`, `sendWhisper`, `startCombat`, `addCombatParticipant`, `generateLoot`, `wait`. Executed sequentially by `CoordinatorHandler`.

`registerNPC` is how an NPC introduced in narration becomes a real row instead of a name that scrolls out of the RAG window and comes back as a different person. It is deliberately a *same-turn* tool: `BuildNarrateSystemPromptAsync` lists the NPCs in play and tells the model to call it alongside `narrate` in the same response, so there is no second LLM pass to bill or to fail. Matching is by **name**, case-insensitively scoped to the game — never by an id, which the model would have to invent — and an existing name updates rather than duplicating. Only fields the model actually supplied are written, so a later attitude-only call doesn't blank the description.

`updateNPCStatus` is the other half: `NPCStatus` is `Active | Dead | Departed`, and only `Active` NPCs are offered to the prompt unprompted. Without it the roster only grows and the narrator keeps being handed people the party killed two sessions ago. It refuses to create — a status change naming an unregistered NPC means the model invented the name. `registerNPC` revives `Departed` (they were just written back into a scene) but never `Dead`, which needs an explicit status call.

### NPC relevance

`INPCRelevanceService.GetRelevantAsync(gameId, sessionId, limit = 8)` decides which NPCs the narrator sees, and is the **only** thing that should build an NPC list for a prompt — both `BuildNarrateSystemPromptAsync` and `RAGService.GeneratePlotContextAsync` (`=== NPCs IN PLAY ===`) go through it. A campaign accumulates NPCs indefinitely; a prompt cannot, and the old unbounded dump buried the two people actually in the room. Ranking is name-mentions in the last 20 table messages first, then `Active` NPCs by `LastSeenAt` desc; a `Dead`/`Departed` NPC is included only while the party is still talking about them. `LastSeenAt` is stamped by `registerNPC`/`updateNPCStatus` and on manual creation. This is recency, not semantics — NPCs have no embedding column, and adding one would cost an embedding call per turn to answer a question recency already answers. `queryNPCs` remains the unfiltered escape hatch and reports `Status`.

`wait` is the GM declining to act this turn — every in-character player line fires a narrate call, so without it the GM is structurally obliged to interject on both halves of a conversation the characters are having with each other. It never reaches `GMToolRegistry.ExecuteToolAsync` in the normal case: `AgentSaga.Handle(LLMResponseReceived)` intercepts a response whose tool calls are *all* `wait` (and only for `AgentAction.Narrate`) and completes the saga right there — no tools run, no follow-up LLM call, no `Message` row, nothing broadcast but the `Completed` step that clears the client's activity chip. Silence is therefore not the same as the empty-narrative guard in `Handle(NarrativeReady)`, which still treats a blank response as a failure to retry. `GameHub.BuildNarrateSystemPromptAsync` forbids `wait` once the GM has been silent for `WaitStreakLimit` (3) consecutive table messages, so a model that settles into waiting can't leave the table talking to itself forever.

The pending tool list lives on `ToolCallCoordinator.ToolCalls` — **not** on `AgentCall.Output`, which holds narrative text. Tools requiring confirmation (`requestPlayerRoll`) are persisted as `GMToolCall` rows with `Status = AwaitingConfirmation` and resume the saga via `ToolCallConfirmationResolved` when `/api/gmtools/{id}/confirm|decline` is called.

Tool arguments are LLM-generated: parse ids with `TryGetGuid` rather than `Guid.Parse`, and always scope looked-up entities to `gameId`.

### LLM Provider Abstraction

`ILLMProviderFactory` resolves to `ILLMProvider` based on `LLMPreset.ProviderType`. Supported values: `ollama`, `openaicompatible`, `openai`, `google`. API keys stored encrypted at rest via AES-256-GCM (`ApiKeyEncryptionService`).

### What the narrator is allowed to have seen

`MessageVisibility.IsVisibleToNarration` (`Data/MessageVisibility.cs`) is the **only** definition
of which messages may reach the GM: not OOC, and no whisper routing in either direction. Anything
that builds prompt context or writes `Message.Embedding` goes through `.VisibleToNarration()` —
`RAGService.GeneratePlotContextAsync`, `GenerateSessionSummaryAsync`, `EmbedMessagesAsync`,
`EmbedMessageAsync`, and `NPCRelevanceService`. This was previously spelled out inline in four
places with three different predicates, and the weakest one (`!IsOOC` alone, in the session
summary) put whispers into the "Previously..." recap that `GameStartService` publishes to the
whole table.

Decide on `WhisperFromId`/`WhisperToId`, never on `Type == "Whisper"`: a private GM-suggest reply
is stored with `Type "GM"` and only its `WhisperToId` marks it private. The converse needs no
rule — a public in-character line carries no routing fields and is therefore included, which is
exactly why a character repeating a whisper publicly *should* influence narration.

Embeddings are filtered at the **write** site, not at each read. Nothing similarity-searches
`Message.Embedding` today (only `PlotThread.Embedding`), so a read-side filter would be a rule
the next feature could forget; a null vector cannot leak.

### RAG + Plot Intelligence

`RAGService` builds GM prompt context from recent messages, active plot threads, and NPC states using pgvector HNSW embeddings on `Message.Embedding` and `PlotThread.Embedding`. `PlotWeaver` runs periodic LLM-driven reviews to adapt and generate plot threads.

### SignalR Hub

`GameHub` is a partial class split across ~19 files (combat, dice, spells, inventory, whispers, character creation, etc.). JWT is extracted from the WebSocket query string (`?access_token=…`). Online players tracked in a static `ConcurrentDictionary<string, string>` (connectionId → userId).

**Every hub method that takes a `gameId` must start with `RequireMemberAsync(gameId)` or `RequireCreatorAsync(gameId)`** (both on `GameHub.cs`). `Clients.Group(...)` does *not* require the caller to be in the group, so an unchecked method lets any authenticated user act in any game. Combat methods additionally call `RequireCombatInGameAsync` / `RequireParticipantAsync` so ids cannot be borrowed across games. Prefer `Clients.User(userId)` over scanning `_playerConnections`.

### JSONB Storage

Character attributes, skills, inventory, spells, conditions, NPC data, plot adaptation history, and combat metadata are all stored as PostgreSQL `jsonb` using EF value converters. Avoids migrations for game-system-specific data changes.

Value comparers must compare by **content**, not by identity or length — the `MilestoneEvents` comparer compared `a.Count == b.Count`, so in-place edits were invisible to the change tracker and were never persisted.

## Authorization

This is the convention most easily broken by adding a new endpoint, and the one with the worst consequences.

**Controllers that operate on a game derive from `GameScopedController`** and gate every action:

```csharp
if (await RequireMember(gameId) is { } failure) return failure;   // any player in the game
if (await RequireCreator(gameId) is { } failure) return failure;  // the GM only
```

When the route carries an entity id rather than a `gameId`, load the entity first and authorize against **its** owning game — never against a game id supplied by the caller.

Rules of thumb:
- Reading game content → `RequireMember`. Mutating world state, driving the GM, or anything that spends LLM budget → `RequireCreator`.
- A `Character` reaches its game through `Player.GameId`, and `Player` is soft-deleted independently of `Character` — read past the query filter with `IgnoreQueryFilters()` or a kicked player's sheet throws.

## Serialization

**Never return EF entities that reach a `User` or an API key.** Use a DTO. Two live leaks came from this: `Player.User.PasswordHash` via `GET /api/games/{id}/players`, and `LLMPreset.DecryptedApiKey` via the preset endpoints. `[JsonIgnore]` now guards both, but the DTO is the real fix — see `PlayerDto`, `LLMPresetDto`.

Request bodies bind to DTOs, never to entities: binding `NPC`/`PlotThread`/`Character` directly let callers set `Id`, `GameId`, `UserId` and `IsDeleted`.

Client TypeScript types must describe the **wire DTO**, not the EF entity. Most of `src/types/` originally described entities, so fields the UI read were never actually sent and silently rendered `undefined`.

## Key Conventions

- **`TreatWarningsAsErrors = true`** — fix all compiler warnings before committing.
- **`nullable enable` + `ImplicitUsings`** — nullability annotations required throughout.
- **Primary constructor syntax** (C# 12) used everywhere.
- **Interface-per-service** — every service has a matching `IServiceName` registered in DI. Scoped for anything touching `AppDbContext`; singleton for stateless services (`IDiceEngine`, `ISystemRegistry`, `IGameAgentManager`, `IApiKeyEncryptionService`, `ILLMProviderFactory`).
- **Soft delete** — `ISoftDelete` on `Game`, `Player`, `Message`, `Character`, `NPC`, `PlotThread` with global EF query filters.
- **CamelCase JSON + `JsonStringEnumConverter`** for all API responses. Note this applies to MVC only — a bare `JsonSerializer.Deserialize` call uses case-*sensitive* defaults, which is how spell-slot consumption silently broke.
- **Hangfire** (PostgreSQL-backed, 2 workers) for background jobs alongside Wolverine.
- **Swagger is Development-only by default** (`SWAGGER__ENABLED` to override). It documents the full attack surface and cannot sit behind bearer auth, so it is not served anonymously in Production.
- **No HTTPS in the app layer** — TLS terminates at a reverse proxy. Never add `UseHttpsRedirection` or `UseHsts`.
- **Never swallow errors.** No bare `catch { }` on the server, and no `catch { /* silent */ }` in client hooks — expose an `error` and surface it. Silent failure is why a non-functional chat, ~20 dead routes and a broken agent loop all went unnoticed.
- No test projects exist in this solution. There is consequently no safety net: verify changes by running the stack and exercising the affected flow.
