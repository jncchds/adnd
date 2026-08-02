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
# Swagger:  http://localhost:5010/swagger
# Hangfire: http://localhost:5010/hangfire
# Health:   http://localhost:5010/health
# Readiness (LLM + pgvector): http://localhost:5010/health/ready
```

Required `.env` values: `POSTGRES_PASSWORD`, `JWT_SECRET_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `ENCRYPTION_MASTER_KEY`.

## EF Core Migrations

Always use the `sdk` Docker profile — never run `dotnet ef` against a local toolchain.

```bash
docker compose run --rm sdk dotnet ef migrations add <Name> --project src/Adnd.Server
```

Migrations are applied automatically on startup via `MigrationService.ApplyMigrationsAsync()`. There is one migration: `20260726134122_InitialCreate`.

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

### GM Tool Registry

12 registered tools: `narrate`, `rollDice`, `skillCheck`, `requestPlayerRoll`, `queryCharacter`, `queryNPCs`, `searchPlotContext`, `updateGameState`, `sendWhisper`, `startCombat`, `addCombatParticipant`, `generateLoot`. Executed sequentially by `CoordinatorHandler`.

### LLM Provider Abstraction

`ILLMProviderFactory` resolves to `ILLMProvider` based on `LLMPreset.ProviderType`. Supported values: `ollama`, `openaicompatible`, `openai`, `google`. API keys stored encrypted at rest via AES-256-GCM (`ApiKeyEncryptionService`).

### RAG + Plot Intelligence

`RAGService` builds GM prompt context from recent messages, active plot threads, and NPC states using pgvector HNSW embeddings on `Message.Embedding` and `PlotThread.Embedding`. `PlotWeaver` runs periodic LLM-driven reviews to adapt and generate plot threads.

### SignalR Hub

`GameHub` is a partial class split across ~19 files (combat, dice, spells, inventory, whispers, character creation, etc.). JWT is extracted from the WebSocket query string (`?access_token=…`). Online players tracked in a static `ConcurrentDictionary<string, string>` (connectionId → userId).

### JSONB Storage

Character attributes, skills, inventory, spells, conditions, NPC data, plot adaptation history, and combat metadata are all stored as PostgreSQL `jsonb` using EF value converters. Avoids migrations for game-system-specific data changes.

## Key Conventions

- **`TreatWarningsAsErrors = true`** — fix all compiler warnings before committing.
- **`nullable enable` + `ImplicitUsings`** — nullability annotations required throughout.
- **Primary constructor syntax** (C# 12) used everywhere.
- **Interface-per-service** — every service has a matching `IServiceName` registered in DI. Scoped for anything touching `AppDbContext`; singleton for stateless services (`IDiceEngine`, `ISystemRegistry`, `IGameAgentManager`, `IApiKeyEncryptionService`, `ILLMProviderFactory`).
- **Soft delete** — `ISoftDelete` on `Game`, `Player`, `Message`, `Character`, `NPC`, `PlotThread` with global EF query filters.
- **CamelCase JSON + `JsonStringEnumConverter`** for all API responses.
- **Hangfire** (PostgreSQL-backed, 2 workers) for background jobs alongside Wolverine.
- **Swagger enabled in all environments**, not just Development.
- No test projects exist in this solution.
