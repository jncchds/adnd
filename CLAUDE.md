# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

**Docker Compose is the only supported workflow.** There is no `dotnet run` or Vite dev server path.

```bash
# First run: copy and fill in required env vars
cp .env.example .env

# Build and start the full stack
docker compose up --build

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

Current migrations: `InitialCreate`, `AddCharacterBackstory`, `AgentLoopToolCallState`, `PlotThreadResolvedAt`.

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
AgentCallQueued → SagaOrchestratorHandler → AgentSaga
    → LLMDispatchHandler → LLMResponseHandler
    → [ToolExecutionHandler → CoordinatorHandler → LLMFollowUpHandler] (loop if tools)
    → NarrativeHandler → SignalR push to players
```

`AgentSaga` is the state machine keyed on `AgentCall.Id`. States: `Init → LLMDispatch → LLMResponse → ToolExecution → LLMFollowUp → NarrativeReady → Completed | Failed`. 5-minute timeout → `Failed`.

**Any message routed to `AgentSaga` must carry `[property: SagaIdentity]` on `AgentCallId`.** Wolverine only recognises a property named `Id`, `SagaId` or `AgentSagaId` by convention; without the attribute it cannot correlate the message to its saga and the whole chain silently stops. See `Events/GameEvents.cs`.

**Terminal state has exactly one owner.** `AgentCallFailed` is a *non-terminal* signal handled only by `AgentCallFailedHandler`, which decides whether to re-dispatch or give up. When it gives up it publishes `AgentCallAbandoned`, which the saga handles and which is the only path (besides `SagaTimeout`) that writes `AgentCallStatus.Failed`. Do not have both a handler and the saga write `AgentCall.Status` — they run on separate `DbContext`s.

### GM Tool Registry

12 registered tools: `narrate`, `rollDice`, `skillCheck`, `requestPlayerRoll`, `queryCharacter`, `queryNPCs`, `searchPlotContext`, `updateGameState`, `sendWhisper`, `startCombat`, `addCombatParticipant`, `generateLoot`. Executed sequentially by `CoordinatorHandler`.

The pending tool list lives on `ToolCallCoordinator.ToolCalls` — **not** on `AgentCall.Output`, which holds narrative text. Tools requiring confirmation (`requestPlayerRoll`) are persisted as `GMToolCall` rows with `Status = AwaitingConfirmation` and resume the saga via `ToolCallConfirmationResolved` when `/api/gmtools/{id}/confirm|decline` is called.

Tool arguments are LLM-generated: parse ids with `TryGetGuid` rather than `Guid.Parse`, and always scope looked-up entities to `gameId`.

### LLM Provider Abstraction

`ILLMProviderFactory` resolves to `ILLMProvider` based on `LLMPreset.ProviderType`. Supported values: `ollama`, `openaicompatible`, `openai`, `google`. API keys stored encrypted at rest via AES-256-GCM (`ApiKeyEncryptionService`).

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
