# AGENTS.md

> Instructions for LLMs and AI assistants working on this project.

## Project Overview

**ADnD (Advanced Dungeon Network)** is a multi-system TTRPG web framework with:
- ASP.NET Core 10 backend (API + SignalR hub + MediatR event bus)
- React 19 + TypeScript + MUI frontend
- PostgreSQL 17 + pgvector database
- Per-game GameAgent with Hangfire persistence
- Creator/AI-GM split architecture
- Pluggable LLM provider system with agentic framework

## Directory Structure

```
src/
├── Adnd.Server/              # Backend (.NET 10)
│   ├── Agent/                # Per-game GameAgent system
│   │   └── GameAgent.cs      # GameAgent + GameAgentManager (singleton)
│   ├── Controllers/          # REST API endpoints
│   │   ├── AuthController.cs     # JWT auth, register, login, refresh, logout
│   │   ├── GamesController.cs    # Games, sessions, players CRUD
│   │   └── AdminController.cs    # NPCs, plots, characters, LLM, RAG, systems, GM agent
│   ├── Data/
│   │   ├── AppDbContext.cs         # EF Core DbContext with all entities
│   │   ├── MigrationService.cs     # Auto-applies migrations on startup
│   │   ├── GameExtensions.cs       # EF query helpers for games
│   │   └── Migrations/             # EF Core migrations
│   ├── Events/               # MediatR events
│   │   └── GameEvents.cs     # 25+ game event types
│   ├── Handlers/             # MediatR event handlers
│   │   ├── GameEventHandlers.cs  # GameLifecycle, GameAction, Chat, Plot, Session
│   │   └── PlotWeaverHandler.cs  # PlotWeaver event handler
│   ├── Hubs/
│   │   └── GameHub.cs        # SignalR: chat, dice, skill checks, attacks, whispers
│   ├── Models/
│   │   ├── User.cs, Player.cs, Game.cs, GameSession.cs
│   │   ├── Character.cs, Message.cs, NPC.cs, PlotThread.cs
│   │   ├── CustomSystemDefinition.cs, RefreshToken.cs, AuthResponse.cs
│   │   ├── AgentCall.cs, Whisper.cs, LLMPreset.cs, LLMInteractionLog.cs
│   │   ├── Combat.cs (initiative, attack, skill check entities)
│   │   └── PlotReview.cs
│   ├── Services/
│   │   ├── AuthService.cs                # JWT + refresh tokens
│   │   ├── GameEngine.cs                 # Core game logic (dice, skills, attacks, chars)
│   │   ├── GameAuthorizationService.cs   # Role-based game access checks
│   │   ├── DiceEngine.cs                 # Dice formula parsing & resolution
│   │   ├── SystemRegistry.cs             # RPG system definitions (dnd5e, pf2e, coc7e, custom)
│   │   ├── LLMProvider.cs                # ILLMProvider interface + BaseLLMProvider + registry
│   │   ├── OllamaLLMProvider.cs          # Ollama provider
│   │   ├── LmStudioLLMProvider.cs        # LM Studio (OpenAI-compatible) provider
│   │   ├── OpenAILLMProvider.cs          # OpenAI provider
│   │   ├── GoogleAIStudioLLMProvider.cs  # Google AI Studio provider
│   │   ├── ProviderFromPresets.cs        # Preset-based provider wrappers
│   │   ├── LLMInteractionLogger.cs       # Logs LLM calls to DB
│   │   ├── LLMPresetService.cs           # LLM preset CRUD
│   │   ├── RAGService.cs                 # Embedding search, plot consistency, summaries
│   │   ├── AgentBus.cs                   # Agent-to-agent messaging framework
│   │   ├── WhisperService.cs             # Private messaging
│   │   ├── CombatService.cs              # Initiative, attacks, combat state
│   │   ├── UserIdProvider.cs             # Current user from JWT
│   │   ├── IGameAgent.cs                 # IGameAgent + IGameAgentManager interfaces
│   │   ├── PlotWeaver.cs                 # Orchestrator (280 lines)
│   │   ├── IPlotWeaver.cs                # IPlotWeaver interface
│   │   ├── PlotThreadGenerationStrategy.cs
│   │   ├── PlotThreadAdaptation.cs
│   │   ├── PlotMilestoneSpawning.cs
│   │   ├── PlotOpportunityDetection.cs
│   │   ├── PlotSharedTypes.cs
│   └── Program.cs            # DI, auth, Swagger, CORS, SPA, MediatR, GameAgent recovery
└── Adnd.Client/              # Frontend (React 19 + TS + MUI)
    ├── src/
    │   ├── App.tsx               # Router with auth guards
    │   ├── main.tsx              # Entry point, MUI theme (dark mode)
    │   ├── api/
    │   │   ├── client.ts         # APIClient class (fetch wrapper, auth headers)
    │   │   ├── authHook.tsx      # useAuth context provider
    │   │   ├── gameHooks.ts      # useGames, useGame, useSessions, useGMStatus, useSway
    │   │   ├── hubHook.ts        # useGameHub SignalR wrapper
    │   │   ├── useEntity.ts      # Generic entity CRUD hook
    │   │   └── types/index.ts    # Shared TypeScript types
    │   ├── components/
    │   │   └── Layout.tsx        # App layout wrapper
    │   ├── pages/
    │   │   ├── HomePage.tsx              # Landing page
    │   │   ├── AuthPage.tsx              # Login/Register tabs
    │   │   ├── DashboardPage.tsx         # Game list, create/join (LLM preset, plot seed)
    │   │   ├── GameRoomPage.tsx          # Chat, dice, players, actions, settings, GM status
    │   │   ├── AdminPage.tsx             # NPCs, plots, characters, LLM presets, systems
    │   │   ├── LLMPresetsPage.tsx        # LLM preset management
    │   │   ├── LLMUsagePanel.tsx         # LLM usage statistics
    │   │   ├── PlotBoardTab.tsx          # Plot board (player view)
    │   │   ├── PlotBoardAdminTab.tsx     # Plot board (admin view)
    │   │   ├── CharacterSheetPage.tsx    # View/edit character
    │   │   ├── CharacterCreateWizard.tsx # Multi-step character creation
    │   │   └── CombatTab.tsx             # Combat panel (initiative, attacks)
    │   └── vite-env.d.ts
    └── vite.config.ts          # Dev server with API proxy to localhost:5010
```

## Key Conventions

### Backend (.NET)

- **Controllers** are thin — delegate to services. No business logic in controllers.
- **Services** are registered in `Program.cs` via DI. Each service is a single responsibility class.
- **Models** use EF Core conventions. Primary keys are convention-based (`Id`).
- **Migrations** are auto-applied at startup via `MigrationService`. No manual `dotnet ef` needed in prod.
- **Auth** uses JWT bearer tokens with refresh token rotation. `UserIdProvider` extracts user ID from JWT.
- **Authorization** is game-scoped. `GameAuthorizationService` checks role permissions (Creator/Player/Spectator/Observer).
- **SignalR** hub is `GameHub`. Use `Clients.Group($"game:{gameId}")` for game-scoped broadcasts.
- **LLM providers** implement a pluggable interface. Configure via admin panel, stored in DB.
- **RAG** uses pgvector for embeddings. `RAGService` handles similarity search and consistency checks.
- **AgentBus** is the agentic framework. Agents register handlers and call each other via `CallAgent`.
- **DiceEngine** parses formulas like `4d6kh3+2d4-1`. System-aware resolution.
- **MediatR** event bus decouples GameHub from services. Events are published for all game actions.
- **GameAgent** per-game background processor polls `AgentCalls` table for pending events.
- **GameAgentManager** is a singleton that manages per-game agents and recovers them on startup.
- **Hangfire + PostgreSQL** provides persistent job queue (survives restarts, future external broker replacement).
- **Creator/GM split**: Creator defines plot seed/tone/LLM preset; AI-GM runs the game autonomously.
- **PlotWeaver** handles dynamic plot generation with `PlotWeaver.cs` service and `PlotWeaverHandler.cs` event handler.

### Frontend (React)

- **API Client** (`api/client.ts`) wraps fetch with auth headers. All API calls go through it.
- **Hooks** follow `useXxx` pattern: `useAuth`, `useGames`, `useGame`, `useGameHub`, `useGMStatus`, `useSway`.
- **GameHub** (`hubHook.ts`) manages SignalR connection, groups, and event subscriptions.
- **Pages** are route-based. Auth guards wrap protected routes.
- **MUI** theme is dark-mode by default. All components use MUI theming.
- **Types** are shared in `api/types/index.ts`. Match backend models.
- **Game creation** includes LLM preset selection, plot seed, and game parameters.
- **Game room** shows GM status indicator, pause/resume buttons, and creator sway input.
- **LLM presets** managed via `LLMPresetsPage.tsx` and `LLMUsagePanel.tsx`.
- **Plot board** has separate player (`PlotBoardTab.tsx`) and admin (`PlotBoardAdminTab.tsx`) views.

### Docker

- `docker-compose.yml` runs PostgreSQL + app. No dev-time compose.
- PostgreSQL: `pgvector/pgvector:pg17`, DB `adnd`, user `adnd`.
- App exposes port 5010 → container 8080.
- Init script `docker/init-pgvector.sql` enables pgvector extension.

## Important Files

| File | Purpose |
|------|---------|
| `src/Adnd.Server/Program.cs` | App entry, DI, auth, Swagger, CORS, SPA, MediatR, GameAgent recovery |
| `src/Adnd.Server/Data/AppDbContext.cs` | All EF entities and relationships |
| `src/Adnd.Server/Services/GameEngine.cs` | Core game logic — understand this first |
| `src/Adnd.Server/Services/AgentBus.cs` | Agentic framework — agent registration and dispatch |
| `src/Adnd.Server/Services/LLMProvider.cs` | ILLMProvider interface + BaseLLMProvider + registry |
| `src/Adnd.Server/Services/OllamaLLMProvider.cs` | Ollama provider |
| `src/Adnd.Server/Services/LmStudioLLMProvider.cs` | LM Studio provider |
| `src/Adnd.Server/Services/OpenAILLMProvider.cs` | OpenAI provider |
| `src/Adnd.Server/Services/GoogleAIStudioLLMProvider.cs` | Google AI Studio provider |
| `src/Adnd.Server/Services/RAGService.cs` | Embedding search, plot consistency |
| `src/Adnd.Server/Services/PlotWeaver.cs` | Orchestrator (280 lines) |
| `src/Adnd.Server/Services/IPlotWeaver.cs` | IPlotWeaver interface |
| `src/Adnd.Server/Services/PlotThreadGenerationStrategy.cs` | Thread generation strategies |
| `src/Adnd.Server/Services/PlotThreadAdaptation.cs` | Thread adaptation strategy |
| `src/Adnd.Server/Services/PlotMilestoneSpawning.cs` | Milestone spawning strategy |
| `src/Adnd.Server/Services/PlotOpportunityDetection.cs` | Opportunity detection strategy |
| `src/Adnd.Server/Services/PlotSharedTypes.cs` | Shared types (StoryOpportunity, etc.) |
| `src/Adnd.Server/Services/IGameAgent.cs` | IGameAgent + IGameAgentManager interfaces |
| `src/Adnd.Server/Agent/GameAgent.cs` | GameAgent (per-game) + GameAgentManager (singleton) |
| `src/Adnd.Server/Events/GameEvents.cs` | 25+ MediatR event types |
| `src/Adnd.Server/Handlers/GameEventHandlers.cs` | MediatR notification handlers |
| `src/Adnd.Server/Handlers/PlotWeaverHandler.cs` | PlotWeaver event handler |
| `src/Adnd.Server/Hubs/GameHub.cs` | SignalR hub — publishes events via IMediator |
| `src/Adnd.Server/Controllers/AdminController.cs` | NPCs, plots, characters, LLM presets, systems, GM agent |
| `src/Adnd.Server/Controllers/GamesController.cs` | Games, sessions, players CRUD |
| `src/Adnd.Client/src/api/client.ts` | API client — all backend calls |
| `src/Adnd.Client/src/api/hubHook.ts` | SignalR wrapper |
| `src/Adnd.Client/src/api/gameHooks.ts` | useGames, useGame, useGMStatus, useSway |
| `src/Adnd.Client/src/App.tsx` | Router and auth guards |
| `docker-compose.yml` | Dev/prod container orchestration |

## Running the Project

```bash
# Docker (full stack)
docker compose up -d          # Start
docker compose down           # Stop

# Backend only
cd src/Adnd.Server && dotnet run

# Frontend only
cd src/Adnd.Client && npm run dev

# Build for prod
cd src/Adnd.Client && npm run build
cd ../Adnd.Server && dotnet publish -c Release -o ../publish
```

## Adding a New RPG System

1. Add system definition in `SystemRegistry.cs` — attributes, skills, proficiency levels
2. Update `DiceEngine.cs` if system has unique dice mechanics
3. Add system-specific logic to `GameEngine.cs`
4. Create admin UI in `AdminPage.tsx` for system configuration
5. Update `src/Adnd.Client/src/types/index.ts` with new types

## Adding a New LLM Provider

1. Create a new class implementing the LLM provider interface (see `LLMProvider.cs`)
2. Register it in `LLMPresetService.cs`
3. Add provider selection UI in `AdminPage.tsx`
4. Update `LLMInteractionLogger.cs` to handle the new provider

## Adding a New Agent

1. Add agent type to `AgentType` enum in `AgentCall.cs`
2. Add action type to `AgentAction` enum in `AgentCall.cs` (if needed)
3. Add dispatch handler in `AgentBus.cs` `DispatchCall()` method
4. Define agent call contract in `AgentCall.cs` model
5. Add agent-specific logic in a dedicated service class
6. Update `GameHub.cs` to route agent calls through the bus
7. Add event type in `Events/GameEvents.cs` if it should trigger narrative
8. Add handler in `Handlers/GameEventHandlers.cs` if it should be event-driven

## Common Tasks

### Add a new API endpoint
1. Add endpoint to appropriate controller (`AuthController`, `GamesController`, or `AdminController`)
2. Add service method if business logic is needed
3. Add EF entity if persistence is needed (update `AppDbContext.cs`)
4. Create migration: `dotnet ef migrations add <Name>`
5. Add frontend API call in `api/client.ts`
6. Add hook in `api/gameHooks.ts` if reusable
7. Add UI component/page as needed
8. If it triggers game narrative, add event in `Events/GameEvents.cs` and handler in `Handlers/GameEventHandlers.cs`

### Debugging
- Swagger UI: `http://localhost:5010/swagger`
- Backend logs: `dotnet run` outputs to console
- Frontend: React DevTools, Network tab for API calls
- SignalR: Browser DevTools Network tab → WebSocket frames to `/gamehub`
