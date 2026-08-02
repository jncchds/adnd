# Release Notes

## v0.1.0 — 2026-08-02

### Repair & Hardening Pass

The scaffolded build had never been exercised end to end. This pass makes the core gameplay
loop work and closes broad cross-tenant access on the API.

**fix — GM agent loop (was entirely non-functional):**
- `AgentSaga` could not correlate 4 of its 5 messages: Wolverine resolves saga identity from `Id`/`SagaId`/`AgentSagaId`, and the events carried only `AgentCallId`. Added `[SagaIdentity]`.
- `CoordinatorHandler` read `AgentCall.Output` (narrative prose) and parsed it as the tool-call list inside a bare `catch {}`, so every tool after the first was silently dropped. Tool calls are now persisted on `ToolCallCoordinator.ToolCalls`.
- `LLMDispatchHandler` passed an empty tool array, so the GM could never call a tool; now passes `IGMToolRegistry.GetToolDefinitions()`.
- `ToolCount` was hardcoded `0` at every publish site, so the saga advanced while tools were still outstanding.
- Confirmation-gated tools (`requestPlayerRoll`) dead-ended the saga until the 5-minute timeout; pending calls are now persisted and resumable via `ToolCallConfirmationResolved`.
- Retry never retried and left calls `Running` forever while the saga wrote contradictory terminal state from a second `DbContext`. Split into `AgentCallFailed` (retry decision) and `AgentCallAbandoned` (saga-owned terminal).
- Follow-up LLM calls were dispatched with empty system/user prompts, losing all GM persona and context.
- Real `CancellationToken`s throughout; coordinator rows are cleaned up on completion.

**fix — client/server contract:**
- Every SignalR send passed an extra `sessionId`, shifting all arguments: chat, OOC and whispers were impossible. Corrected against the hub signatures.
- Combat events bound `DamageDto`/`ConditionDto` to `setCombat`, wiping `participants` and white-screening the page mid-fight; they now patch existing state.
- ~20 client calls pointed at routes that do not exist (`/dicehistory`, `/quickwins/prompt-templates`, `/llmtrigger/{gameId}/...`, and others).
- `joinByCode` sent `{ code }` against `JoinByCodeRequest(InviteCode, CharacterName)` — joining a game was impossible. Invite generation read `code` instead of `inviteCode`, so the GM saw nothing.
- Chat history never loaded: `loadInitial` ran against a mount-time closure where `sessionId` was still null.
- `useGameHub.disconnect()` nulled the ref after an `await`, discarding the StrictMode-remounted connection and leaving `invoke` throwing over a live socket.
- 401 handling redirected on failed logins, discarding the error; error bodies now surface `{ error }` instead of raw JSON.
- Replaced silent `catch {}` in every data hook with real error state; added a route-level `ErrorBoundary`.

**feat — endpoints the UI already called but that did not exist:**
- `GET /api/games/sessions/{sessionId}/messages` (cursor-paged, whisper-filtered)
- `GET /api/gmtools/pending`, `POST /api/gmtools/{id}/confirm`, `POST /api/gmtools/{id}/decline`
- `GET /api/games/{id}/sessions`
- `PATCH /api/characters/{id}/adjust` (GM-only level/HP changes)

**feat — Game Admin navigation:**
- Game and Game Admin are now separate sidebar sections; Admin is entered from the Game nav and exited via "Back to Game". All eight admin pages are reachable — seven previously had routes but no navigation at all.

**security:**
- `GET /api/games/{id}/players` returned every member's BCrypt hash and email (`[JsonIgnore]` + `PlayerDto`).
- `GET/POST/PUT /api/llmpresets/{id}` returned provider API keys in cleartext (`[JsonIgnore]` + `LLMPresetDto`).
- `GET /api/llm/providers` returned every user's presets and echoed upstream error bodies.
- Systemic IDOR: ~14 controllers accepted a caller-supplied id with no membership check. Added a fail-closed `GameScopedController` base and applied it throughout.
- SignalR hub had one membership check in total (`JoinGameGroup`); every other method trusted the client's `gameId`. Added member/creator checks and combat-to-game scoping.
- JWT signing key: the `OVERRIDE_IN_ENVIRONMENT` placeholder is long enough for HS256, so a missing env var booted the app signing tokens with a value published in the repo. Now rejected at startup, with a 32-byte floor.
- SSRF on `POST /api/llmpresets/models` via a fully caller-controlled endpoint (`OutboundUrlGuard`; permissive to private ranges by default since self-hosted inference is the normal case, cloud-metadata always blocked).
- Rate limiters used `AddFixedWindowLimiter`, giving one bucket for the whole process — 10 requests could lock every user out of login. Now partitioned per user/IP, with `ForwardedHeaders` actually configured.
- `UseStaticFiles` ran before the security-headers middleware, so `/` and all assets shipped unprotected and unthrottled. Added CSP.
- Swagger is no longer served anonymously in Production; Hangfire dashboard has an explicit authorization filter instead of relying on Hangfire's local-requests-only default (which trusts any same-host proxy).
- Mass assignment: EF entities bound directly from request bodies in NPCs, Plots, QuickWins and Characters (a client-authoritative character sheet). Replaced with DTOs.
- Password change now revokes all sessions; refresh-token reuse revokes the whole family; registration validates email and password length.
- Invite codes use `RandomNumberGenerator` instead of `Random.Shared`.
- Dice formulas are bounded — `999999999d20` allocated ~4 GB.
- Removed `Include Error Detail=true` from the production connection string and added a global exception handler.

**fix — silent data corruption:**
- Spell slots were never consumed: case-sensitive deserialization made every slot read as `(0,0)`, so casters had unlimited spells.
- Combat grid rewrote `Combat.Notes` with only the grid, destroying any other key, and threw on non-grid keys.
- `MilestoneEvents` value comparer compared by `Count`, so milestone status edits were invisible to the change tracker.
- PlotWeaver computed a 30-day cutoff and never used it, soft-deleting every resolved thread on the next pass. Added `PlotThread.ResolvedAt`.
- RAG embedding cache keyed on `string.GetHashCode()` — collisions returned the wrong vector; cache also grew unbounded.
- `SendMessage` re-queried "newest message in session" after insert and could broadcast someone else's message.
- The `sendWhisper` GM tool never read `targetPlayerId`, so whispers reached nobody.
- GM tools accepted LLM-supplied ids without scoping them to the game.
- Death saves never reset on stabilisation, so a later failure still counted toward death.
- `PreviousTurn` clamped at 0 and never decremented the round.

**perf / robustness:**
- `OllamaLLMProvider` built a new `OllamaApiClient` per call and never disposed it — a socket leak; now uses the pooled `HttpClient` like every other provider.
- Google provider threw `KeyNotFoundException` on safety-blocked prompts (HTTP 200 with no `candidates`) — routine for TTRPG combat content — and on `MAX_TOKENS` responses. API key moved from the query string to `x-goog-api-key`.
- OpenAI-compatible provider threw opaquely on gateways that return `200 OK` with an `{"error":...}` body.
- Embedding model was hardcoded (Google) or sent empty (OpenAI-compatible), silently disabling RAG.
- `JsonExtract.TryExtractArray` returned the first array-valued property whatever its name, so a JSON narrative could be parsed as tool calls.
- `LlmProvidersHealthCheck` counted table rows rather than probing; `/health` and `/health/ready` were identical, so an LLM outage failed liveness.
- N+1 queries in initiative rolling and PlotWeaver duplicate detection.

**chore:**
- Deleted the unreferenced `Services/Llm/*Strategy.cs` layer and `HandlerRegistry` (a singleton caching root-provider services — a latent captive `DbContext`).
- Added `IDesignTimeDbContextFactory` so `dotnet ef` no longer boots the full host.
- Moved the EF CLI out of the shared build stage; added a container `HEALTHCHECK` and `restart: unless-stopped`.
- Migrations: `AgentLoopToolCallState`, `PlotThreadResolvedAt`.

## v0.1.0 — 2026-07-26

### Phase 9 — Polish + Production Hardening

**feat:**
- `LowerCaseParameterTransformer` + `RouteTokenTransformerConvention`: all controller routes now lowercase (e.g. `/api/games`, `/api/llmpresets`, `/api/auth`)
- Rate limiting: global fixed-window limiter (default 300 req/min per IP) + named `auth` (20/min) + `llm` (10/min) policies via `AddRateLimiter`; `[EnableRateLimiting("auth")]` on `AuthController`, `[EnableRateLimiting("llm")]` on `LLMPresetsController`; `UseRateLimiter()` in middleware pipeline; 429 response on rejection
- `Permissions-Policy: camera=(), microphone=(), geolocation=()` added to security headers middleware

**infra:**
- `.env.example` finalized with all required + optional variables: `POSTGRES_PASSWORD`, `APP_PORT`, `JWT_*`, `ENCRYPTION_MASTER_KEY`, `RESILIENCE__*`, `RATE_LIMITING__*`, `LOGGING__*` with sensible defaults documented

### Phase 8 — Frontend Core (React SPA)

**feat:**
- `main.tsx`: `ColorModeContext` + `useColorMode()` hook, `ThemeProvider` with DnD Arcane Dark palette (`#7c3aed` / `#0c0a0e` / `#141019`), light/dark mode persisted to `localStorage('adnd-theme')`
- `App.tsx`: full router with `BrowserRouter`, `AuthProvider`, `RequireAuth` guard, all 18 routes
- `SidePanel.tsx`: MUI-based collapsible sidebar — hamburger toggle, version pill, user name, main nav (Games/Presets/Systems/Settings), context-sensitive in-game sub-nav (Chat/Admin/Plot Board/NPCs/Characters/Consistency/LLM Logs/Agent Calls), theme toggle + logout; collapse state persisted to `localStorage('adnd-sidebar')`
- `AppShell.tsx`: layout wrapper using `<Outlet>` from react-router-dom; detects current game/admin context from URL
- `AuthPage.tsx`: login + register tabs, BCrypt-safe form, redirects to dashboard on success
- `DashboardPage.tsx`: game grid, create-game dialog (LLM preset/system/language/plot-seed selectors), join-by-invite-code input, start/archive quick actions
- `GameChatPage.tsx`: Zone A = `CombatPanel` (initiative list, HP bars, AC, action economy chips, death-save tracking); Zone B = `ChatPanel` (infinite scroll, SignalR dedup by ID, type-aware message rendering, react-markdown for GM narration); Zone C = `ChatInput` (in-game/OOC toggle, receiver dropdown, whisper routing); GM thinking indicator, `ToolCallBanner`, GM error alert; all SignalR event subscriptions
- `AdminDashboardPage.tsx`: stat cards, game controls (start/archive/pause-resume GM), invite code generator, game details panel, quick-nav to all admin sub-pages
- `AdminPlotBoardPage.tsx`: plot thread cards with momentum bars, category/status chips, PlotWeaver review trigger, add/delete threads
- `AdminNPCsPage.tsx`: NPC cards with attitude chip (color-coded), faction, create/edit/delete dialog
- `AdminCharactersPage.tsx`: player grid showing character name/class/level/HP with link to character sheet
- `AdminConsistencyPage.tsx`: on-demand consistency check + story continuation suggestions via PlotWeaver
- `AdminLLMLogsPage.tsx`: filterable table, collapsible rows with full prompt/response text, token/duration stats, bulk delete
- `AdminAgentCallsPage.tsx`: agent call table with status chips, expandable error details
- `CharacterSheetPage.tsx`: 6-tab sheet — Stats (attribute cards with modifiers), Skills, Inventory, Spells (per-level), Background (editable traits/bonds/flaws), Custom (JSON editor); save to API
- `CharacterCreateWizard.tsx`: 4-step stepper — 8 backgrounds (Acolyte, Criminal, Soldier, Sage, Gladiator, Folk Hero, Urchin, Noble), name/class, attribute assignment, backstory
- `LLMPresetsPage.tsx`: preset list, full create/edit dialog (provider type, model, endpoint, API key masked, temperature slider, max tokens, top-p, reasoning effort, embedding config, stream toggle), test-connection button
- `GameSettingsPage.tsx`, `SystemsPage.tsx`, `UserSettingsPage.tsx`: settings, system list, profile/password pages
- Full TypeScript type system: `auth.types.ts`, `game.types.ts`, `message.types.ts`, `combat.types.ts`, `agent.types.ts`, `gm.types.ts`, `llm.types.ts`, `plot.types.ts`, `template.types.ts`
- `api/client.ts`: `APIClient` singleton — JWT from localStorage, 401 auto-refresh, 429 handling, full API surface coverage (auth, games, NPCs, LLM presets, logs, agent calls, tool calls, plots, combat, characters, templates, triggers, systems, dice, whispers)
- `api/hooks/useHub.ts`: `useGameHub()` — SignalR connection, JWT via `accessTokenFactory`, `withAutomaticReconnect`, game group join on connect
- `api/hooks/`: `useGame`, `useGames`, `usePlayers`, `usePlotThreads`, `useNPCs`, `useCharacter`, `useToolCalls`, `useMessagesInfiniteScroll` — cursor-based pagination, live append with dedup by ID
- `context/AuthContext.tsx`: `AuthProvider` + `useAuth()` — login, register, logout, auto-refresh on mount

**ui:**
- DnD Arcane Dark theme: accent `#7c3aed`, bg `#0c0a0e`, surface `#141019`; MUI `createTheme` wired to CSS variables
- Custom scrollbar styling matching accent palette
- All pages responsive with MUI Grid2 (`size` prop) and flexbox layouts

### Phase 7 — Plot Intelligence (RAG + PlotWeaver) + README + LICENSE

**feat:**
- `RAGService` / `IRAGService`: context assembly (recent messages + NPCs + active plot threads + character backstories), pgvector cosine similarity search via `FindSimilarPlotThreadsAsync`, LLM-driven session summary generation, consistency check, continuation suggestions, batch/single message embedding with 60-minute in-memory cache
- `PlotWeaver` / `IPlotWeaver`: orchestrates four strategy implementations — `PlotThreadGenerationStrategy` (generates 2-4 initial threads from plot seed + LLM), `PlotThreadAdaptationStrategy` (adapts existing threads + momentum based on recent events), `PlotMilestoneSpawningStrategy` (spawns milestone events for threads with momentum ≥ 5), `PlotOpportunityDetectionStrategy` (detects 1-2 new story opportunities per review cycle)
- `CharacterCreationFactory` / `ICharacterCreationFactory`: 8 D&D-style backgrounds (Acolyte, Criminal, Soldier, Sage, Gladiator, Folk Hero, Urchin, Noble) with pre-filled skill proficiencies, tool proficiencies, features, default traits/bonds/flaws, and proper attribute/inventory initialization
- `NarrativeGenerationFactory` / `INarrativeGenerationFactory`: LLM-powered opening narration (game system–aware system prompt), session recap generation (delegates to RAGService), and general narrative generation; auto-loads game's `PromptTemplate` if one exists
- `GameStartService` / `IGameStartService`: full game startup orchestration — creates/gets session, seeds default prompt templates on first run (S3: D&D 5e, PF2e, CoC 7e, Generic), generates prior-session recap if prior sessions exist (S4), generates initial plot threads via PlotWeaver, queues opening narration via AgentBus
- Updated `PlotWeaverHandler`: now injects `IPlotWeaver` and calls `ReviewAndAdaptAsync` every 10 messages; adds S9 event-based triggers — `CombatEnded`, `SessionCreated`, `StorySwayed` all trigger a PlotWeaver review
- Updated `PlotWeaverController`: replaced stub with real endpoints — `GET context/{gameId}`, `GET consistency/{gameId}`, `GET continuation/{gameId}`, `POST embed/{gameId}`, `POST session-summary/{gameId}/{sessionId}`, `POST review/{gameId}`
- Updated `GamesController.Start`: calls `IGameStartService.StartGameAsync` after setting game status to Starting

**infra:**
- Hangfire wired up: `AddHangfire()` with `UsePostgreSqlStorage`, `AddHangfireServer()` (2 workers), `UseHangfireDashboard("/hangfire")` in middleware pipeline
- All 5 new Phase 7 services registered in `Program.cs` as scoped
- `README.md`: project overview, quick start, LLM provider setup table, architecture diagram, endpoints table, tech stack
- `LICENSE`: MIT



### Phase 5 — SignalR GameHub, remaining controllers, WhisperService

**feat:**
- `GameHub` partial class (18 files): base hub with `ConcurrentDictionary` connection tracking, `PersistGameEventAsync`, `BroadcastToGameAsync`
- `GameHub.JoinLeave`: `JoinGameGroup`/`LeaveGameGroup` with group membership + player `IsConnected` tracking
- `GameHub.ChatMethods`: `SendMessage`, `SendWhisper`, `SendOOC*`
- `GameHub.Dice`: `RollDice` (with secret flag), `RollSkillCheck`, `RollAttack`
- `GameHub.Combat`: 12 methods — `StartCombat` through `RecordDeathSave`, `BuildCombatDto` helper
- `GameHub.AgentMethods`: `TriggerNarrate`/`Suggest`, `GetGMStatus`, `PauseGM`/`ResumeGM`
- `GameHub.ToolCalls`: `Confirm`/`Decline` roll + `ConfirmToolCall`
- `GameHub.Whispers`: `GetWhisperHistory`, `SendGMWhisper`
- 10 stub partial files for Phase 6/7 (AICombat, CharacterCreation, FlavorText, Grid, HelperMethods, Inventory, Progression, RestSystem, SAN, Spells, SystemSpecific)
- Response DTOs: `MessageDto`, `GameStatusDto`, `GMStatusDto`, `PlayerDto`, `CombatDto`, `ParticipantDto`, `DamageDto`, `ConditionDto`, `RollRequestDto`, `DiceResultDto`
- `WhisperService`
- Controllers: `CharactersController`, `NPCsController`, `PlotsController`, `GMStatusController`, `GameStateController`, `WhispersController`

**infra:**
- `GameHub` mapped at `/gamehub` in `Program.cs`

---

### Phase 4 — Event System, Wolverine Saga, Agent Framework

**feat:**
- 58+ `IGameEvent` records covering game lifecycle, player, session, chat, combat, character/NPC/plot, story, and saga-internal events
- `IEventBus` / `EventBusWorker` wrapping Wolverine `IMessageBus`
- `IHandlerRegistry` with `ConcurrentDictionary` cache
- `IAgentBus` / `AgentBus`: saves `AgentCall` + publishes `AgentCallQueued`
- `IDeadLetterQueue`: 3-retry max per call ID
- `IGMToolRegistry` with 12 GM tools: `narrate`, `rollDice`, `skillCheck`, `requestPlayerRoll`, `queryCharacter`, `queryNPCs`, `searchPlotContext`, `updateGameState`, `sendWhisper`, `startCombat`, `addCombatParticipant`, `generateLoot`
- `IGMToolCallService`
- `GameAgent` + `GameAgentManager` (`IHostedService`, recovers active games on startup)
- 8 Wolverine saga handlers: `SagaOrchestratorHandler` → `LLMDispatchHandler` → `LLMResponseHandler` → `ToolExecutionHandler` → `CoordinatorHandler` → `LLMFollowUpHandler` → `NarrativeHandler` → `GameLifecycle`
- Event handlers: `ChatHandler`, `PlotWeaverHandler`, `AgentCallFailedHandler`, `ToolCallWaitingConfirmationHandler`

**infra:**
- Wolverine configured with PostgreSQL persistence on `public` schema

---

### Phase 3 — LLM Provider System, Embeddings, Resilience

**feat:**
- `ILLMProvider` interface + `BaseLLMProvider` (template method, fallback text-based tool calling via `JsonExtract` 3-pass extraction)
- Four concrete providers: `OllamaLLMProvider` (OllamaSharp), `OpenAICompatibleLLMProvider` (raw HttpClient, any OpenAI-compatible endpoint), `OpenAILLMProvider` (OpenAI SDK 2.12.0 with native tool calling), `GoogleAIStudioLLMProvider` (raw HttpClient, reasoning budget mapping)
- `LLMProviderFactory`: routes on `ProviderType`
- `EmbeddingService`: game-scoped, falls back to empty vector
- `LLMInteractionLogger`: persists every LLM call to DB
- `ResiliencePolicies`: config-backed Polly retry/circuit-breaker
- `LlmProvidersHealthCheck` and `PgVectorHealthCheck` (both tagged `"ready"`)

---

### Phase 2 — Game Management, LLM Presets, Dice Engine

**feat:**
- `LLMPresetService`: encrypt/decrypt API keys, set-default, test-connection
- `GameAuthorizationService`: Creator/Player role enforcement
- `GameManagementService`: full lifecycle — create, join by invite code, start, archive, promote/kick
- `SessionManagementService`: get-or-create current session
- `DiceEngine`: full formula parser — NdM, kh/kl/dh/dl keep/drop modifiers, flat bonuses, `Random.Shared`
- `SystemRegistry` singleton: built-in D&D 5e, PF2e, CoC 7e system definitions
- `GameEngine`, `PlayerManagementService`
- `GamesController` (12 routes), `LLMPresetsController` (7 routes), `SystemsController` (2 routes)
- DTOs: `GameDtos`, `LLMPresetDtos`

---

### Phase 6 — Combat System + Phase 5 Gap-Fill

**feat:**
- Combat system: 12 domain services + ICombatService facade (lifecycle, participants, initiative, turns, state, spells, inventory, progression, grid, AI suggestions, queries, CoC 7e sanity)
- D&D 5e death save logic (nat 20 revive, nat 1 double failure, 3 success/failure thresholds)
- AgentSaga: Wolverine durable saga keyed on AgentCall.Id with 5-minute timeout via IMessageContext.ScheduleAsync
- LLM strategy layer: IAdndLlmStrategy + 4 provider strategies (Ollama, OpenAI, OpenAI-Compatible, Google AI Studio)
- 12 missing REST controllers: Admin, CombatLog, DiceHistory, GMTool, LLM, LLMTrigger, PlotWeaver, QuickWins, SpellManagement, Sway, LLMLogsController, AgentFramework
- CombatEventLogger: shared scoped helper for writing CombatEvent records with round/turn metadata
- CombatGridService: grid/position state stored in Combat.Notes jsonb under "grid" sub-key
- CombatAIService: builds context string, calls game's LLMPreset provider, parses JSON suggestions array

**infra:**
- Program.cs: 14 new combat service registrations under ── Combat System ── comment block
- GMToolRegistry.cs: added CombatEntity alias to resolve namespace collision with new Services.Combat namespace

### Phase 0 — Repo Scaffold

- infra: ASP.NET Core 10 server project with all NuGet package references, minimal `Program.cs` with health check at `/health`, `MapFallbackToFile("index.html")` for SPA serving
- infra: React 19 + TypeScript + Vite 6 client project — `vite.config.ts` with `build.outDir` targeting `../Adnd.Server/wwwroot`, `__APP_VERSION__` injected from `VERSION` file, dev proxy to `localhost:5010`
- infra: multi-stage `Dockerfile` — Node 22 + .NET SDK 10 build stage (client build → server publish), `aspnet:10.0` runtime stage; EF CLI installed in build stage for SDK profile
- infra: `docker-compose.yml` — `postgres` (pgvector/pgvector:pg17) + `app` services; `sdk` profile service for running `dotnet ef migrations add` commands without local tooling
- infra: `.env.example` — all required environment variables documented with generation instructions
- infra: `VERSION` file (0.1.0) read by Vite to inject `__APP_VERSION__` global
- docs: `PLAN.md` — full build plan with 9 phases, architecture decisions, incorporated improvements (S1–S14), ground rules, and additional architectural rules

### Phase 1 — Backend Foundation (Data Layer + Auth)

**feat:**
- All 25 EF Core entity models: `User`, `Game`, `Player`, `Character`, `GameSession`, `Message`, `NPC`, `PlotThread`, `PlotReview`, `LLMPreset`, `AgentCall`, `ToolCallCoordinator`, `GMToolCall`, `Whisper`, `LLMInteractionLog`, `AuditLog`, `RefreshToken`, `Combat`, `CombatParticipant`, `CombatEvent`, `SessionNote`, `PromptTemplate`, `GameTemplate`, `EventRecord`, `CustomSystemDefinition`
- `ISoftDelete` interface; global EF query filters on `Game`, `Player`, `Message`, `Character`, `NPC`, `PlotThread`
- `Enums.cs` — all domain enums: `GameStatus`, `GMStatus`, `PlayerRole`, `PlotThreadCategory`, `AgentType`, `AgentAction`, `SagaStep`, `CombatStatus`, `CombatEventType`, `WhisperType`, `Attitude`, and more
- `AppDbContext` — 26 DbSets, full `OnModelCreating`: jsonb columns with `ValueComparer<JsonElement>`, vector columns (`Message.Embedding`, `PlotThread.Embedding`), soft-delete filters, unique indexes, cascade rules, jsonb list converters for `PlotThread.AdaptationHistory` and `MilestoneEvents`
- `MigrationService` + `UseDatabaseMigrationsAsync()` extension — auto-applies pending migrations on startup
- `InitialCreate` EF migration — full initial schema with `CREATE EXTENSION IF NOT EXISTS vector;`
- `AuthService` — BCrypt password hashing, JWT HS256 (60 min), refresh token rotation (30 day), register/login/refresh/logout/me/change-password/display-name
- `ApiKeyEncryptionService` — AES-256-GCM with 12-byte nonce + 16-byte tag; `Encryption:MasterKey` from environment
- `UserIdProvider` — resolves `Guid` from JWT `NameIdentifier` claim
- `AuthController` — `/api/auth/register`, `/login`, `/refresh`, `/logout`, `/me`, `/change-password`, `/display-name`

**infra:**
- `Program.cs` fully wired: JWT Bearer auth (with SignalR query-string token extraction), Swagger with Bearer security definition, EF Core + pgvector, `AppDbContext`, auth services, CORS AllowAll, security headers middleware, forwarded headers, `AddHealthChecks().AddDbContextCheck`
- NuGet versions pinned to latest stable: EF Core 10.0.10, Npgsql 10.0.3, Pgvector 0.3.0, WolverineFx 6.22.0, BCrypt 4.2.1, JwtBearer 10.0.10, Polly 8.7.0, Swashbuckle 8.1.1, OllamaSharp 5.4.12
