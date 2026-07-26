# ADnD — Build Plan

> **Status**: READY — all decisions locked. Implementation can begin.
> **Scope**: Full greenfield build from the design document. Repo currently contains only `DESIGN_DOCUMENT.md`.

---

## Decisions (locked)

| # | Question | Answer |
|---|----------|--------|
| Q1 | MUI vs plain CSS for sidebar | **MUI throughout** — sidebar implemented in MUI but styled to look identical to the plain `<aside>` abook pattern (no visible Drawer chrome, same collapsed/expanded behavior, same DnD colors) |
| Q2 | Wolverine sagas timing | **Day 1** — saga infrastructure from the very first phase that touches AI dispatch |
| Q3 | Feature scope | **Everything** — full feature set as designed, no cuts |
| Q4 | Dev workflow | **Docker-first always** — `docker compose up --build` is the only workflow. No local `dotnet run`. No HTTPS support — app runs behind a reverse proxy; `UseHttpsRedirection` and `UseHsts` are omitted entirely |
| Q5 | LLM provider rename | **LmStudio → OpenAICompatible** — `ProviderType = "openaicompatible"`, class `OpenAICompatibleLLMProvider`. Strategy `OpenAICompatibleStrategy` already correctly named. Test target: LM Studio's OpenAI-compatible endpoint |

---

## Architecture Decisions (locked)

| Concern | Choice |
|---------|--------|
| Backend | ASP.NET Core 10 + EF Core 10 + Npgsql |
| Database | PostgreSQL 17 + pgvector extension |
| Message bus / Sagas | WolverineFx 6.8 (PostgreSQL-backed, no RabbitMQ) |
| Real-time | SignalR hub at `/gamehub` |
| Auth | JWT HS256 (60 min) + refresh token rotation (30 day) |
| API key encryption | AES-256-GCM (`Encryption__MasterKey`) |
| HTTPS | None — app runs behind a reverse proxy; `UseHttpsRedirection` + `UseHsts` omitted |
| Frontend | React 19 + TypeScript + Vite 6 |
| UI library | MUI v7 throughout — sidebar uses MUI styled components to look identical to abook `<aside>` pattern |
| Sidebar | MUI `Drawer` / `Box` — heavily styled, same collapsed/expanded behavior and DnD colors as abook |
| Theme | Arcane Dark: accent `#7c3aed`, bg `#0c0a0e`, surface `#141019`; CSS custom properties + MUI `createTheme` |
| LLM providers | Ollama, **OpenAICompatible** (replaces LmStudio), OpenAI, Google — `ProviderType: "openaicompatible"` |
| SPA serving | ASP.NET `wwwroot` via `MapFallbackToFile("index.html")` |
| Production | Docker multi-stage (Node 22 builds client → aspnet runtime serves it); no local dev workflow |

---

## Incorporated Improvements (accepted from suggestions)

| # | Decision |
|---|----------|
| S1 | **GM thinking indicator** — `GMThinking` / `GMDoneThinking` SignalR events; frontend shows "GM is composing…" typing indicator |
| S2 | **Character backstory in GM context** — included in LLM context IF filled in. Not mandatory for players |
| S3 | **Default per-system prompt templates** — seed D&D 5e, PF2e, CoC 7e, Generic templates on first run |
| S4 | **No sessions at all** — game is non-stop continuous. One internal session auto-created on game start, never exposed to users. On-demand session recap available via admin page |
| S5 | **Context window management** — `MaxContextMessages` + `MaxContextTokenEstimate` on `LLMPreset` |
| S6 | ~~GM manual narration~~ — **rejected** — creator influence limited to nudging only |
| S7 | **Secret dice rolls** — `isSecret` flag sends result only to the rolling player as a private GM whisper. Visible to GM in admin |
| S8 | **pgvector HNSW index** — variable embedding dimensions (no hardcoded size in column). HNSW index created with configurable `EmbeddingDimension` (default 1536) |
| S9 | **Event-driven PlotWeaver triggers** — combat end, session start equivalent (game resume), player death, `StorySwayed`. Additional events welcome |
| S10 | **GM error notification** — log via logging framework, broadcast human-readable `GMError` SignalR event to players |
| S11 | **Auto-loot generation** — `generateLoot` GM tool, triggered on `CombatEnded` with defeated enemies |
| S12 | Roll history = part of message history (covered by "everything is a message" rule) |
| S13 | **LLM health as readiness-only** — `LlmProvidersHealthCheck` tagged `"ready"` only, not liveness |
| S14 | **NPC attitude + faction** — `Attitude` enum (Friendly/Neutral/Unfriendly/Hostile) + `Faction` string on NPC, included in GM context |

---

## Additional Architectural Rules (from verification + user feedback)

### Everything is a Message
Every game event produces a `Message` record with appropriate `MessageType` and `Metadata`. There is no separate dice history table, no separate combat event log visible to players — all of it flows through the message feed.

- Dice roll → `Message` (type=DiceRoll, metadata has formula/result/breakdown)
- Combat events → `Message` (type=CombatAttack, etc.)
- Skill checks → `Message`
- GM narration → `Message` (type=GM)
- Whispers → `Message` (type=Whisper, with WhisperFromId/WhisperToId)
- Secret dice → private `Message` delivered only to roller + GM

**Message visibility per player:**
- Public: all players in game
- OOC: all players
- Whispers: sender + recipient + GM only
- Secret rolls: roller + GM only
- GM messages: all players
- System messages: all players (or targeted)

**Infinite scroll**: load newest N messages first, scroll-up triggers load of older pages (cursor-based by `CreatedAt`). SignalR `NewMessage` appends to live end. Dedup by message ID.

### Server-Side Dice Only
No client-side RNG. All dice rolls happen in `DiceEngine` on the server. Results are posted as messages. Rerolls (if game mechanics allow) are offered via a private whisper from the GM to the player containing a reroll button/confirmation.

### No Sessions (User-Facing)
`GameSession` exists as internal infrastructure for message grouping and saga correlation, but is never exposed in the UI. One session is auto-created on `GameStarted` and never explicitly closed (until game is archived). There is no "start session" or "end session" concept for users.

On-demand recap: admin page has a "Generate Recap" button that calls `GenerateSessionSummaryAsync` on the current period and posts the result as a GM message.

### LLM Interaction Log Visibility
`LLMInteractionLogs` are visible to the user who owns the preset that was used (matched via `UserId`). Not just admins. `LLMLogsController` enforces this: a player can see logs for interactions that used their own presets.

### EF Migrations in Docker
Since no local dev workflow exists, migrations are generated via:
```bash
docker compose run --rm app-sdk dotnet ef migrations add <Name> --project src/Adnd.Server
```
A `app-sdk` profile service based on the SDK build stage is provided in `docker-compose.yml`. Migrations are committed to source after generation. Applied automatically on startup via `MigrationService`.

---

### S2 — Character backstory in GM context (quality, high value)
**Problem**: `RAGService.GeneratePlotContextAsync` assembles recent messages + NPCs + plot threads, but not the players' character backgrounds, traits, bonds, and flaws. The AI GM doesn't know who the characters *are* as people — only their stats.
**Fix**: Add character Background, Traits, Bonds, Flaws to the context string. The GM will write more personalized narration that references character history.

---

### S3 — Default per-system prompt templates (quality, high value)
**Problem**: The `PromptTemplates` table exists but ships empty. A new game has no system prompt guidance, so the AI GM starts cold with whatever the LLM's base personality is.
**Fix**: On first run (or seeded via migration), create a default `IsDefault=true` template per RPG system:
- D&D 5e: tactical, heroic, RAW-adjacent tone, 5e mechanics
- Pathfinder 2e: action economy focus, gritty-heroic tone
- CoC 7e: horror atmosphere, sanity-focused narration, pull-no-punches
- Custom/Generic: neutral GM template

GMs edit these per-game. Zero extra UI needed — `PromptTemplateService` already has `GetDefaultAsync()`.

---

### S4 — Session-start recap narration (quality, medium value)
**Problem**: When a new session opens in an ongoing game, the AI GM starts narrating fresh with no "previously on…" framing. Players have to remember where they left off.
**Fix**: In `GameStartService` (or on `SessionCreated` handler), if the game has prior sessions, automatically trigger `GenerateSessionSummaryAsync` for the last session and post it as a GM message before the opening narration. Toggle-able via `GameParameters`.

---

### S5 — Explicit context window management (reliability, high value)
**Problem**: The design sends recent messages to the LLM but never specifies a cap. After 50+ sessions, context overflows the model's window — silent truncation, garbled narration, or API errors.
**Fix**: Add two `LLMPreset` fields: `MaxContextMessages` (default 30) and `MaxContextTokenEstimate` (default 4000). `AgentBus.HandleGMCall()` trims the message list before building the system prompt. RAG context always included; oldest messages dropped first. No schema change needed — fits in existing `ExtraParams` jsonb, but a dedicated migration column is cleaner.

---

### S6 — GM manual narration (control, medium value)
**Problem**: If the AI GM says something wrong, the game creator's only recourse is to type as a player (which is weird) or pause the GM. There's no way to inject a correction styled as GM narration.
**Fix**: New hub method `InjectGMNarration(gameId, text)` — Creator-only. Posts a `Message` with `Type=GM` (same as AI output) without going through the saga. Displayed identically to AI narration. The AI doesn't know it happened (unless it falls in the next context window). No new saga paths needed.

---

### S7 — Secret dice rolls (gameplay, medium value)
**Problem**: In D&D, GMs often roll secretly — Passive Perception vs. Stealth, trap detection, NPC saving throws. Currently all dice results broadcast to the game group.
**Fix**: Add `isSecret: bool` to `RollDice` and `RollSkillCheck` hub methods. If secret, broadcast the result only to the rolling player's connection (not the game group) and store the roll with a `IsSecret` flag in `Message.Metadata`. The GM sees all rolls in the Admin dashboard.

---

### S8 — pgvector HNSW index on embedding columns (performance, important at scale)
**Problem**: The design uses pgvector cosine similarity (`<=>`) with no index defined on `PlotThread.Embedding` or `Message.Embedding`. This is an O(n) sequential scan — fine for small games, catastrophic for long campaigns with thousands of messages.
**Fix**: Add to a migration (after `InitialCreate` establishes the vector columns):
```sql
CREATE INDEX ix_plotthreads_embedding ON "PlotThreads" USING hnsw ("Embedding" vector_cosine_ops);
CREATE INDEX ix_messages_embedding ON "Messages" USING hnsw ("Embedding" vector_cosine_ops);
```
EF Core `HasIndex()` with `HasMethod("hnsw")` via Pgvector.EntityFrameworkCore extension.

---

### S9 — PlotWeaver event-based triggers (quality, medium value)
**Problem**: `PlotWeaverHandler` triggers only on message count. Major story events (combat end, player death, session start) don't trigger PlotWeaver even though they're the most dramatically significant moments.
**Fix**: In addition to the message counter, also trigger `ReviewAndAdaptAsync` on:
- `CombatEnded` (especially if a PC or significant NPC died)
- `SessionCreated` (opening a new session with existing threads)
- `PlayerLeft` with reason "character death"
- `StorySwayed` (creator pushed a sway)

One new handler each, calling the existing `IPlotWeaver` interface.

---

### S10 — GM error notification (reliability, medium value)
**Problem**: When `DeadLetterQueue` swallows a failed `AgentCall`, the game silently stalls. Players don't know if the GM is slow or broken.
**Fix**: In `AgentCallFailedHandler` (or `DeadLetterQueue`), when a call exceeds max retries, broadcast `GMError { gameId, message: "The GM encountered an issue and will retry shortly." }` via SignalR. Frontend shows a dismissible banner. Already have the infrastructure — just need the event + client handler.

---

### S11 — Auto-loot generation after combat (immersion, nice-to-have)
**Problem**: After combat, the AI GM narrates victory but there's no loot. Someone has to manually describe what the monsters dropped.
**Fix**: New GM tool `generateLoot(combatId, defeatedNPCIds)`. Triggered automatically (or on demand) when `CombatEnded` fires with defeated participants. The LLM produces a JSON array of items appropriate to the monster types, which are added to the GM narration and optionally to the first player's inventory for the GM to distribute.

---

### S12 — Roll history visible to own players (UX, low effort)
**Problem**: `DiceHistoryController` is in the Admin section. Regular players can't see their own roll history from within the game.
**Fix**: Add a `GET /api/dice/my-rolls?gameId=` endpoint returning rolls for the current player only, and a small collapsible "Roll History" section in `GameChatPage` (or linked from the chat panel).

---

### S13 — LLM health check as readiness-only (reliability)
**Problem**: `LlmProvidersHealthCheck` is currently wired to both `/health` (liveness) and `/health/ready` (readiness). If your Ollama instance goes down, Docker's healthcheck would restart the entire container even though the app itself is healthy.
**Fix**: Move `LlmProvidersHealthCheck` to readiness-only (tagged `"ready"`). Liveness only checks database connectivity. The app stays up; the load balancer/reverse proxy knows not to send traffic, but the container doesn't restart.

---

### S14 — NPC attitude / faction tracking (depth, medium value)
**Problem**: NPCs have stats and description but no structured tracking of their attitude toward players (Friendly/Neutral/Hostile) or faction membership. The AI GM has to infer this from the description text.
**Fix**: Add `Attitude` (enum: Friendly/Neutral/Unfriendly/Hostile) and `Faction` (string, nullable) to the `NPC` model. Include in `RAGService` context string as "Gareth [Hostile, City Guard faction]". Small migration, immediate impact on GM narration quality.

---

## Phase Plan

### Phase 0 — Repo Scaffold
**Goal**: Buildable skeleton, nothing functional.

- `src/Adnd.Server/` — dotnet new webapi, solution file, `.csproj` with all NuGet packages pinned
- `src/Adnd.Client/` — Vite + React 19 + TypeScript scaffold
- `docker-compose.yml` — postgres + app services
- `src/Adnd.Server/Dockerfile` — multi-stage (Node 22 → dotnet SDK → aspnet runtime)
- `.env.example` — all required environment variables documented
- `VERSION` file
- `vite.config.ts` — build output → `../Adnd.Server/wwwroot`, dev proxy to 5010
- `.gitignore` — standard dotnet + node + wwwroot

**Deliverable**: `docker compose up --build` starts postgres and app container (app returns 200 on `/health`).

---

### Phase 1 — Backend Foundation (Data Layer + Auth)
**Goal**: Database connected, migrations working, auth endpoints live.

#### 1a — EF Core + Database
- `AppDbContext` with all 28 DbSet properties
- All entity models (User, Game, Player, Character, GameSession, Message, NPC, PlotThread, PlotReview, AgentCall, ToolCallCoordinator, GMToolCall, Whisper, LLMPreset, LLMInteractionLog, AuditLog, RefreshToken, Combat, CombatParticipant, CombatEvent, SessionNote, PromptTemplate, GameTemplate, PlotReview, EventRecord, CustomSystemDefinition)
- `OnModelCreating`: soft-delete filters, jsonb columns, vector columns, unique indexes, cascade rules
- All EF migrations (chronological, matching design doc):
  1. `InitialCreate`
  2. `HighPriorityFeatures`
  3. `PerformanceIndexes`
  4. `GameTemplates`
  5. `SoftDeleteAndWhispersCleanup`
  6. `LLMInteractionLogSnapshotFields`
  7. `SingleSessionPerGame`
  8. `EventBusReplacement`
  9. `AddCurrentSessionIdColumn`
  10. `ReactiveSagaArchitecture`
  11. `LLMPresetStrategyFields`
- `MigrationService` + `app.UseDatabaseMigrations()` auto-applies on startup
- pgvector: `UseVector()` on NpgsqlDbContextOptionsBuilder

#### 1b — Auth
- `AuthService`: BCrypt hash, JWT generation, refresh token rotation, logout
- `AuthController`: `/api/auth/register`, `/login`, `/refresh`, `/logout`, `/me`, `/change-password`, `/display-name`
- JWT middleware: HS256, `JwtSettings__*` env config, SignalR query-string token extraction
- `ApiKeyEncryptionService`: AES-256-GCM, `Encryption__MasterKey`
- `UserIdProvider`: resolves current user from claims

**Deliverable**: Register, login, refresh, logout work. JWT protects all subsequent endpoints.

---

### Phase 2 — Game Management + LLM Presets
**Goal**: Create games, configure LLM, invite players, start sessions.

- `LLMPresetService` + `LLMPresetsController`: full CRUD, encrypt/decrypt API key, set-default, test-connection stub
- `GameManagementService` + `GamesController`: game CRUD, invite code generation, join by code, player management, role promotion, start/archive
- `SessionManagementService`: create/close sessions, `Game.CurrentSessionId` one-to-one
- `GameAuthorizationService`: Creator/Player/Spectator/Observer role enforcement
- `SystemRegistry` singleton: dnd5e, pf2e, coc7e built-in definitions
- `SystemsController`: list systems, custom system CRUD
- `DiceEngine` singleton: full dice formula parser (`4d6kh3+2d4-1`, kh/kl/dh/dl)
- `GameEngine`: system-aware rules (skill checks, attack rolls, proficiency)
- Rate limiting middleware: Global + Auth + LLMPreset limits

**Deliverable**: Full game lifecycle via REST (create → invite → join → start → archive). LLM preset created and stored encrypted.

---

### Phase 3 — LLM Provider System
**Goal**: All four providers callable, strategy pattern wired, resilience applied.

- `ILLMProvider` interface + `BaseLLMProvider` (template method, logging wrapper, tool-call fallback parser)
- Four concrete providers: `OllamaLLMProvider`, `OpenAICompatibleLLMProvider` (ProviderType `"openaicompatible"` — LM Studio, any OpenAI-compatible endpoint), `OpenAILLMProvider`, `GoogleAIStudioLLMProvider`
- `*FromPreset` variants constructed at runtime from `LLMPreset`
- `LLMProviderFactory`: routes on `preset.ProviderType`
- Strategy layer (`Services/Llm/`): `IAdndLlmStrategy`, `AdndLlmStrategyFactory`, four strategy classes
- `JsonExtract` utility: three-pass JSON extraction from messy LLM output
- `ResiliencePolicies`: Polly retry + circuit breaker, configured from `Resilience__*` env vars
- `EmbeddingService`: delegates to preset's embedding model, falls back to zero-vector
- `LLMInteractionLogger`: persists every call to `LLMInteractionLogs`
- `LlmProvidersHealthCheck` + `PgVectorHealthCheck`

**Deliverable**: `POST /api/llmpresets/{id}/test` returns provider status. All four providers can complete a prompt.

---

### Phase 4 — Event System + Wolverine Saga Infrastructure
**Goal**: Durable async message bus live; AgentSaga can orchestrate a full GM narration cycle.

- All 40+ `IGameEvent` record types in `GameEvents.cs`
- `EventBusWorker` + `IEventBus`: delegates `PublishAsync<T>` to Wolverine
- `HandlerRegistry`: scans assembly at startup, builds `Dictionary<string, IEventHandler<T>>`
- Wolverine setup: `PersistMessagesWithPostgresql(connStr, "public", MessageStoreRole.Main)`
- `AgentSaga`: Wolverine saga keyed on `AgentCall.Id`, 5-minute timeout
- `AgentBus` (`IAgentBus`): `SendCallAsync` → saves AgentCall → publishes `AgentCallQueued`; `HandleGMCall` for synchronous direct execution
- `DeadLetterQueue`: 3-retry max per AgentCall.Id
- All saga handler chain (8 handlers):
  - `SagaOrchestratorHandler` (AgentCallQueued → LLMDispatchRequested)
  - `LLMDispatchHandler` (LLMDispatchRequested → LLMResponseReceived)
  - `LLMResponseHandler` (LLMResponseReceived → ToolCallRequested | NarrativeReady)
  - `ToolExecutionHandler` (ToolCallRequested → ToolCallCompleted | ToolCallWaitingConfirmation)
  - `CoordinatorHandler` (ToolCallCompleted → ToolCallRequested[n+1] | LLMFollowUpRequested)
  - `LLMFollowUpHandler` (LLMFollowUpRequested → NarrativeReady)
  - `NarrativeHandler` (NarrativeReady → persists GM message → broadcasts NewMessage)
  - `GameLifecycleHandler` (GameNarrationStarted → transitions game to Active)
- `GameAgent` + `GameAgentManager` singleton: per-game agents, startup recovery, pause/resume
- `GMToolRegistry`: tool definitions + execution (narrate, rollDice, skillCheck, requestPlayerRoll, queryCharacter, queryNPCs, searchPlotContext, updateGameState, sendWhisper, startCombat, addCombatParticipant + query tools)
- `GMToolCallService`

**Deliverable**: Sending a player message triggers the full saga chain and produces a GM narration that's persisted and ready to broadcast.

---

### Phase 5 — SignalR Hub
**Goal**: Real-time bidirectional game events over WebSocket.

- `GameHub` base class: connection tracking (`_playerConnections`), `OnConnected/Disconnected`, `PersistGameEventAsync` (saves Message + embedding), `ResolveGameSessionAsync`, 60s stale cleanup timer
- All 18 partial hub files:
  - `ChatMethods`: SendMessage, SendWhisper, SendOOCMessage, SendOOCWhisper
  - `Combat`: 12 combat hub methods (start/end combat, add/remove participant, roll initiative, advance turn, deal damage, heal, apply/remove condition, record death save)
  - `Dice`: RollDice, RollSkillCheck, RollAttack
  - `AICombat`: tactical suggestions
  - `AgentMethods`: TriggerNarrate, TriggerSuggest, GetGMStatus, PauseGM, ResumeGM
  - `CharacterCreation`, `FlavorText`, `Grid`, `Inventory`, `JoinLeave`, `Progression`, `RestSystem`, `SAN`, `Spells`, `SystemSpecific`, `ToolCalls`, `Whispers`, `HelperMethods`
- All server→client SignalR events: `NewMessage`, `GameNarration`, `GameStatusChanged`, `GMStatusChanged`, `PlayerDisconnected`, `PlayerReconnected`, `CombatStarted`, `CombatEnded`, `TurnAdvanced`, `ParticipantAdded`, `ParticipantRemoved`, `CombatDamageDealt`, `CombatConditionApplied`, `CombatConditionRemoved`, `PlayerRollRequested`
- `WhisperService`
- All remaining controllers: AdminController, CharactersController, CombatLogController, DiceHistoryController, GMStatusController, GMToolController, GameStateController, LLMController, LLMTriggerController, NPCsController, PlotWeaverController, PlotsController, QuickWinsController, SpellManagementController, SwayController, WhispersController, LLMLogsController, AgentFrameworkController
- `LLMLogsController`, `AgentFrameworkController`

**Deliverable**: Full real-time game loop: player types → hub persists → saga runs → GM response broadcasts to all clients.

---

### Phase 6 — Combat System
**Goal**: Full combat tracker working end-to-end.

- 12 combat service interfaces + implementations:
  - `ICombatLifecycleService`: start/end, status transitions
  - `ICombatParticipantService`: add/remove participants
  - `ICombatInitiativeService`: roll initiative, sort order
  - `ICombatTurnService`: advance/retreat, action economy reset
  - `ICombatStateService`: state queries
  - `ICombatSpellService`: cast spells, consume spell slots
  - `ICombatInventoryService`: use items during combat
  - `ICombatProgressionService`: grant XP, level up
  - `ICombatGridService`: set grid, positions, move
  - `ICombatAIService`: LLM tactical suggestions
  - `ICombatQueryService`: query combat state
  - `ISANService`: CoC 7e sanity checks
- `ICombatService` facade
- Death save logic (D&D 5e): 3 successes = stable, 3 failures = dead, nat 20 = revive 1 HP, nat 1 = 2 failures
- Action economy reset per turn
- Combat events logged to `CombatEvent` table

**Deliverable**: Full combat round works via hub: initiative → turns → damage → conditions → death saves.

---

### Phase 7 — Plot Intelligence (RAG + PlotWeaver)
**Goal**: Semantic plot context and adaptive story management.

- `RAGService`: context assembly (recent messages + NPCs + active threads), pgvector cosine similarity search, session summary generation, consistency check, continuation suggestions, batch/single embedding generation, 60-minute embedding cache
- `PlotWeaver` + 4 strategy classes:
  - `PlotThreadGenerationStrategy`
  - `PlotThreadAdaptationStrategy`
  - `PlotMilestoneSpawningStrategy`
  - `PlotOpportunityDetectionStrategy`
- `PlotWeaverHandler`: per-game message counter, triggers `ReviewAndAdaptAsync` every N messages
- `CharacterCreationFactory`: 8-background character creation
- `NarrativeGenerationFactory`
- `GameStartService`
- Hangfire: background job setup (PostgreSQL-backed)

**Deliverable**: Plot threads auto-generate on game start, adapt with story momentum, and semantic context feeds into GM system prompts.

---

### Phase 8 — Frontend Core
**Goal**: Working SPA with auth, dashboard, and the main game chat page.

#### 8a — Scaffold + Theme
- Vite 6 + React 19 + TypeScript project
- `src/main.tsx`: `ColorModeContext`, `useColorMode()`, `localStorage('adnd-theme')`, MUI `createTheme` with DnD Arcane Dark palette (`#7c3aed` / `#0c0a0e` / `#141019`)
- `Sidebar.tsx`: MUI-based, styled to look identical to abook's `<aside class="app-sidebar">` — same collapse behavior, same button/icon/label structure, same DnD colors. Collapse state in `localStorage('adnd-sidebar')`. Components: `SidebarBtn`, `SidebarDivider`, `SidebarSection`.
- `AppShell.tsx`: layout wrapper (sidebar + `<Outlet>`)
- MUI theme CSS variables bridged for any raw CSS that needs palette values

#### 8b — API Client + Types
- `src/api/client.ts`: `APIClient` singleton, JWT from `localStorage`, 401 auto-refresh, 429 handling
- All TypeScript types: `auth.types.ts`, `game.types.ts`, `agent.types.ts`, `combat.types.ts`, `gm.types.ts`, `llm.types.ts`, `message.types.ts`, `plot.types.ts`, `template.types.ts`
- Full API surface coverage matching all controllers

#### 8c — Hooks
- `useHub.ts` (`useGameHub`): SignalR connection, JWT via `accessTokenFactory`, `withAutomaticReconnect`, `connect(gameId)` → `JoinGameGroup`
- `useGame.ts`, `usePlayers.ts`, `usePlotWeaver.ts`, `useCharacters.ts`, `useNPCs.ts`, `useToolCalls.ts`, `useMessagesInfiniteScroll.ts`

#### 8d — Core Pages
- `AuthPage.tsx`: login + register forms
- `DashboardPage.tsx`: game list, create game dialog, join by invite code
- `GameChatPage.tsx`:
  - Zone A: `CollapsibleCombatPanel` (initiative order, HP bars, AC, action economy chips, death save tracker, conditions, `CharacterSheetPopup`)
  - Zone B: `ChatPanel` (paginated infinite scroll backwards, dedup live vs paginated by ID, type-aware message rendering, react-markdown for GM narration)
  - Zone C: `ChatInput` (single-line, in-game/OOC toggle, receiver dropdown, whisper chips)
  - `ToolCallBanner`: pending AI tool call notifications
  - SignalR subscriptions: all 12 events

#### 8e — Admin + Character Pages
- `AdminDashboardPage.tsx`: game header, stat cards, state controls, game details panel
- `AdminPlotBoardPage.tsx`, `AdminNPCsPage.tsx`, `AdminCharactersPage.tsx`, `AdminConsistencyPage.tsx`
- `AdminLLMLogsPage.tsx`: filterable table, expandable rows, bulk delete
- `AdminAgentCallsPage.tsx`
- `CharacterSheetPage.tsx`: 6-tab sheet (Stats, Skills, Inventory, Spells, Background, Custom)
- `CharacterCreateWizard.tsx`: 8-background multi-step wizard
- `LLMPresetsPage.tsx`: preset list + create/edit form
- `GameSettingsPage.tsx`, `SystemsPage.tsx`, `UserSettingsPage.tsx`

**Deliverable**: Full UI working end-to-end: register → create game → chat → GM responds → combat tracker updates in real time.

---

### Phase 9 — Polish + Production
**Goal**: App is deployable, observable, and hardened.

- Swagger with Bearer security definition
- Health endpoints: `/health` (liveness), `/health/ready` (readiness) — DatabaseHealthCheck, LlmProvidersHealthCheck, PgVectorHealthCheck
- Security headers middleware: X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy
- Forwarded headers (X-Forwarded-For / X-Forwarded-Proto) — trust proxy headers, no HTTPS redirection
- CORS: AllowAll policy (for SPA; reverse proxy handles TLS termination)
- Logging: EF Core log level from env (`Logging__LogLevel__Microsoft.EntityFrameworkCore`)
- `LowerCaseControllerConvention` on all routes
- Docker build verified: `docker compose up --build` → fully functional at `http://localhost:5010`
- `.env.example` finalized with all required vars and generation instructions

**Deliverable**: `docker compose up --build` produces a production-ready, fully functional ADnD instance.

---

## Implementation Order (within each phase)

Within each phase, always build in this order:
1. Models / entities first (no dependencies)
2. Interfaces / contracts second
3. Implementations third
4. Registration in `Program.cs` / DI fourth
5. Controllers / hub methods last

---

## File Structure (target)

```
adnd/
├── DESIGN_DOCUMENT.md
├── PLAN.md                          ← this file
├── docker-compose.yml
├── .env.example
├── VERSION
├── src/
│   ├── Adnd.Server/
│   │   ├── Adnd.Server.csproj
│   │   ├── Adnd.Server.sln
│   │   ├── Program.cs
│   │   ├── Dockerfile
│   │   ├── appsettings.json
│   │   ├── Agent/
│   │   │   └── GameAgent.cs
│   │   ├── Controllers/             (20+ controllers)
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── MigrationService.cs
│   │   │   ├── GameExtensions.cs
│   │   │   └── Migrations/
│   │   ├── Events/
│   │   │   ├── GameEvents.cs
│   │   │   ├── IGameEvent.cs
│   │   │   └── IEventHandler.cs
│   │   ├── Handlers/                (11 handler files)
│   │   ├── Hubs/                    (19 hub files)
│   │   ├── Models/
│   │   ├── Services/
│   │   │   ├── AgentBus.cs
│   │   │   ├── AgentSaga.cs
│   │   │   ├── Llm/                 (strategy layer)
│   │   │   └── ...
│   │   └── wwwroot/                 (gitignored, built output)
│   └── Adnd.Client/
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── index.html
│       └── src/
│           ├── main.tsx
│           ├── App.tsx
│           ├── api/
│           │   ├── client.ts
│           │   └── hooks/
│           ├── components/
│           │   ├── AppShell.tsx
│           │   ├── Sidebar.tsx      (plain CSS, abook pattern)
│           │   └── ...
│           ├── pages/
│           └── types/
```

---

## Ground Rules (non-negotiable, apply to every commit)

1. **Docker only** — never run the project locally. All verification is via `docker compose up --build`. No `dotnet run`, no `npm run dev`.

2. **EF tool for migrations always** — never hand-write migration files. Use `dotnet ef migrations add <Name> --project src/Adnd.Server` inside a container or via the EF global tool. Migrations are committed as generated.

3. **Fix warnings** — all compiler warnings and build warnings must be resolved on the same commit that introduces them. Never suppress via `#pragma warning disable`, `[SuppressMessage]`, or `<NoWarn>` unless the warning is a known false-positive in a third-party package and there is no other fix.

4. **VERSION file** — root-level file named `VERSION` (no extension) containing only the semver string (e.g., `0.1.0`). Vite reads this via `fs.readFileSync('../../VERSION')` and defines `__APP_VERSION__`.

5. **RELEASE_NOTES.md** — maintained in the abook style: `## vX.Y.Z — YYYY-MM-DD` header, bullets grouped by category (`fix:`, `feat:`, `infra:`, `ui:`, `refactor:`, `perf:`, `api:`, `types:`, `docs:`). Updated on every commit with that commit's changes.

6. **Version bump rule**:
   - If the version in `VERSION` has already been pushed to `origin` (i.e., it appears in `git log origin/main..HEAD` as unchanged), bump the patch version before writing release notes.
   - If the current version has not yet been pushed, leave `VERSION` unchanged and append to (or create) the existing version's section in `RELEASE_NOTES.md`.
   - Never create a new version entry if the current one is still local-only.

7. **Release notes date** — always use today's actual date on the heading. If the current version section already exists, update its date to today if it has changed.

8. **Package versions** — always use latest stable versions. Do not pin to the specific versions listed in the design document. Re-evaluate at scaffold time.

---

## Key Invariants (must be preserved throughout build)

1. **EF Core only for migrations** — never raw SQL DDL. `dotnet ef migrations add <Name>`.
2. **Wolverine for all async inter-handler communication** — no MediatR, no manual channels, no RabbitMQ.
3. **LLMPreset is the source of truth** — all LLM calls through `ILLMProviderFactory.CreateFromPreset()`.
4. **API keys encrypted at rest** — `LLMPreset.ApiKey` = AES-256-GCM ciphertext. `DecryptedApiKey` is `[NotMapped]`.
5. **Soft-delete only** — Game, Player, Message, Character, NPC, PlotThread. Global EF query filters enforce this.
6. **SignalR groups = game ID** — group name = `gameId.ToString()`. Connection tracking in-memory.
7. **Vite output → wwwroot** — `build.outDir` = `../Adnd.Server/wwwroot`. ASP.NET serves SPA + falls back to `index.html`.
8. **Backwards-compatible DB changes** — new columns must be nullable or have defaults.
9. **MUI sidebar styled like abook** — MUI components styled to produce the same visual result as abook's `<aside class="app-sidebar">`: collapsible, icon + label buttons, no visible Drawer chrome. `collapsed` / `expanded` state in `localStorage('adnd-sidebar')`.
10. **DnD Arcane Dark palette** — accent `#7c3aed`, bg `#0c0a0e`, surface `#141019`. Defined in MUI `createTheme`; bridged to CSS variables where needed.
11. **No HTTPS in app layer** — `UseHttpsRedirection` and `UseHsts` are never added. TLS is the reverse proxy's job.
