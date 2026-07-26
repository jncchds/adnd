# Release Notes

## v0.1.0 — 2026-07-26

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
