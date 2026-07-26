# Release Notes

## v0.1.0 — 2026-07-26

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
