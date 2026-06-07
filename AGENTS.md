# AGENTS.md

> Instructions for LLMs and AI assistants working on this project.

## Project Overview

**ADnD (Advanced Dungeon Network)** is a multi-system TTRPG web framework with:
- ASP.NET Core 10 backend (API + SignalR hub)
- React 19 + TypeScript + MUI frontend
- PostgreSQL 17 + pgvector database
- Pluggable LLM provider system with agentic framework

## Directory Structure

```
src/
├── Adnd.Server/              # Backend (.NET 10)
│   ├── Controllers/          # REST API endpoints
│   │   ├── AuthController.cs     # JWT auth, register, login, refresh, logout
│   │   ├── GamesController.cs    # Games, sessions, players CRUD
│   │   └── AdminController.cs    # NPCs, plots, characters, LLM, RAG, systems
│   ├── Hubs/
│   │   └── GameHub.cs            # SignalR: chat, dice, skill checks, attacks, whispers
│   ├── Services/
│   │   ├── AuthService.cs            # JWT + refresh tokens
│   │   ├── GameEngine.cs             # Core game logic (dice, skills, attacks, chars)
│   │   ├── GameAuthorizationService.cs # Role-based game access checks
│   │   ├── DiceEngine.cs             # Dice formula parsing & resolution
│   │   ├── SystemRegistry.cs         # RPG system definitions (dnd5e, pf2e, coc7e, custom)
│   │   ├── LLMProvider.cs            # Pluggable LLM interface + implementations
│   │   ├── LLMInteractionLogger.cs   # Logs LLM calls to DB
│   │   ├── LLMPresetService.cs       # LLM preset CRUD
│   │   ├── RAGService.cs             # Embedding search, plot consistency, summaries
│   │   ├── AgentBus.cs               # Agent-to-agent messaging framework
│   │   ├── WhisperService.cs         # Private messaging
│   │   ├── CombatService.cs          # Initiative, attacks, combat state
│   │   └── UserIdProvider.cs         # Current user from JWT
│   ├── Models/
│   │   ├── User.cs, Player.cs, Game.cs, GameSession.cs
│   │   ├── Character.cs, Message.cs, NPC.cs, PlotThread.cs
│   │   ├── CustomSystemDefinition.cs, RefreshToken.cs, AuthResponse.cs
│   │   ├── AgentCall.cs, Whisper.cs, LLMPreset.cs, LLMInteractionLog.cs
│   │   └── Combat.cs (initiative, attack, skill check entities)
│   ├── Data/
│   │   ├── AppDbContext.cs         # EF Core DbContext with all entities
│   │   ├── MigrationService.cs     # Auto-applies migrations on startup
│   │   ├── GameExtensions.cs       # EF query helpers for games
│   │   └── Migrations/             # EF Core migrations
│   └── Program.cs            # DI, auth, Swagger, CORS, SPA middleware
└── Adnd.Client/              # Frontend (React 19 + TS + MUI)
    ├── src/
    │   ├── App.tsx               # Router with auth guards
    │   ├── main.tsx              # Entry point, MUI theme (dark mode)
    │   ├── api/
    │   │   ├── client.ts         # APIClient class (fetch wrapper, auth headers)
    │   │   ├── authHook.tsx      # useAuth context provider
    │   │   ├── gameHooks.ts      # useGames, useGame, useSessions, etc.
    │   │   ├── hubHook.ts        # useGameHub SignalR wrapper
    │   │   ├── useEntity.ts      # Generic entity CRUD hook
    │   │   └── types/index.ts    # Shared TypeScript types
    │   ├── pages/
    │   │   ├── HomePage.tsx           # Landing page
    │   │   ├── AuthPage.tsx           # Login/Register tabs
    │   │   ├── DashboardPage.tsx      # Game list, create/join
    │   │   ├── GameRoomPage.tsx       # Chat, dice, players, actions, settings
    │   │   ├── AdminPage.tsx          # NPCs, plots, characters, LLM presets, systems
    │   │   ├── CharacterSheetPage.tsx # View/edit character
    │   │   ├── CharacterCreateWizard.tsx # Multi-step character creation
    │   │   └── CombatTab.tsx          # Combat panel (initiative, attacks)
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
- **Authorization** is game-scoped. `GameAuthorizationService` checks role permissions (Creator/GM/Player/Spectator/Observer).
- **SignalR** hub is `GameHub`. Use `Clients.Group($"game:{gameId}")` for game-scoped broadcasts.
- **LLM providers** implement a pluggable interface. Configure via admin panel, stored in DB.
- **RAG** uses pgvector for embeddings. `RAGService` handles similarity search and consistency checks.
- **AgentBus** is the agentic framework. Agents register handlers and call each other via `CallAgent`.
- **DiceEngine** parses formulas like `4d6kh3+2d4-1`. System-aware resolution.

### Frontend (React)

- **API Client** (`api/client.ts`) wraps fetch with auth headers. All API calls go through it.
- **Hooks** follow `useXxx` pattern: `useAuth`, `useGames`, `useGame`, `useGameHub`.
- **GameHub** (`hubHook.ts`) manages SignalR connection, groups, and event subscriptions.
- **Pages** are route-based. Auth guards wrap protected routes.
- **MUI** theme is dark-mode by default. All components use MUI theming.
- **Types** are shared in `api/types/index.ts`. Match backend models.

### Docker

- `docker-compose.yml` runs PostgreSQL + app. No dev-time compose.
- PostgreSQL: `pgvector/pgvector:pg17`, DB `adnd`, user `adnd`.
- App exposes port 5010 → container 8080.
- Init script `docker/init-pgvector.sql` enables pgvector extension.

## Important Files

| File | Purpose |
|------|---------|
| `src/Adnd.Server/Program.cs` | App entry, DI, auth, Swagger, CORS, SPA |
| `src/Adnd.Server/Data/AppDbContext.cs` | All EF entities and relationships |
| `src/Adnd.Server/Services/GameEngine.cs` | Core game logic — understand this first |
| `src/Adnd.Server/Services/AgentBus.cs` | Agentic framework — agent registration and dispatch |
| `src/Adnd.Server/Services/LLMProvider.cs` | LLM provider interface and base |
| `src/Adnd.Server/Services/RAGService.cs` | Embedding search, plot consistency |
| `src/Adnd.Client/src/api/client.ts` | API client — all backend calls |
| `src/Adnd.Client/src/api/hubHook.ts` | SignalR wrapper |
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

1. Register the agent in `AgentBus.cs` with its handler methods
2. Define agent call contract in `AgentCall.cs` model
3. Add agent-specific logic in a dedicated service class
4. Update `GameHub.cs` to route agent calls through the bus

## Common Tasks

### Add a new API endpoint
1. Add endpoint to appropriate controller (`AuthController`, `GamesController`, or `AdminController`)
2. Add service method if business logic is needed
3. Add EF entity if persistence is needed (update `AppDbContext.cs`)
4. Create migration: `dotnet ef migrations add <Name>`
5. Add frontend API call in `api/client.ts`
6. Add hook in `api/gameHooks.ts` if reusable
7. Add UI component/page as needed

### Debugging
- Swagger UI: `http://localhost:5010/swagger`
- Backend logs: `dotnet run` outputs to console
- Frontend: React DevTools, Network tab for API calls
- SignalR: Browser DevTools Network tab → WebSocket frames to `/gamehub`
