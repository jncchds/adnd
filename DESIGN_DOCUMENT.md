# ADnD — Complete Design Document

> **Status**: ALPHA. This document describes the system as it exists and is sufficient to recreate it from scratch in working state.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture Overview](#2-architecture-overview)
3. [Infrastructure & Deployment](#3-infrastructure--deployment)
4. [Backend: Adnd.Server](#4-backend-adndserver)
   - 4.1 [Technology Stack](#41-technology-stack)
   - 4.2 [Program.cs — Startup & DI](#42-programcs--startup--di)
   - 4.3 [Data Layer](#43-data-layer)
   - 4.4 [Domain Models](#44-domain-models)
   - 4.5 [Event System](#45-event-system)
   - 4.6 [Agent Framework & Saga](#46-agent-framework--saga)
   - 4.7 [LLM Provider System](#47-llm-provider-system)
   - 4.8 [SignalR Hub](#48-signalr-hub)
   - 4.9 [Services](#49-services)
   - 4.10 [Controllers](#410-controllers)
   - 4.11 [Security](#411-security)
5. [Frontend: Adnd.Client](#5-frontend-adndclient)
   - 5.1 [Technology Stack](#51-technology-stack)
   - 5.2 [Build Configuration](#52-build-configuration)
   - 5.3 [Routing & Layout](#53-routing--layout)
   - 5.4 [API Client](#54-api-client)
   - 5.5 [SignalR Hook](#55-signalr-hook)
   - 5.6 [Pages](#56-pages)
   - 5.7 [Theme System](#57-theme-system)
6. [Complete Data Flow](#6-complete-data-flow)
7. [RPG Systems](#7-rpg-systems)
8. [Plot Intelligence (RAG + PlotWeaver)](#8-plot-intelligence-rag--plotweaver)
9. [Combat System](#9-combat-system)
10. [Configuration Reference](#10-configuration-reference)
11. [Database Schema Summary](#11-database-schema-summary)
12. [Recreating From Scratch](#12-recreating-from-scratch)

---

## 1. Project Overview

ADnD is a multiplayer, LLM-powered TTRPG platform. A human Game Master can be fully replaced by an AI GM backed by a configurable LLM (OpenAI, Ollama, LM Studio, Google AI Studio). Players connect via real-time WebSockets (SignalR) and interact through a chat-first interface.

**Core capabilities:**
- Multi-game, multi-session management
- AI Game Master with tool-calling (dice, RAG, combat, whispers, narrative)
- Four LLM provider backends, configurable per-game via `LLMPreset`
- pgvector-backed semantic RAG for plot context and consistency
- Full combat tracker (D&D 5e, Pathfinder 2e, Call of Cthulhu 7e, custom)
- Plot thread management with AI momentum tracking and adaptation
- Character sheets, inventory, spells, death saves, conditions
- Durable saga architecture via Wolverine (PostgreSQL-backed message durability)
- JWT auth with refresh token rotation, rate limiting, encrypted API keys at rest

---

## 2. Architecture Overview

```
┌──────────────────────────────────────────────────────────┐
│                    Docker Compose                        │
│                                                          │
│  ┌─────────────────────────┐   ┌─────────────────────┐  │
│  │     Adnd.Server          │   │  PostgreSQL + pgvec  │  │
│  │   (.NET 10, port 8080)   │◄──│  (pgvector/pg17)     │  │
│  │                          │   │  volume: adnd-data   │  │
│  │  ┌──────────────────┐    │   └─────────────────────┘  │
│  │  │ Adnd.Client SPA   │   │                            │
│  │  │ (built into       │   │                            │
│  │  │  wwwroot)         │   │                            │
│  │  └──────────────────┘   │                            │
│  └─────────────────────────┘                            │
└──────────────────────────────────────────────────────────┘
         │
         │ HTTP/WS on ${APP_PORT:-5010}
         ▼
      Browser (React SPA)
```

**Communication layers:**
- REST API: `/api/**` — CRUD, auth, admin operations
- WebSocket: `/gamehub` — SignalR hub for real-time game events
- Static SPA: `index.html` fallback serves the React build from `wwwroot`
- Internal: Wolverine message bus (PostgreSQL-backed) for durable async saga orchestration

---

## 3. Infrastructure & Deployment

### docker-compose.yml

Two services: `postgres` and `app`.

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg17
    environment:
      POSTGRES_DB: adnd
      POSTGRES_USER: adnd
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    healthcheck:
      test: ["CMD", "pg_isready", "-U", "adnd"]
      interval: 5s
      timeout: 3s
      retries: 10
    volumes:
      - adnd-data:/var/lib/postgresql/data

  app:
    build:
      context: src/Adnd.Server
      dockerfile: Dockerfile
    ports:
      - "${APP_PORT:-5010}:8080"
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ConnectionStrings__Default: "Host=postgres;Database=adnd;Username=adnd;Password=${POSTGRES_PASSWORD};..."
      JwtSettings__SecretKey: ${JWT_SECRET_KEY}
      JwtSettings__Issuer: ${JWT_ISSUER}
      JwtSettings__Audience: ${JWT_AUDIENCE}
      Encryption__MasterKey: ${ENCRYPTION_MASTER_KEY}
      Resilience__RetryDelayMs: 500
      Resilience__MaxRetries: 3
      Resilience__FailureThreshold: 5
      Resilience__HalfOpenDurationSeconds: 30
      RateLimiting__GlobalPermitLimit: ...
      RateLimiting__AuthPermitLimit: ...
      RateLimiting__LLMPresetPermitLimit: ...
      Logging__LogLevel__Microsoft.EntityFrameworkCore: Warning

volumes:
  adnd-data:
```

### Dockerfile (src/Adnd.Server/Dockerfile)

Multi-stage:
1. **Build stage** (`mcr.microsoft.com/dotnet/sdk:10.0`): installs Node.js, builds the Vite client into `src/Adnd.Server/wwwroot`, then `dotnet publish`
2. **Runtime stage** (`mcr.microsoft.com/dotnet/aspnet:10.0`): copies published output, sets `ASPNETCORE_URLS=http://+:8080`, runs `Adnd.Server.dll`

The `vite.config.ts` build output target is `../Adnd.Server/wwwroot`, so client assets land directly alongside the server.

---

## 4. Backend: Adnd.Server

### 4.1 Technology Stack

| Concern | Package | Version |
|---------|---------|---------|
| Framework | ASP.NET Core | .NET 10.0 |
| ORM | Microsoft.EntityFrameworkCore + Npgsql | 10.0.0 |
| Vector DB | Pgvector + Pgvector.EntityFrameworkCore | 0.3.2 |
| Message bus / Sagas | WolverineFx + WolverineFx.Postgresql | 6.8.0 |
| Background jobs | Hangfire.AspNetCore + Hangfire.PostgreSql | latest |
| Ollama client | OllamaSharp | 5.4.12 |
| OpenAI client | OpenAI | 2.12.0 |
| Password hashing | BCrypt.Net-Next | 4.0.3 |
| JWT auth | Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 |
| Resilience | Polly | 8.6.6 |
| API docs | Swashbuckle.AspNetCore | 8.0.0 |
| Real-time | ASP.NET Core SignalR (built-in) | — |

### 4.2 Program.cs — Startup & DI

#### MVC / API
- `AddControllers()` with camelCase JSON naming + `JsonStringEnumConverter`
- `LowerCaseControllerConvention` on all routes (e.g., `/api/games`, not `/api/Games`)
- Swagger with Bearer security definition

#### Auth
- JWT Bearer: HS256, validates issuer + audience + lifetime + signing key
- Signing key from `JwtSettings__SecretKey`
- SignalR: JWT token extracted from query string `access_token` for WebSocket connections (via `OnMessageReceived` event)

#### Database
- `AppDbContext` with Npgsql + `UseVector()` for pgvector support
- `MigrationService` auto-applies pending migrations on startup via `app.UseDatabaseMigrations()`

#### Wolverine (Durable Message Bus)
```csharp
builder.Host.UseWolverine(opts => {
    opts.PersistMessagesWithPostgresql(connStr, "public", MessageStoreRole.Main);
    // AgentSaga registered automatically by Wolverine via scan
});
```
Wolverine persists messages and saga state in PostgreSQL, providing at-least-once delivery and durable saga orchestration without RabbitMQ or any external broker.

#### Service Registrations (in order)
```
IAuthService → AuthService (scoped)
MigrationService (scoped)
IUserIdProvider → UserIdProvider (scoped)
IGameAuthorizationService → GameAuthorizationService (scoped)
IDiceEngine → DiceEngine (singleton)
ISystemRulesFactory → SystemRulesFactory (singleton)
SystemRegistry (singleton)
IGameEngine → GameEngine (scoped)
[All ICombat* services → Combat* implementations]
IAgentBus → AgentBus (scoped)
IDeadLetterQueue → DeadLetterQueue (singleton)
IGMToolRegistry → GMToolRegistry (scoped)
IGMToolCallService → GMToolCallService (scoped)
IGameAgentManager → GameAgentManager (singleton)
EventBusWorker (singleton, also IEventBus + IHostedService)
IHandlerRegistry → HandlerRegistry (singleton)
IWhisperService → WhisperService (scoped)
IApiKeyEncryptionService → ApiKeyEncryptionService (singleton)
IResiliencePolicies → ResiliencePolicies (singleton)
HealthChecks: DatabaseHealthCheck, LlmProvidersHealthCheck, PgVectorHealthCheck
HttpClient "LLMProvider" (60s timeout)
IRAGService → RAGService (scoped)
IEmbeddingService → EmbeddingService (scoped)
ILLMPresetService → LLMPresetService (scoped)
ILLMInteractionLogger → LLMInteractionLogger (scoped)
IPlotWeaver → PlotWeaver (scoped)
ICharacterCreationFactory → CharacterCreationFactory (scoped)
IGameStartService → GameStartService (scoped)
INarrativeGenerationFactory → NarrativeGenerationFactory (scoped)
ILLMProviderFactory → LLMProviderFactory (singleton)
IGameManagementService, ISessionManagementService, IPlayerManagementService (scoped)
ISessionNoteService, IPromptTemplateService, IDiceStatsService, IGameTemplateService (scoped)
RateLimitingOptions
```

#### Middleware Pipeline (exact order)
1. `UseDatabaseMigrations()` — EF Core auto-migration
2. Swagger + SwaggerUI
3. `/health` (liveness) + `/health/ready` (readiness)
4. HTTPS redirection + HSTS (production only)
5. Static files + `UseDefaultFiles()`
6. CORS (`AllowAll` policy — any origin/method/header)
7. Routing
8. Security headers (X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy)
9. Forwarded headers (X-Forwarded-For / X-Forwarded-Proto)
10. Rate limiting
11. Authentication + Authorization
12. `MapControllers()`
13. `MapHub<GameHub>("/gamehub")`
14. `MapFallbackToFile("index.html")` — SPA fallback

### 4.3 Data Layer

#### AppDbContext

28 DbSet properties:

| DbSet | Entity | Notes |
|-------|--------|-------|
| `Users` | `User` | |
| `Games` | `Game` | Soft-delete global filter |
| `Players` | `Player` | Soft-delete global filter |
| `Characters` | `Character` | Soft-delete global filter |
| `GameSessions` | `GameSession` | |
| `Messages` | `Message` | Soft-delete global filter; `Embedding` = vector column |
| `NPCs` | `NPC` | Soft-delete global filter |
| `PlotThreads` | `PlotThread` | Soft-delete global filter; `Embedding` = vector column |
| `PlotReviews` | `PlotReview` | `Updates` = jsonb |
| `CustomSystems` | `CustomSystemDefinition` | |
| `RefreshTokens` | `RefreshToken` | Unique index on `Token` |
| `AgentCalls` | `AgentCall` | Self-referential parent/child; composite index (GameId, Status, CreatedAt DESC) |
| `ToolCallCoordinators` | `ToolCallCoordinator` | |
| `GMToolCalls` | `GMToolCall` | |
| `Whispers` | `Whisper` | |
| `LLMPresets` | `LLMPreset` | Unique (UserId, Name); cascade delete |
| `LLMInteractionLogs` | `LLMInteractionLog` | Composite index (UserId, OriginGameId, StartedAt) |
| `AuditLogs` | `AuditLog` | |
| `Combats` | `Combat` | |
| `CombatParticipants` | `CombatParticipant` | Cascade delete from Combat |
| `CombatEvents` | `CombatEvent` | Cascade delete from Combat |
| `SessionNotes` | `SessionNote` | |
| `PromptTemplates` | `PromptTemplate` | |
| `GameTemplates` | `GameTemplate` | |
| `EventRecords` | `EventRecord` | Indexes: (GameId, Status), CorrelationId (unique) |

**Key model configuration in `OnModelCreating`:**
- `PlotThread.Embedding` and `Message.Embedding` → column type `vector` (pgvector)
- `Message.Metadata` → `jsonb` with full `ValueComparer<JsonElement>`
- `PlotThread.AdaptationHistory` and `MilestoneEvents` → `jsonb` with JSON list converters
- `Game.InviteCode` → lowercase column `invitecode`, unique partial index where not null
- `Game → LLMPreset` FK: `SetNull` on delete
- `Game → CurrentSession` → one-to-one, `SetNull` on delete
- `Player` → unique index on `(GameId, UserId)`
- `Character → Player` → one-to-one FK on `Character.PlayerId`
- Soft-delete query filters on: Game, Player, Message, Character, NPC, PlotThread (filter `!e.IsDeleted`)
- `AgentCall.Input`/`Output` stored as `text` (large JSON payloads)
- `CombatParticipant.Conditions`, `SavingThrows`, `DeathSaveState`, `TemporaryHP` → `jsonb`

#### Migrations (chronological)

| Migration Name | Key Changes |
|---------------|-------------|
| `InitialCreate` | Full initial schema: Users, LLMPresets, RefreshTokens, Games, Players, Characters, GameSessions, Messages, NPCs, PlotThreads, AgentCalls, Whispers, CustomSystems |
| `HighPriorityFeatures` | Character backgrounds, spell slots, spellcasting ability/DC/bonus; NPC stats; Plot categories; Game language; session notes |
| `PerformanceIndexes` | Composite indexes for plot threads, combats, messages |
| `GameTemplates` | `GameTemplates` table; `PromptTemplates` table |
| `SoftDeleteAndWhispersCleanup` | Soft-delete columns on Game/Player/Message/Character/NPC/PlotThread; Whisper table refactor |
| `LLMInteractionLogSnapshotFields` | Snapshot fields (preset name, endpoint, model) on LLM interaction logs |
| `SingleSessionPerGame` | `Game.CurrentSessionId` one-to-one FK to `GameSession` |
| `EventBusReplacement` | `EventRecords` table for custom durable event bus (superseded by Wolverine but table remains) |
| `AddCurrentSessionIdColumn` | Fix for `CurrentSessionId` column naming |
| `ReactiveSagaArchitecture` | `ToolCallCoordinators` table; `AgentCall.CurrentStep` saga step column |
| `LLMPresetStrategyFields` | `LLMPreset.TimeoutMs`, `LLMPreset.ReasoningEffort` |

### 4.4 Domain Models

#### Game
```
Id (Guid)                    Status: Draft | Starting | Active | Archived
CreatorId (FK → User)        GMStatus: Idle | Running | Paused
LLMPresetId (FK, nullable)   LastGMAction (string)
CurrentSessionId (FK, one-to-one) LastGMActionAt (DateTimeOffset?)
Name, SystemId               PlotSeed, GameParameters, GameState (string)
SystemVersion                Language (default "English")
CustomSystemJson             InviteCode (nullable, stored lowercase)
Timestamps                   ISoftDelete
```

Collections: `Players`, `Sessions`, `NPCs`, `PlotThreads`, `PlotReviews`, `AgentCalls`

#### Character
Full character sheet:
```
Id, PlayerId (one-to-one FK)
Name, Class, Level, ProficiencyBonus
CurrentHP, MaxHP
Attributes (jsonb), Skills (jsonb), Inventory (jsonb), Spells (jsonb), Conditions (jsonb), CustomFields (jsonb)
Background, BackgroundSkills, BackgroundProficiencies, BackgroundFeatures
SpellSlots (jsonb), SpellcastingAbility, SpellSaveDC, SpellAttackBonus
ISoftDelete
```

#### AgentCall
The core unit of AI work:
```
Id, GameId, SessionId
FromAgent (AgentType), ToAgent (AgentType)
Action (AgentAction)
Input (text/JSON), Output (text/JSON), OutputMessage (string)
Status (AgentCallStatus), Error
ParentCallId (self-ref), ChildCalls
CurrentStep (SagaStep)
Timestamps, DurationMs, Metadata (jsonb)
```

**AgentType enum**: Creator, GM, LLM, Dice, RAG, NPC, Player, System

**AgentAction enum**: Query, Generate, Roll, Check, Narrate, Suggest, Execute, Notify, Recall, ManageState, Nudge, CreateCharacter, OpenNarrative, GenerateInitialThreads

**AgentCallStatus enum**: Pending, Running, Completed, Failed, Cancelled

**SagaStep enum**: None(0), Init(1), LLMDispatch(2), LLMResponse(3), ToolExecution(4), ToolCoordination(5), LLMFollowUp(6), NarrativeReady(7), Completed(8), Failed(9)

#### LLMPreset
Per-user LLM configuration stored in DB, API key encrypted at rest:
```
Id, UserId, Name
ProviderType: "ollama" | "lmstudio" | "openai" | "google"
BaseModel, EndpointUrl
ApiKey (encrypted AES-256-GCM), [NotMapped] DecryptedApiKey
Temperature (float), MaxTokens (int), TopP (float)
FrequencyPenalty (float), PresencePenalty (float), Stream (bool)
TimeoutMs (int?), ReasoningEffort: "none" | "low" | "medium" | "high"
EmbeddingModel, EmbeddingEndpointUrl
IsActive, IsDefault
ExtraParams (jsonb)
```

Unique constraint: `(UserId, Name)`. Cascade delete when user deleted.

#### PlotThread
```
Id, GameId
Title, Description
Category: General | Faction | Mystery | Personal | Threat | WorldEvent | Relationship
Status: Active | Resolved | Abandoned
Momentum (float, -10 to +10)
RelevanceScore (float, 0-1)
NextMilestone, Foreshadowing
AdaptationHistory (List<string> jsonb)
MilestoneEvents (List<MilestoneEvent> jsonb)
IsDynamic, KeyEventMessageIds
Vector? Embedding (pgvector column)
ISoftDelete
```

#### Message
```
Id, SessionId, PlayerId
Content (string)
Type (MessageType enum — 100+ values)
Metadata (jsonb / JsonElement)
CreatedAt, IsOOC
WhisperFromId, WhisperToId, WhisperTarget
Vector? Embedding (pgvector column)
ISoftDelete
```

`MessageType` covers every game event: public/OOC chat, whispers, dice/skill/attack/spell, all combat events, player/character/session/game lifecycle events, GM narration, AI messages, tool calls, roll requests/confirmations, and more.

#### Combat + CombatParticipant
```
Combat: Id, GameId, SessionId, Name
        Status: Active | Paused | Finished
        CurrentRound, CurrentTurnIndex, InitiativeCount
        Notes (jsonb)
        → CombatParticipants (cascade), CombatEvents (cascade)

CombatParticipant: Id, CombatId, ParticipantType, CharacterId/NPCId/PlayerId
                   DisplayName, Initiative, InitiativeCount (tiebreak)
                   HP, MaxHP, AC
                   Conditions (jsonb), TemporaryHP (jsonb)
                   SavingThrows (jsonb), DeathSaveState (jsonb)
                   ActionsRemaining, BonusActionsRemaining
                   ReactionsRemaining, MovementsRemaining, FreeActions
```

`CombatEventType` enum: Start, End, TurnChange, Attack, Damage, Healing, Condition, SaveThrow, Initiative, Death, Revival, DeathSave, RoundStart

### 4.5 Event System

#### IGameEvent
All events implement `IGameEvent { Guid GameId }`.

#### GameEvents.cs — Full Event Catalog

**Game lifecycle**: `GameCreated`, `GameStarted`, `GameArchived`, `GamePaused`, `GameResumed`, `GameNarrationStarted`, `GMStatusChanged`, `GameStatusChanged`

**Player**: `PlayerJoined`, `PlayerLeft`, `PlayerDisconnected`, `PlayerReconnected`, `PlayerRoleChanged`

**Session**: `SessionCreated`, `SessionClosed`

**Chat**: `MessageSent`, `WhisperSent`, `OOCMessageSent`, `OOCWhisperSent`, `OOCWhisperReceived`

**Game actions**: `DiceRolled`, `SkillCheckRequested`, `AttackRequested`

**Combat** (16 events): `CombatStarted`, `CombatEnded`, `ParticipantAdded`, `ParticipantRemoved`, `InitiativeRolled`, `InitiativeRolledForAll`, `TurnAdvanced`, `TurnRetreated`, `CombatAttackExecuted`, `CombatSaveThrowExecuted`, `CombatSpellCast`, `CombatConditionApplied`, `CombatConditionRemoved`, `CombatDamageDealt`, `CombatHealed`, `CombatXPGranted`, `CombatLevelUp`, `CombatRestStarted`, `CombatRestEnded`, `CombatGridSet`, `CombatPositionSet`, `CombatMove`

**Character/NPC/Plot**: `CharacterUpdated`, `NPCCreated`, `NPCUpdated`, `NPCDeleted`, `PlotThreadCreated`, `PlotThreadUpdated`

**Story**: `StorySwayed`, `GMActioned`

**Saga internal events**: `AgentCallQueued`, `LLMDispatchRequested`, `LLMResponseReceived`, `ToolCallRequested`, `ToolCallCompleted`, `LLMFollowUpRequested`, `NarrativeReady`, `AgentCallFailed`, `ToolCallWaitingConfirmation`

#### EventBusWorker / IEventBus
`EventBusWorker` is a singleton `BackgroundService` implementing `IEventBus`. Its `PublishAsync<T>()` method delegates to Wolverine's `IMessageContext.PublishAsync()`. Wolverine handles all delivery, retry, and persistence in PostgreSQL.

#### HandlerRegistry
`HandlerRegistry` (singleton) scans the `Adnd.Server.Handlers` assembly at startup for all `IEventHandler<T>` implementations and builds a dictionary keyed by event type name. `GameAgent.DispatchToHandlers()` uses this registry to route events to their handlers.

### 4.6 Agent Framework & Saga

#### The Saga Path (step by step)

```
1. Hub method or Controller calls agentBus.SendCallAsync(AgentCall)
   └─ Saves AgentCall to DB (Status=Pending)
   └─ Publishes AgentCallQueued via Wolverine

2. SagaOrchestratorHandler handles AgentCallQueued
   └─ Sets AgentCall.CurrentStep = Init
   └─ Extracts system/user prompts from Input (GMDispatchOptions)
   └─ Optionally builds character context for Narrate/Generate actions
   └─ Emits LLMDispatchRequested

3. LLMDispatchHandler handles LLMDispatchRequested
   └─ Loads game's LLMPreset
   └─ Creates ILLMProvider via ILLMProviderFactory
   └─ Calls provider.CompleteWithToolsAsync()
   └─ Logs interaction to LLMInteractionLogs
   └─ Emits LLMResponseReceived (with HasToolCalls flag + tool count)

4a. If no tool calls → LLMResponseHandler emits NarrativeReady

4b. If tool calls → LLMResponseHandler emits ToolCallRequested for tool[0]
   └─ ToolExecutionHandler executes tool via GMToolRegistry
       ├─ If requiresConfirmation → emits ToolCallWaitingConfirmation
       │  └─ ToolCallWaitingConfirmationHandler broadcasts PlayerRollRequested via SignalR
       │  └─ Hub method ConfirmPlayerRoll/DeclinePlayerRoll resumes saga
       └─ Emits ToolCallCompleted
   └─ CoordinatorHandler receives ToolCallCompleted
       ├─ If more tools remain → emits ToolCallRequested for tool[n+1]
       └─ If all tools done → emits LLMFollowUpRequested
   └─ LLMFollowUpHandler composes tool results into follow-up prompt
       └─ Calls LLM again → emits NarrativeReady

5. NarrativeHandler handles NarrativeReady
   └─ Persists Message (type=GM) to DB
   └─ Broadcasts NewMessage to game's SignalR group
   └─ Emits GameNarrationStarted

6. GameLifecycleHandler handles GameNarrationStarted
   └─ If game.Status == Starting → transitions to Active
   └─ Broadcasts GameStatusChanged via SignalR
```

#### AgentSaga (Wolverine Saga)
```
State: Id (= AgentCall.Id = Saga.Id)
       AgentCallId, GameId
       ToolsRemaining, CurrentToolId, CurrentState

Start() → triggered by AgentCallQueued
         → schedules 5-minute timeout (ToolCallTimeout message)

Handle(LLMResponseReceived) → transitions state to "ExecuteTools" or "NarrativeReady"
Completed/failed events → mark saga done
```

#### GameAgent + GameAgentManager
`GameAgent` (per game instance):
- Holds `_gameId`, `_isPaused`, `_isConnected`
- `StartAsync()`: sets game status to Starting, recovers pending saga events
- `DispatchToHandlers()`: persists event as `EventRecord`, uses `HandlerRegistry` to invoke handlers via reflection
- `PauseAsync()`/`ResumeAsync()`: updates `Game.GMStatus`

`GameAgentManager` (singleton):
- `ConcurrentDictionary<Guid, GameAgent>` with double-checked locking on `GetOrCreate()`
- `StartAllActiveGamesAsync()`: on app startup, finds all Active+Running games and recovers their agents

#### AgentBus (IAgentBus)
The central dispatch service. Injected everywhere that needs to trigger AI work.

**Key methods:**
- `SendCallAsync(AgentCall)` — saves to DB + publishes `AgentCallQueued` via Wolverine
- `ExecuteCallAsync(AgentCall)` — direct synchronous execution (bypasses saga, used for testing)
- `GetGMStatusAsync(gameId)` / `PauseGMAsync()` / `ResumeGMAsync()` — GM lifecycle
- `SendSwayAsync(gameId, direction, intensity, content)` — story sway

**HandleGMCall() — the main AI GM path:**
- `ManageState`: updates `Game.GameState`, broadcasts `GMStatusChanged`
- `Narrate`/`Generate`: calls `provider.CompleteWithToolsAsync()`, handles tool calls iteratively up to `ToolCallingMaxDepth` (default 5), feeds results back to LLM for follow-up narrative
- `Nudge`: incorporates creator narrative direction into system prompt
- `GenerateInitialThreads`: LLM generates 2-4 JSON plot threads with retry logic and fallback seed-based generation
- `OpenNarrative`: iterative tool-calling loop for the game's opening narration

**GMDispatchOptions** (main input type):
```csharp
record GMDispatchOptions {
    string SystemPrompt { get; init; }
    string UserPrompt { get; init; }
    bool Stream { get; init; }
    int? MaxTokens { get; init; }
    bool IncludeTools { get; init; }
    // ... other LLM params
}
```

#### DeadLetterQueue
Tracks retry count per AgentCall.Id. After 3 failures, moves the call to DLQ and stops retrying.

### 4.7 LLM Provider System

#### ILLMProvider Interface
```csharp
interface ILLMProvider {
    string ProviderId { get; }
    string EndpointUrl { get; }
    TokenUsage GetTokenUsage();
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct);
    Task<T> CompleteStructuredAsync<T>(string systemPrompt, string userPrompt, JsonElement schema, LLMOptions opts, CancellationToken ct);
    Task<LLMToolCallResult> CompleteWithToolsAsync(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct);
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct);
    Task<bool> IsAvailableAsync(CancellationToken ct);
    Task<ProviderStatus> GetStatusAsync(CancellationToken ct);
}
```

#### LLMOptions
```csharp
record LLMOptions {
    string Model { get; init; }
    float Temperature { get; init; }        // default 0.7
    int MaxTokens { get; init; }            // default 2048
    float TopP { get; init; }               // default 0.9
    float FrequencyPenalty { get; init; }
    float PresencePenalty { get; init; }
    Dictionary<string, object> ExtraParams { get; init; }
    JsonSchemaOutput JsonSchemaOutput { get; init; }
}
```

#### Four Concrete Providers

Each provider exists in two forms:
1. A standalone registered class (for dev/test)
2. A `*FromPreset` class created at runtime from an `LLMPreset` row

| Provider | ProviderType | Notes |
|----------|-------------|-------|
| `OllamaLLMProvider` | `"ollama"` | Uses OllamaSharp; `format:json` for structured output |
| `LmStudioLLMProvider` | `"lmstudio"` | OpenAI-compatible HTTP; `json_schema` response format |
| `OpenAILLMProvider` | `"openai"` | Uses OpenAI SDK; native tool calling; `json_schema` structured output |
| `GoogleAIStudioLLMProvider` | `"google"` | Google AI Studio REST API; `responseMimeType: application/json`; `ReasoningEffort` maps to thinking budget |

#### BaseLLMProvider (abstract base)
Template method pattern:
- `CompleteAsync()` wraps `CompleteAsyncCore()` with logging + timing
- `CompleteWithToolsAsync()` wraps `CompleteWithToolsAsyncCore()`
- `CompleteWithToolsAsyncFallback()`: when provider doesn't support native tool calling, appends tool definitions as JSON to the system prompt and parses `{id, name, arguments}` arrays from text response
- `ParseToolCallsFromResponse()`: three-pass JSON extraction — direct parse → code fence regex → bracket scanning

#### LLMProviderFactory
```csharp
interface ILLMProviderFactory {
    ILLMProvider CreateFromPreset(LLMPreset preset);
}
```
Routes on `preset.ProviderType.ToLowerInvariant()`.

#### Strategy Pattern (Services/Llm/)
`IAdndLlmStrategy` decouples request formatting from provider wrappers:
- `OllamaStrategy`
- `OpenAIStrategy`
- `GoogleAIStudioStrategy`
- `OpenAICompatibleStrategy` (LM Studio, any OpenAI-compatible endpoint)

`AdndLlmStrategyFactory`: static dictionary mapping provider type strings to strategy instances.

#### JsonExtract Utility
Static class for extracting valid JSON from messy LLM output:
1. Try direct `JsonDocument.Parse()`
2. Regex for ` ```json ... ``` ` fences
3. Scan for first `{` or `[` bracket, try parse from there

#### ResiliencePolicies
Singleton Polly policies applied to all LLM calls:
- **Retry**: configurable delay (default 500ms) and max retries (default 3), with jitter
- **Circuit breaker**: opens after N failures (default 5), half-open duration (default 30s)
- Config from `Resilience__*` env vars

### 4.8 SignalR Hub

#### GameHub (partial class, split across 18 files)

**Base** (`GameHub.cs`):
- Static `ConcurrentDictionary<string, string> _playerConnections` (connectionId → userId)
- Periodic 60-second stale connection cleanup timer
- `OnConnectedAsync`: resolves player from `Context.UserIdentifier`, adds to `_playerConnections` + game SignalR group, emits `PlayerReconnected` or `PlayerJoined`
- `OnDisconnectedAsync`: reverse-lookup, removes from group, emits `PlayerDisconnected`
- `PublishAsync<T>()`: fire-and-forget event publishing via EventBus
- `PersistGameEventAsync()`: saves `Message` entity + generates pgvector embedding
- `ResolveGameSessionAsync()`: gets or creates the active session for a game

**Partial files and their responsibilities:**
| File | Methods |
|------|---------|
| `GameHub.ChatMethods.cs` | `SendMessage`, `SendWhisper`, `SendOOCMessage`, `SendOOCWhisper` |
| `GameHub.Combat.cs` | `StartCombat`, `EndCombat`, `AddParticipant`, `RemoveParticipant`, `RollInitiative`, `RollInitiativeForAll`, `NextTurn`, `PreviousTurn`, `DealDamage`, `HealParticipant`, `ApplyCondition`, `RemoveCondition`, `RecordDeathSave` |
| `GameHub.Dice.cs` | `RollDice`, `RollSkillCheck`, `RollAttack` |
| `GameHub.AICombat.cs` | AI tactical suggestions |
| `GameHub.AgentMethods.cs` | `TriggerNarrate`, `TriggerSuggest`, `GetGMStatus`, `PauseGM`, `ResumeGM` |
| `GameHub.CharacterCreation.cs` | Character creation flow |
| `GameHub.FlavorText.cs` | Flavor text generation |
| `GameHub.Grid.cs` | Combat grid setup/position |
| `GameHub.Inventory.cs` | Item add/remove/equip |
| `GameHub.JoinLeave.cs` | `JoinGameGroup`, `LeaveGameGroup` |
| `GameHub.Progression.cs` | XP grant, level up |
| `GameHub.RestSystem.cs` | Short rest, long rest |
| `GameHub.SAN.cs` | Call of Cthulhu sanity checks |
| `GameHub.Spells.cs` | Cast spell, use spell slot, rest recovery |
| `GameHub.SystemSpecific.cs` | System-specific rules |
| `GameHub.ToolCalls.cs` | `ConfirmPlayerRoll`, `DeclinePlayerRoll`, `ConfirmToolCall` |
| `GameHub.Whispers.cs` | Whisper history, GM whisper |
| `GameHub.HelperMethods.cs` | Shared internal helpers |

**SignalR events broadcast to clients (from server → client):**

| Event Name | Payload | When |
|-----------|---------|------|
| `NewMessage` | MessageDto | Any new game message |
| `GameNarration` | string | GM narration text |
| `GameStatusChanged` | GameStatusDto | Game status transitions |
| `GMStatusChanged` | GMStatusDto | GM idle/running/paused |
| `PlayerDisconnected` | PlayerDto | Player disconnect |
| `PlayerReconnected` | PlayerDto | Player reconnect |
| `CombatStarted` | CombatDto | Combat begins |
| `CombatEnded` | CombatDto | Combat ends |
| `TurnAdvanced` | CombatDto | Initiative order advance |
| `ParticipantAdded` | ParticipantDto | New combat participant |
| `ParticipantRemoved` | ParticipantDto | Participant removed |
| `CombatDamageDealt` | DamageDto | Damage applied |
| `CombatConditionApplied` | ConditionDto | Condition added |
| `CombatConditionRemoved` | ConditionDto | Condition removed |
| `PlayerRollRequested` | RollRequestDto | AI requests player to roll |

### 4.9 Services

#### GameEngine (IGameEngine)
Core game rules engine. System-aware via `ISystemRulesFactory`.

Methods: dice resolution, skill checks (with proficiency), attack rolls (with action logging), character queries, session management helpers.

#### DiceEngine (IDiceEngine)
Parses and evaluates dice formulas: `4d6kh3+2d4-1`
- `d` = dice notation (`NdM`)
- `kh` = keep highest N dice
- `kl` = keep lowest N dice
- `dh` = drop highest N dice
- `dl` = drop lowest N dice
- `+N` / `-N` = flat modifiers
- Returns: individual rolls, kept rolls, total, formula string

#### SystemRegistry (singleton)
Built-in RPG systems defined in static constructor:

| SystemId | Name | Attributes | Skills |
|---------|------|-----------|-------|
| `dnd5e` | D&D 5th Edition | STR/DEX/CON/INT/WIS/CHA | 18 standard skills |
| `pf2e` | Pathfinder 2e | STR/DEX/CON/INT/WIS/CHA | Proficiency-based skills |
| `coc7e` | Call of Cthulhu 7e | STR/CON/SIZ/DEX/APP/INT/POW/EDU | d100 skills + Sanity |

Custom systems stored per-game in `Game.CustomSystemJson`, deserialized to `CustomSystemDefinition`.

#### RAGService (IRAGService)
Retrieval-Augmented Generation for game context.

Methods:
- `GeneratePlotContextAsync(gameId)` — assembles recent messages + NPCs + active plot threads into a context string
- `FindSimilarPlotThreadsAsync(gameId, queryEmbedding, topK)` — pgvector cosine similarity search on `PlotThread.Embedding`
- `GenerateSessionSummaryAsync(gameId, sessionId)` — LLM-generated summary
- `CheckPlotConsistencyAsync(gameId)` — returns `ConsistencyReport`
- `SuggestContinuationAsync(gameId)` — returns `PlotContinuation`
- `EmbedMessagesAsync(gameId)` / `EmbedMessageAsync(messageId)` — batch/single embedding generation

In-memory embedding cache: `ConcurrentDictionary` with 60-minute TTL.

#### PlotWeaver (IPlotWeaver)
Orchestrates four strategy classes:
1. `PlotThreadGenerationStrategy` — LLM generates new plot threads
2. `PlotThreadAdaptationStrategy` — LLM adapts existing threads to recent events
3. `PlotMilestoneSpawningStrategy` — spawns milestone events for active threads
4. `PlotOpportunityDetectionStrategy` — detects story opportunities from recent context

Support methods: `HasInitialThreadsAsync()`, `HasSimilarThreadAsync()` (Jaccard-style dedup), `ArchiveOldThreadsAsync()` (archives resolved/abandoned threads >30 days old).

Triggered by `PlotWeaverHandler` every N messages (configurable).

#### AuthService (IAuthService)
- `RegisterAsync()`: BCrypt password hash, saves User, returns JWT
- `LoginAsync()`: BCrypt verify, generates JWT + refresh token, saves `RefreshToken`
- `RefreshAsync()`: validates refresh token, rotates (deletes old, issues new JWT + refresh token)
- `LogoutAsync()`: deletes refresh token
- JWT: HS256, 60-minute expiry (configurable), refresh token 30-day expiry

#### ApiKeyEncryptionService (IApiKeyEncryptionService)
- AES-256-GCM encryption using `Encryption__MasterKey` from environment
- `EncryptAsync(plaintext)` → base64-encoded `nonce + tag + ciphertext`
- `DecryptAsync(ciphertext)` → plaintext
- Called by `LLMPresetService` when storing/reading API keys

#### EmbeddingService (IEmbeddingService)
Generates vector embeddings using the game's configured LLM preset.
Falls back to a zero-vector if no embedding model is configured.

#### LLMInteractionLogger (ILLMInteractionLogger)
Logs every LLM call to `LLMInteractionLogs`:
- System prompt + user prompt (full text)
- Response text
- Token usage (prompt + completion + total)
- Duration (ms)
- LLM preset snapshot (name, endpoint, model at time of call)
- Game ID + session ID

#### GMToolRegistry (IGMToolRegistry)
Provides tool definitions (JSON Schema) to LLMs and executes tool calls.

**Available GM tools (partial list):**
| Tool Name | Category | Description |
|-----------|---------|-------------|
| `narrate` | narrative | Output narrative text |
| `rollDice` | dice | Roll dice with formula |
| `skillCheck` | dice | Request a skill check |
| `requestPlayerRoll` | dice | Ask a player to roll (requires confirmation) |
| `queryCharacter` | character | Get character sheet data |
| `queryNPCs` | narrative | Get NPC list and descriptions |
| `searchPlotContext` | rag | Semantic search of plot history |
| `updateGameState` | state | Update game state JSON |
| `sendWhisper` | communication | Send GM whisper to player |
| `startCombat` | combat | Initialize combat encounter |
| `addCombatParticipant` | combat | Add participant to active combat |
| Combat query tools | combat | Query combat state |

`RequiresConfirmation(toolName)` → `true` for `requestPlayerRoll` and similar player-interactive tools.

#### WhisperService (IWhisperService)
Private messaging routing:
- `WhisperType`: PlayerToGM, GMToPlayer, PlayerToPlayer, GMBroadcast, SystemMessage, NPCToPlayer, TableTalk

#### GameAuthorizationService (IGameAuthorizationService)
Enforces player roles:
- `Creator`: full admin access
- `Player`: can send messages, roll dice, manage own character
- `Spectator`: read-only
- `Observer`: read-only, not listed in player count

### 4.10 Controllers

All controllers use `LowerCaseControllerConvention`. Routes: `/api/{controller}/{action}`.

| Controller | Key Routes |
|-----------|-----------|
| `AuthController` | POST /api/auth/register, /login, /refresh, /logout; GET /me; POST /change-password; PUT /display-name |
| `GamesController` | Full CRUD + sessions + players + invite + join/leave/promote + start/archive |
| `AdminController` | NPCs, plot threads, characters, LLM presets, systems, GM agent, agent calls, whispers, tool calls, dice history, combat, spells, session notes, messages, prompt templates, game templates, RAG |
| `LLMPresetsController` | CRUD + test + set-default + list models |
| `LLMLogsController` | List/detail/delete interaction logs + usage stats + cleanup |
| `AgentFrameworkController` | Agent call CRUD + history |
| `CharactersController` | Character CRUD + sheet |
| `CombatLogController` | Combat history |
| `DiceHistoryController` | Dice roll history |
| `GMStatusController` | Status/pause/resume |
| `GMToolController` | List + execute tools |
| `GameStateController` | Read/update game state |
| `LLMController` | Provider status |
| `LLMTriggerController` | Manual trigger: narrate, suggest, consistency, review, new-scene, GM-evaluate, plot-check, detect-opportunities, generate-threads, spawn-milestones, session-summary |
| `NPCsController` | NPC CRUD |
| `PlotWeaverController` | PlotWeaver operations |
| `PlotsController` | Plot thread CRUD |
| `QuickWinsController` | Session notes, prompt templates, game templates |
| `SpellManagementController` | Character spell CRUD |
| `SwayController` | Story sway |
| `SystemsController` | RPG system list + custom system |
| `WhispersController` | Whisper history + send |

### 4.11 Security

| Concern | Implementation |
|---------|---------------|
| Authentication | JWT Bearer HS256, 60-min expiry |
| Session refresh | Refresh token rotation (30-day expiry, one token per session) |
| API key storage | AES-256-GCM encryption at rest (`Encryption__MasterKey`) |
| Password storage | BCrypt with work factor |
| Authorization | Role-based via `GameAuthorizationService` |
| Rate limiting | Global + per-endpoint limits via ASP.NET Core rate limiting middleware |
| CORS | AllowAll (intended for SPA; tighten in production) |
| Security headers | X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy |
| Soft delete | Global EF query filters prevent accidental data exposure |
| SQL injection | EF Core parameterized queries throughout |
| Input validation | Model validation on all DTOs |

---

## 5. Frontend: Adnd.Client

### 5.1 Technology Stack

| Concern | Package | Version |
|---------|---------|---------|
| Framework | React | 19.1.0 |
| Router | react-router-dom | 7.1.0 |
| UI components | @mui/material + @mui/icons-material | 7.1.0 |
| Styling | @emotion/react + @emotion/styled | latest |
| Real-time | @microsoft/signalr | 8.0.7 |
| Markdown | react-markdown + remark-gfm | 10.1.0 / 4.0.1 |
| Build | Vite + @vitejs/plugin-react | 6.1.0 |
| Language | TypeScript | 5.7.2 |

### 5.2 Build Configuration

**vite.config.ts:**
- Reads `VERSION` file, defines `__APP_VERSION__` global
- Build output: `../Adnd.Server/wwwroot` (co-located with server)
- Manual chunks: `vendor` (react/react-dom), `mui` (@mui/material), `signalr`
- Dev server: port 3000
- Dev proxy: `/api/**` → `http://localhost:5010`, `/gamehub` → `http://localhost:5010` (with WebSocket upgrade)

**index.html:** Standard Vite entry, loads `src/main.tsx`.

### 5.3 Routing & Layout

#### main.tsx
Defines `ColorModeContext` + `useColorMode()` hook. Theme persisted to `localStorage` key `"adnd-theme"`.

**Dark theme palette:**
- Primary: `#7c3aed` (purple)
- Background: `#0c0a0e`
- Paper: `#141019`

**Light theme:** Inverted/lighter equivalents.

Renders `<Root>` → `<ThemeProvider>` → `<App>`.

#### App.tsx
Wraps everything in `<AuthProvider>` + `<BrowserRouter>`.

**Route structure:**
```
/login, /register          → <AuthPage /> (no layout)
/* (with <AppShell> layout):
  /                        → <HomePage />
  /dashboard               → <DashboardPage />
  /llm-presets             → <LLMPresetsPage />
  /llm-presets/new         → <LLMPresetsPage /> (new mode)
  /systems                 → <SystemsPage />
  /systems/new             → <SystemsPage /> (new mode)
  /user-settings           → <UserSettingsPage />
  /game/:id                → <GameChatPage />
  /game/:id/settings       → <GameSettingsPage />
  /admin/:id               → <AdminDashboardPage />
  /admin/:id/plot-board    → <AdminPlotBoardPage />
  /admin/:id/npcs          → <AdminNPCsPage />
  /admin/:id/characters    → <AdminCharactersPage />
  /admin/:id/consistency   → <AdminConsistencyPage />
  /admin/:id/llm-logs      → <AdminLLMLogsPage />
  /admin/:id/agent-calls   → <AdminAgentCallsPage />
  /character/:id           → <CharacterSheetPage />
  *                        → redirect to /
```

#### AppShell.tsx
Layout wrapper. Handles responsive sidebar (open state in `localStorage`). Renders `<SidePanel>` + `<Outlet>`.

Tracks `currentView` (AppView type) and `gameId` from URL. Handles:
- Join game dialog
- Navigation callbacks passed to SidePanel
- Authenticated vs unauthenticated display

#### SidePanel.tsx
Collapsible sidebar:
- Hamburger toggle
- Version pill + ALPHA badge
- User display name + logout
- Main nav: Games / LLM Presets / Systems / User Settings
- Context-sensitive sub-nav based on `currentView`: dashboard game list, preset list, in-game links (Chat / Admin Dashboard / Plot Board / etc.), admin sub-links
- Fixed bottom: dark/light theme toggle button

### 5.4 API Client

**src/api/client.ts** exports a singleton `api` (instance of `APIClient`).

JWT stored in `localStorage`. `request<T>()` private method:
- Attaches `Authorization: Bearer <token>` header
- On 401: attempts token refresh, retries once
- On 429: throws rate limit error
- On non-OK: throws with response body

**Covered API surface:**
- Auth: register, login, logout, getMe, changePassword, updateDisplayName
- Games: full CRUD + sessions + players + invite + join/joinByCode/leave + promote + start/archive + GM status/pause/resume/sway + pause/resume events + trigger combat start/end + whispers
- Admin: NPCs, plot threads, characters, game state, pending events, plot context, RAG endpoints, systems
- LLM: providers, preset CRUD, test, set-default, list models
- LLM Logs: list/detail/delete + usage stats by preset/game/provider + cleanup
- Agent: history, get/create/pending calls
- Manual LLM triggers: narrate, suggest, consistency, review, full-review, new-scene, GM-evaluate, plot-check, detect-opportunities, generate-threads, spawn-milestones, session-summary
- Game templates: CRUD
- GM tools: list by category, execute
- Tool calls: pending, confirm, confirm/decline player roll
- Dice: history, stats, player stats
- Combat: list, detail
- Spells: get/update/single update/remove
- Session notes: CRUD
- Messages: paginated, search
- Prompt templates: CRUD, get default

### 5.5 SignalR Hook

**src/api/hooks/useHub.ts** — `useGameHub()` hook:

```typescript
// Creates HubConnection with:
//   - JWT token from localStorage via accessTokenFactory
//   - withAutomaticReconnect()
//   - LogLevel.None

// Returns:
{
  isConnected: boolean,
  error: Error | null,
  connect(gameId: string, token: string): Promise<void>,  // joins game group
  disconnect(): Promise<void>,
  on(event: string, handler: (...args: any[]) => void): void,
  off(event: string, handler?): void,
  invoke(method: string, ...args: any[]): Promise<any>,
  waitForConnection(): Promise<void>,
  // ...spread of typed HubMethods from createHubMethods()
}
```

`connect()` calls `invoke("JoinGameGroup", gameId)` after establishing connection.

### 5.6 Pages

#### GameChatPage (/game/:id)
The main in-game interface. Three vertical zones:

**Zone A — CollapsibleCombatPanel**
Shows active combat when present:
- Initiative order list with HP bars, AC, action economy chips
- `ActionEconomyTracker`: actions/bonus actions/reactions/movement remaining
- `DeathSaveTracker`: success/failure tracking
- Conditions list
- Click participant → opens `CharacterSheetPopup`

**Zone B — ChatPanel**
- Paginated message feed with infinite scroll backwards (loads older pages on scroll-up)
- Deduplicates live (SignalR) vs paginated messages by message ID
- Renders each message with type-aware formatting (GM narration, dice rolls, combat events, whispers, OOC)
- `react-markdown` + `remark-gfm` for GM narrative text

**Zone C — ChatInput**
- Single-line input
- In-Game / OOC toggle button
- Receiver dropdown: All / To GM / To Player N
- Player whisper target chips

**SignalR subscriptions in GameChatPage:**
`CombatStarted`, `CombatEnded`, `CombatDamageDealt`, `CombatConditionApplied`, `CombatConditionRemoved`, `TurnAdvanced`, `ParticipantAdded`, `ParticipantRemoved`, `NewMessage`, `GameNarration`, `PlayerDisconnected`, `PlayerReconnected`

`ToolCallBanner` renders pending tool call notifications (e.g., "AI is waiting for player to roll").

#### DashboardPage (/dashboard)
- Game list with status chips
- Create game dialog: LLM preset selector, plot seed textarea, RPG system dropdown, language selector
- Join by invite code input

#### AdminDashboardPage (/admin/:id)
- Game header: name, status chips, quick nav buttons (to Plot Board, NPCs, Characters, Consistency, LLM Logs, Agent Calls)
- Stat cards grid: Players, Plot Threads, Characters, NPCs, Agent Calls, LLM Logs, Pending Events
- State controls: Start Game, Archive Game, Pause/Resume GM, Send Pause/Resume Events
- Game details panel: ID, dates, language, LLM preset, invite code

#### AdminLLMLogsPage (/admin/:id/llm-logs)
- Filterable table of LLM interaction logs
- Expandable rows with full prompt/response text
- Token usage, duration, model/endpoint info
- Bulk delete (by game or date range)

#### LLMPresetsPage (/llm-presets)
- List of user's presets with default/active indicators
- Create/edit form: provider type, model name, endpoint URL, API key (masked), temperature, max tokens, top-p, frequency/presence penalty, stream toggle, timeout, reasoning effort, embedding model/endpoint
- Test connection button

#### CharacterSheetPage (/character/:id)
Full character sheet with tabs:
- **Stats**: attributes, saving throws, proficiency, HP/AC
- **Skills**: skill list with proficiency checkboxes
- **Inventory**: item list with equip/unequip
- **Spells**: spell slot tracker + spell list per level
- **Background**: background story, traits, bonds, flaws
- **Custom**: free-form custom fields

#### CharacterCreateWizard
Multi-step (8 character backgrounds): Acolyte, Criminal, Soldier, Sage, Gladiator, Folk Hero, Urchin, Noble. Each background pre-fills skills, proficiencies, and features.

### 5.7 Theme System

Dark/light mode via MUI `createTheme`. Mode stored in `localStorage("adnd-theme")`. `ColorModeContext` provides `toggleColorMode()`. SidePanel bottom button toggles mode. `ThemeProvider` wraps the entire app.

---

## 6. Complete Data Flow

### Player Sends a Message → GM Responds

```
1. Player types in ChatInput, clicks Send
   └─ hub.invoke("SendMessage", gameId, sessionId, content, isOOC, receiverType)

2. GameHub.ChatMethods.SendMessage()
   └─ PersistGameEventAsync(MessageSent event) → saves Message to DB + embeds
   └─ Broadcasts NewMessage to game group via SignalR
   └─ PublishAsync(MessageSent event) via EventBus → Wolverine

3. ChatHandler.HandleAsync(MessageSent)   [currently logging only]
4. PlotWeaverHandler.HandleAsync(MessageSent)
   └─ Increments per-game message counter
   └─ Every N messages: triggers PlotWeaver to adapt threads
5. GameActionHandler.HandleAsync(MessageSent)  [for action events only]
   └─ QueueGMMaybe(): checks GMStatus + pending call count
   └─ agentBus.SendCallAsync(new AgentCall { Action=Narrate, ... })

6. AgentBus.SendCallAsync()
   └─ Saves AgentCall (Status=Pending)
   └─ Wolverine.PublishAsync(AgentCallQueued)

7-12. Saga executes (steps 2-6 from Section 4.6)

13. NarrativeHandler saves Message (Type=GM) + broadcasts NewMessage

14. Client receives NewMessage via SignalR
    └─ ChatPanel appends GM narrative to feed
    └─ react-markdown renders the narrative text
```

### Player Joins a Game

```
1. POST /api/games/{id}/join (or join by invite code)
2. DashboardPage: navigate to /game/:id
3. GameChatPage mounts, calls hub.connect(gameId, token)
4. useHub: SignalR connection opens → calls JoinGameGroup(gameId)
5. GameHub.OnConnectedAsync():
   └─ Adds connection to _playerConnections
   └─ hub.Groups.AddToGroupAsync(connectionId, gameId)
   └─ PublishAsync(PlayerReconnected or PlayerJoined)
6. PlayerDisconnectHandler broadcasts PlayerReconnected to game group
7. Other clients receive PlayerReconnected, update their player lists
```

---

## 7. RPG Systems

### Built-in Systems

#### D&D 5th Edition (dnd5e)
- Attributes: STR, DEX, CON, INT, WIS, CHA
- Skills: Acrobatics (DEX), Animal Handling (WIS), Arcana (INT), Athletics (STR), Deception (CHA), History (INT), Insight (WIS), Intimidation (CHA), Investigation (INT), Medicine (WIS), Nature (INT), Perception (WIS), Performance (CHA), Persuasion (CHA), Religion (INT), Sleight of Hand (DEX), Stealth (DEX), Survival (WIS)
- Dice: d20 + modifier vs DC
- Death saves: 3 successes = stable, 3 failures = dead

#### Pathfinder 2e (pf2e)
- Attributes: STR, DEX, CON, INT, WIS, CHA
- Proficiency levels: Untrained, Trained, Expert, Master, Legendary
- Skills: Acrobatics, Arcana, Athletics, Crafting, Deception, Diplomacy, Intimidation, Lore (any), Medicine, Nature, Occultism, Performance, Religion, Society, Stealth, Survival, Thievery
- Dice: d20 + proficiency + modifier vs DC

#### Call of Cthulhu 7e (coc7e)
- Attributes: STR, CON, SIZ, DEX, APP, INT, POW, EDU
- Skills: Accounting, Anthropology, Appraise, Archaeology, Art (any), Charm, Climb, Computer Use, Credit Rating, Cthulhu Mythos, Disguise, Dodge, Drive Auto, Electrical Repair, Fast Talk, Fighting (Brawl), Firearms (Handgun/Rifle), First Aid, History, Intimidate, Jump, Language (any), Law, Library Use, Listen, Locksmith, Mechanical Repair, Medicine, Natural World, Navigate, Occult, Operate Heavy Machinery, Persuade, Pilot (any), Psychology, Psychoanalysis, Ride, Science (any), Sleight of Hand, Spot Hidden, Stealth, Swim, Throw, Track
- Special: Sanity (max 99 - Cthulhu Mythos skill)
- Dice: d100 (roll-under), success levels: Extreme (⅕ skill), Hard (½ skill), Regular, Fail, Fumble

### Custom Systems
Stored as `CustomSystemDefinition` JSON in `Game.CustomSystemJson`. Created via `/api/systems/custom` endpoint. Structure:
```json
{
  "id": "string",
  "name": "string",
  "version": "string",
  "attributes": [{ "id": "STR", "name": "Strength", "abbreviation": "STR" }],
  "skills": [{ "id": "athletics", "name": "Athletics", "attribute": "STR" }],
  "diceType": "d20 | d100 | other",
  "description": "string"
}
```

---

## 8. Plot Intelligence (RAG + PlotWeaver)

### pgvector Setup
- PostgreSQL extension: `pgvector` (via `pgvector/pgvector:pg17` image)
- EF Core: `UseVector()` on `NpgsqlDbContextOptionsBuilder`
- Columns: `PlotThread.Embedding` and `Message.Embedding` typed as `Vector`

### RAGService
Assembles game context from three sources:
1. Recent messages (last N from DB)
2. Active NPCs (name + description)
3. Active plot threads (title + description + momentum)

Combines into a context string passed to LLM system prompts.

`FindSimilarPlotThreadsAsync()` uses pgvector cosine similarity:
```sql
SELECT * FROM "PlotThreads"
ORDER BY "Embedding" <=> @queryVector
LIMIT @topK
```

### PlotWeaver Strategies

**PlotThreadGenerationStrategy**: Called when a game has no threads or when major events occur. Sends recent context to LLM, requests JSON array of plot thread objects.

**PlotThreadAdaptationStrategy**: Sends current thread state + recent events to LLM, receives updated momentum/milestone/adaptation history. Updates `Thread.Momentum`, `Thread.AdaptationHistory`.

**PlotMilestoneSpawningStrategy**: For threads with high momentum, asks LLM to spawn `MilestoneEvent` objects (status: Pending/Triggered/Completed/Abandoned).

**PlotOpportunityDetectionStrategy**: Scans recent messages for story opportunities, returns `StoryOpportunityResponse` list.

### PlotWeaverHandler Trigger
Triggered by `MessageSent` events. Per-game counter (in memory); every N messages, calls `IPlotWeaver.ReviewAndAdaptAsync()`.

---

## 9. Combat System

### Architecture
`ICombatService` is a facade over 12 domain-split services:

| Service | Responsibility |
|---------|---------------|
| `ICombatLifecycleService` | Start/end combat, status transitions |
| `ICombatParticipantService` | Add/remove participants |
| `ICombatInitiativeService` | Roll initiative, sort order |
| `ICombatTurnService` | Advance/retreat turns, action economy reset |
| `ICombatStateService` | Combat state queries |
| `ICombatSpellService` | Cast spells, consume spell slots |
| `ICombatInventoryService` | Use items from inventory during combat |
| `ICombatProgressionService` | Grant XP, level up |
| `ICombatGridService` | Set grid, set positions, move |
| `ICombatAIService` | AI tactical suggestions |
| `ICombatQueryService` | Query combat state |
| `ISANService` | Call of Cthulhu sanity checks |

### Action Economy
Per participant, per turn:
- `ActionsRemaining` (default 1 per turn)
- `BonusActionsRemaining` (default 1 per turn)
- `ReactionsRemaining` (default 1 per round)
- `MovementsRemaining` (in feet)
- `FreeActions` (tracked separately)

### Death Saves (D&D 5e)
`DeathSaveState` (jsonb on `CombatParticipant`):
- 3 successes → stable
- 3 failures → dead
- Natural 20 → regain 1 HP
- Natural 1 → counts as 2 failures

### Sanity (CoC 7e)
`ISANService` handles sanity loss rolls, madness thresholds, sanity recovery, indefinite insanity tracking.

### Combat Events
All combat events logged to `CombatEvent` table with: round, turn, event type, actor, target, content, metadata.

### AI Combat Suggestions
`ICombatAIService.GetSuggestionsAsync()` builds combat state context, calls LLM, returns `AICombatSuggestion` list with tactical recommendations for each participant's turn.

---

## 10. Configuration Reference

### Environment Variables

| Variable | Required | Description |
|----------|---------|-------------|
| `ConnectionStrings__Default` | Yes | Npgsql connection string |
| `JwtSettings__SecretKey` | Yes | JWT signing key (min 32 chars) |
| `JwtSettings__Issuer` | Yes | JWT issuer string |
| `JwtSettings__Audience` | Yes | JWT audience string |
| `Encryption__MasterKey` | Yes | AES-256-GCM key for API key encryption (32 bytes base64) |
| `Resilience__RetryDelayMs` | No | LLM retry delay in ms (default: 500) |
| `Resilience__MaxRetries` | No | Max LLM retries (default: 3) |
| `Resilience__FailureThreshold` | No | Circuit breaker open threshold (default: 5) |
| `Resilience__HalfOpenDurationSeconds` | No | Circuit breaker half-open window (default: 30) |
| `RateLimiting__GlobalPermitLimit` | No | Global rate limit |
| `RateLimiting__AuthPermitLimit` | No | Auth endpoint rate limit |
| `RateLimiting__LLMPresetPermitLimit` | No | LLM preset endpoint rate limit |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore` | No | EF Core log level (default: Warning) |
| `APP_PORT` | No | Host port mapping (default: 5010) |
| `POSTGRES_PASSWORD` | Yes | PostgreSQL password |

### appsettings.json (development defaults)
```json
{
  "JwtSettings": {
    "SecretKey": "CHANGE_ME",
    "Issuer": "CHANGE_ME",
    "Audience": "CHANGE_ME"
  },
  "ConnectionStrings": {
    "Default": "..."
  },
  "Encryption": {
    "MasterKey": "CHANGE_ME"
  },
  "Resilience": {
    "RetryDelayMs": 500,
    "MaxRetries": 3,
    "FailureThreshold": 5,
    "HalfOpenDurationSeconds": 30
  }
}
```

### LLM Provider Configuration (via LLMPreset DB rows)

| Provider | Required Fields | Optional Fields |
|----------|----------------|----------------|
| `ollama` | BaseModel, EndpointUrl (e.g., http://host:11434) | Temperature, MaxTokens, EmbeddingModel |
| `lmstudio` | BaseModel, EndpointUrl (e.g., http://host:1234/v1) | Temperature, MaxTokens, ApiKey |
| `openai` | BaseModel (e.g., gpt-4o), ApiKey | Temperature, MaxTokens, ReasoningEffort |
| `google` | BaseModel (e.g., gemini-2.0-flash), ApiKey | Temperature, MaxTokens, ReasoningEffort |

`ReasoningEffort`: `"none"` | `"low"` | `"medium"` | `"high"` (maps to Google's thinking budget / OpenAI's reasoning effort).

---

## 11. Database Schema Summary

### Core Tables

```sql
-- Users
Users(Id uuid PK, Email varchar UNIQUE, PasswordHash varchar, DisplayName varchar, CreatedAt, LastLoginAt)

-- Games
Games(Id uuid PK, CreatorId FK→Users, LLMPresetId FK→LLMPresets NULL, CurrentSessionId FK→GameSessions NULL,
      Name, Status, GMStatus, LastGMAction, LastGMActionAt,
      SystemId, SystemVersion, CustomSystemJson,
      Language, PlotSeed, GameParameters, GameState,
      invitecode varchar UNIQUE PARTIAL (WHERE NOT NULL),
      IsDeleted, DeletedAt, CreatedAt, UpdatedAt)

-- Players  
Players(Id uuid PK, GameId FK→Games, UserId FK→Users, CharacterName, Role, Status, IsConnected,
        IsDeleted, DeletedAt)
  UNIQUE(GameId, UserId)

-- Characters
Characters(Id uuid PK, PlayerId FK→Players UNIQUE,
           Name, Class, Level, ProficiencyBonus, CurrentHP, MaxHP,
           Attributes jsonb, Skills jsonb, Inventory jsonb, Spells jsonb,
           Conditions jsonb, CustomFields jsonb,
           Background, BackgroundSkills, BackgroundProficiencies, BackgroundFeatures,
           SpellSlots jsonb, SpellcastingAbility, SpellSaveDC, SpellAttackBonus,
           IsDeleted, DeletedAt)

-- GameSessions
GameSessions(Id uuid PK, GameId FK→Games, Title, Description, Status, CreatedAt, ClosedAt)

-- Messages
Messages(Id uuid PK, SessionId FK→GameSessions, PlayerId FK→Players NULL,
         Content, Type varchar, Metadata jsonb,
         IsOOC, WhisperFromId, WhisperToId, WhisperTarget,
         Embedding vector,
         IsDeleted, DeletedAt, CreatedAt)
  INDEX(SessionId, CreatedAt DESC)

-- NPCs
NPCs(Id uuid PK, GameId FK→Games, Name, Description,
     Attributes jsonb, Skills jsonb, Inventory jsonb,
     IsDeleted, DeletedAt)

-- PlotThreads
PlotThreads(Id uuid PK, GameId FK→Games, Title, Description,
            Category, Status, Momentum real, RelevanceScore real,
            NextMilestone, Foreshadowing,
            AdaptationHistory jsonb, MilestoneEvents jsonb,
            IsDynamic, KeyEventMessageIds,
            Embedding vector,
            IsDeleted, DeletedAt)

-- LLMPresets
LLMPresets(Id uuid PK, UserId FK→Users,
           Name, ProviderType, BaseModel, EndpointUrl, ApiKey,
           Temperature real, MaxTokens int, TopP real,
           FrequencyPenalty real, PresencePenalty real, Stream bool,
           TimeoutMs int NULL, ReasoningEffort varchar,
           EmbeddingModel, EmbeddingEndpointUrl,
           IsActive, IsDefault, ExtraParams jsonb,
           CreatedAt, UpdatedAt)
  UNIQUE(UserId, Name)

-- AgentCalls
AgentCalls(Id uuid PK, GameId FK, SessionId FK NULL,
           FromAgent, ToAgent, Action, Input text, Output text, OutputMessage,
           Status, Error, ParentCallId FK→AgentCalls NULL,
           CurrentStep int, Metadata jsonb, DurationMs,
           CreatedAt, UpdatedAt)
  INDEX(GameId, Status, CreatedAt DESC)

-- ToolCallCoordinators
ToolCallCoordinators(Id uuid PK, AgentCallId FK→AgentCalls,
                     TotalTools int, CompletedTools int, CurrentToolIndex int,
                     ToolResults jsonb)

-- GMToolCalls
GMToolCalls(Id uuid PK, GameId FK, SessionId FK,
            ToolName, Arguments jsonb, Result jsonb, Status,
            StartedAt, CompletedAt)

-- Combats
Combats(Id uuid PK, GameId FK, SessionId FK, Name, Status,
        CurrentRound, CurrentTurnIndex, InitiativeCount, Notes jsonb,
        CreatedAt, UpdatedAt)

-- CombatParticipants
CombatParticipants(Id uuid PK, CombatId FK→Combats CASCADE,
                   ParticipantType, CharacterId NULL, NPCId NULL, PlayerId NULL,
                   DisplayName, Initiative real, InitiativeCount real,
                   HP int, MaxHP int, AC int,
                   Conditions jsonb, TemporaryHP jsonb, SavingThrows jsonb, DeathSaveState jsonb,
                   ActionsRemaining, BonusActionsRemaining,
                   ReactionsRemaining, MovementsRemaining, FreeActions)

-- CombatEvents
CombatEvents(Id uuid PK, CombatId FK→Combats CASCADE,
             Round, Turn, EventType, ActorId NULL, TargetId NULL,
             Content, Metadata jsonb, CreatedAt)

-- LLMInteractionLogs
LLMInteractionLogs(Id uuid PK, UserId FK, OriginGameId FK NULL,
                   SystemPrompt, UserPrompt, Response,
                   PromptTokens, CompletionTokens, TotalTokens,
                   DurationMs, PresetName, EndpointUrl, Model,
                   StartedAt)
  INDEX(UserId, OriginGameId, StartedAt)

-- RefreshTokens
RefreshTokens(Id uuid PK, UserId FK→Users, Token varchar UNIQUE,
              ExpiresAt, CreatedAt, IsRevoked)

-- SessionNotes
SessionNotes(Id uuid PK, GameId FK, SessionId FK NULL,
             Title, Content, CreatedAt, UpdatedAt)

-- PromptTemplates
PromptTemplates(Id uuid PK, GameId FK NULL, Name, Type, Content, IsDefault, CreatedAt)

-- GameTemplates
GameTemplates(Id uuid PK, UserId FK, LLMPresetId FK NULL,
              Name, Description, SystemId, Language, PlotSeed,
              GameParameters, CreatedAt, UpdatedAt)

-- PlotReviews
PlotReviews(Id uuid PK, GameId FK, Updates jsonb, CreatedAt)

-- EventRecords
EventRecords(Id uuid PK, GameId FK, EventType, Payload jsonb,
             CorrelationId uuid UNIQUE, Status, CreatedAt, ProcessedAt)
  INDEX(GameId, Status)

-- Whispers
Whispers(Id uuid PK, SessionId FK, FromPlayerId FK NULL, Type,
         Content, TargetPlayerIds jsonb, CreatedAt)

-- AuditLogs
AuditLogs(Id uuid PK, UserId FK NULL, Action, EntityType, EntityId, Details jsonb, CreatedAt)

-- CustomSystems
CustomSystems(Id uuid PK, GameId FK, Definition jsonb, CreatedAt)
```

### Wolverine Message Store Tables
Wolverine creates its own tables in the `public` schema on startup:
- `wolverine_incoming_envelopes`
- `wolverine_outgoing_envelopes`
- `wolverine_dead_letters`
- `wolverine_node_records`
- `wolverine_node_assignments`
- Saga state table for `AgentSaga`

---

## 12. Recreating From Scratch

### Prerequisites
- Docker + Docker Compose (v2+)
- Node.js 20+ (for local frontend dev)
- .NET 10 SDK (for local backend dev)

### Step 1: Environment Setup

Create `.env` in the project root:
```env
POSTGRES_PASSWORD=your_secure_password
APP_PORT=5010
JWT_SECRET_KEY=your_jwt_secret_minimum_32_characters_long
JWT_ISSUER=adnd-server
JWT_AUDIENCE=adnd-client
ENCRYPTION_MASTER_KEY=base64_encoded_32_byte_key
```

Generate `ENCRYPTION_MASTER_KEY`:
```bash
openssl rand -base64 32
```

### Step 2: Run with Docker Compose

```bash
docker compose up --build
```

This will:
1. Start PostgreSQL with pgvector
2. Build the .NET server (which also builds the Vite client inside Docker)
3. Serve the app at `http://localhost:5010`

EF Core migrations run automatically on startup via `MigrationService`.

### Step 3: First-Time Setup

1. Open `http://localhost:5010`
2. Register an account at `/register`
3. Go to `/llm-presets` and create an LLM preset (Ollama, LM Studio, OpenAI, or Google)
4. Go to `/dashboard` and create a game, selecting your LLM preset

### Step 4: Local Development (hot-reload)

**Backend:**
```bash
cd src/Adnd.Server
dotnet run
# Starts on http://localhost:5010
```

**Frontend (separate terminal):**
```bash
cd src/Adnd.Client
npm install
npm run dev
# Starts on http://localhost:3000
# Proxies /api and /gamehub to localhost:5010
```

### Project File Structure

```
adnd/
├── docker-compose.yml
├── AGENTS.md                    # AI assistant instructions
├── VERSION                      # Version string (read by Vite)
├── src/
│   ├── Adnd.Server/
│   │   ├── Adnd.Server.csproj
│   │   ├── Program.cs           # Entry point + DI + middleware
│   │   ├── Dockerfile
│   │   ├── appsettings.json
│   │   ├── Agent/
│   │   │   └── GameAgent.cs     # GameAgent + GameAgentManager
│   │   ├── Controllers/         # 20+ thin REST controllers
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── MigrationService.cs
│   │   │   ├── GameExtensions.cs
│   │   │   └── Migrations/      # EF Core migrations
│   │   ├── Events/
│   │   │   ├── GameEvents.cs    # All 40+ event records
│   │   │   ├── IGameEvent.cs
│   │   │   └── IEventHandler.cs
│   │   ├── Handlers/            # IEventHandler<T> implementations
│   │   │   ├── SagaOrchestratorHandler.cs
│   │   │   ├── LLMDispatchHandler.cs
│   │   │   ├── LLMResponseHandler.cs
│   │   │   ├── ToolExecutionHandler.cs
│   │   │   ├── CoordinatorHandler.cs
│   │   │   ├── LLMFollowUpHandler.cs
│   │   │   ├── NarrativeHandler.cs
│   │   │   ├── PlotWeaverHandler.cs
│   │   │   ├── AgentCallFailedHandler.cs
│   │   │   ├── ToolCallWaitingConfirmationHandler.cs
│   │   │   └── GameEventHandlers.cs  # 5 handler classes
│   │   ├── Hubs/
│   │   │   ├── GameHub.cs       # Base hub + connection tracking
│   │   │   ├── GameHub.ChatMethods.cs
│   │   │   ├── GameHub.Combat.cs
│   │   │   ├── GameHub.Dice.cs
│   │   │   ├── GameHub.AICombat.cs
│   │   │   ├── GameHub.AgentMethods.cs
│   │   │   ├── GameHub.CharacterCreation.cs
│   │   │   ├── GameHub.FlavorText.cs
│   │   │   ├── GameHub.Grid.cs
│   │   │   ├── GameHub.Inventory.cs
│   │   │   ├── GameHub.JoinLeave.cs
│   │   │   ├── GameHub.Progression.cs
│   │   │   ├── GameHub.RestSystem.cs
│   │   │   ├── GameHub.SAN.cs
│   │   │   ├── GameHub.Spells.cs
│   │   │   ├── GameHub.SystemSpecific.cs
│   │   │   ├── GameHub.ToolCalls.cs
│   │   │   ├── GameHub.Whispers.cs
│   │   │   └── ResponseDTOs.cs
│   │   ├── Models/              # EF Core entity classes
│   │   ├── Services/
│   │   │   ├── AgentBus.cs      # IAgentBus — central AI dispatch
│   │   │   ├── AgentSaga.cs     # Wolverine saga
│   │   │   ├── EventBusWorker.cs
│   │   │   ├── GameAgent.cs
│   │   │   ├── LLMProvider.cs   # ILLMProvider + BaseLLMProvider
│   │   │   ├── ILLMProviderFactory.cs
│   │   │   ├── GMToolRegistry.cs
│   │   │   ├── RAGService.cs
│   │   │   ├── PlotWeaver.cs
│   │   │   ├── SystemRegistry.cs
│   │   │   ├── GameEngine.cs
│   │   │   ├── DiceEngine.cs
│   │   │   ├── AuthService.cs
│   │   │   ├── CombatService.cs
│   │   │   ├── EmbeddingService.cs
│   │   │   ├── LLMPresetService.cs
│   │   │   ├── LLMInteractionLogger.cs
│   │   │   ├── WhisperService.cs
│   │   │   ├── ResiliencePolicies.cs
│   │   │   ├── ApiKeyEncryptionService.cs
│   │   │   ├── HandlerRegistry.cs
│   │   │   ├── DeadLetterQueue.cs
│   │   │   ├── GameAuthorizationService.cs
│   │   │   ├── UserIdProvider.cs
│   │   │   └── Llm/             # Strategy pattern for providers
│   │   │       ├── IAdndLlmStrategy.cs
│   │   │       ├── AdndLlmStrategyFactory.cs
│   │   │       ├── LlmStrategyTypes.cs
│   │   │       ├── OllamaStrategy.cs
│   │   │       ├── OpenAIStrategy.cs
│   │   │       ├── GoogleAIStudioStrategy.cs
│   │   │       └── OpenAICompatibleStrategy.cs
│   │   └── wwwroot/             # Built Vite output (gitignored)
│   └── Adnd.Client/
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── index.html
│       └── src/
│           ├── main.tsx         # Theme + ColorModeContext + render
│           ├── App.tsx          # Router + AuthProvider
│           ├── api/
│           │   ├── client.ts    # APIClient singleton
│           │   └── hooks/
│           │       ├── useHub.ts        # useGameHub()
│           │       ├── useGame.ts
│           │       ├── usePlayers.ts
│           │       ├── usePlotWeaver.ts
│           │       ├── useCharacters.ts
│           │       ├── useNPCs.ts
│           │       ├── useToolCalls.ts
│           │       └── useMessagesInfiniteScroll.ts
│           ├── components/
│           │   ├── AppShell.tsx
│           │   ├── SidePanel.tsx
│           │   └── [other shared components]
│           ├── pages/
│           │   ├── AuthPage.tsx
│           │   ├── DashboardPage.tsx
│           │   ├── GameChatPage.tsx
│           │   ├── AdminDashboardPage.tsx
│           │   ├── AdminLLMLogsPage.tsx
│           │   ├── AdminAgentCallsPage.tsx
│           │   ├── AdminPlotBoardPage.tsx
│           │   ├── AdminNPCsPage.tsx
│           │   ├── AdminCharactersPage.tsx
│           │   ├── AdminConsistencyPage.tsx
│           │   ├── CharacterSheetPage.tsx
│           │   ├── CharacterCreateWizard.tsx
│           │   ├── LLMPresetsPage.tsx
│           │   ├── GameSettingsPage.tsx
│           │   ├── SystemsPage.tsx
│           │   └── UserSettingsPage.tsx
│           └── types/
│               ├── auth.types.ts
│               ├── game.types.ts
│               ├── agent.types.ts
│               ├── combat.types.ts
│               ├── gm.types.ts
│               ├── llm.types.ts
│               ├── message.types.ts
│               ├── plot.types.ts
│               ├── template.types.ts
│               └── index.ts
```

### Key Invariants to Preserve

1. **EF Core only for migrations.** Never write raw SQL DDL. Use `dotnet ef migrations add <Name>` and `dotnet ef database update`.

2. **Wolverine for all async inter-handler communication.** Do not re-introduce RabbitMQ, MediatR, or manual channels. Wolverine persists all messages in PostgreSQL.

3. **LLMPreset is the source of truth for all LLM configuration.** Never hardcode model names or endpoints. All LLM calls go through `ILLMProviderFactory.CreateFromPreset()`.

4. **API keys are always encrypted at rest.** `LLMPreset.ApiKey` stores only AES-256-GCM ciphertext. `LLMPreset.DecryptedApiKey` (marked `[NotMapped]`) is populated at runtime by `IApiKeyEncryptionService`.

5. **Soft-delete, never hard-delete** for: Game, Player, Message, Character, NPC, PlotThread. The global EF query filters (`!e.IsDeleted`) enforce this automatically.

6. **SignalR groups are scoped to game ID.** Group name = `gameId.ToString()`. Connection tracking is in-memory (`_playerConnections`); adding Redis backplane would be required for multi-instance deployment.

7. **The Vite build output goes to `wwwroot`.** `vite.config.ts` sets `build.outDir` to `../Adnd.Server/wwwroot`. The .NET server serves this as static files and falls back to `index.html` for SPA routing.

8. **Backwards-compatible DB changes only.** New columns must be nullable or have defaults. No dropping columns without a transitional migration.
