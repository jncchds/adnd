# ADnD — Advanced Dungeon Network

> A multi-system TTRPG web framework with LLM-powered Game Master assistance, real-time chat, and custom RPG system support.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-purple.svg)](https://dotnet.microsoft.com)
[![React 19](https://img.shields.io/badge/React-19-blue.svg)](https://react.dev)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-red.svg)](https://www.postgresql.org)
[![SignalR](https://img.shields.io/badge/SignalR-Real--time-orange.svg)](https://learn.microsoft.com/aspnet/signalr)

---

## ✨ Features

- **Multi-system TTRPG support** — D&D 5e, Pathfinder 2e, Call of Cthulhu 7e, plus custom systems
- **AI-driven Game Master** — Per-game GameAgent runs the game using the Creator's LLM preset
- **Creator/GM split** — Creator defines plot seed, tone, and LLM preset; AI-GM handles all narrative
- **MediatR event bus** — In-process event-driven architecture between SignalR hub and services
- **Persistent GameAgent** — Per-game background processor with Hangfire + PostgreSQL (survives restarts)
- **Agentic framework** — Creator, GM, LLM, Dice, RAG, NPC, Player, and System agents
- **RAG for plot consistency** — Embedding-based context retrieval and continuity checks
- **Real-time multiplayer** — SignalR hub for chat, dice rolls, skill checks, and combat
- **Private whispers** — Player-to-player, player-to-creator, creator-to-player, and group whispers
- **Creator story sway** — Creators can nudge the AI-GM's narrative direction in real-time
- **Character management** — Full character sheets, creation wizard (8 backgrounds), spell slot tracking, and proficiency tracking
- **Combat system** — Initiative, attacks, skill checks, per-system resolution, action economy (actions/bonus actions/reactions/movements), and condition management
- **Player roll negotiation** — GM can request rolls from players; players confirm/decline with dice dialog
- **Spell slot management** — Visual slot tracker with +/- controls, color-coded by remaining count
- **Condition manager** — Visual condition tracking with emoji icons, duration countdown, and quick-add chip bar
- **Disconnected player detection** — Background service auto-detects stale connections, broadcasts reconnection events
- **JWT authentication** — Register, login, refresh tokens with role-based game access
- **Docker-first deployment** — One-command setup with PostgreSQL + pgvector + Hangfire

## 🚀 Quick Start

### Docker (recommended)

```bash
docker compose up -d
```

Database is created automatically and migrations are applied. The app runs at **http://localhost:5010**.

Environment variables (all optional):

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` or `Production` |
| `ConnectionStrings__Default` | `Host=postgres;Port=5432;Database=adnd;Username=adnd;Password=adnd_secret` | PostgreSQL connection string |
| `JwtSettings__SecretKey` | *(dev only)* | JWT signing key — **must be set in production** (32+ char random string) |
| `JwtSettings__Issuer` | `adnd-server` | JWT token issuer |
| `JwtSettings__Audience` | `adnd-client` | JWT token audience |
| `Encryption__MasterKey` | *(dev only)* | AES-256-GCM key for API key encryption at rest — **must be set in production** (32+ char hex) |
| `Resilience__RetryDelayMs` | `500` | Polly retry delay in ms |
| `Resilience__MaxRetries` | `3` | Max retry attempts for LLM calls |
| `Resilience__FailureThreshold` | `5` | Circuit breaker trip threshold |
| `Resilience__HalfOpenAfterSec` | `30` | Circuit breaker half-open delay in seconds |

### Development

```bash
# Backend
cd src/Adnd.Server
dotnet run

# Frontend (in another terminal)
cd src/Adnd.Client
npm install
npm run dev
```

Open **http://localhost:3000** (Vite proxies API to `localhost:5010`).

### Migrations (dev)

```bash
cd src/Adnd.Server
dotnet ef migrations add <Name>
dotnet ef database update
```

## 📐 Architecture

```
┌─────────────────────────────────────────────────────┐
│                    Adnd.Client (React)              │
│  Auth │ Dashboard │ Game Room │ Admin │ Character   │
└──────────────────────┬──────────────────────────────┘
                       │  REST + SignalR
┌──────────────────────▼──────────────────────────────┐
│                   Adnd.Server (.NET 10)             │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────┐  │
│  │ Controllers│ │  Hubs   │  │    Services      │  │
│  │ Auth /    │  │ GameHub │  │ GameEngine,      │  │
│  │ Games /   │  │         │  │ LLMProvider,     │  │
│  │ Admin     │  │         │  │ RAGService,      │  │
│  └──────────┘  └──────────┘  │ AgentBus, Dice,  │  │
│                               │ Whisper, Combat, │  │
│                               │ SystemRegistry   │  │
│  ┌──────────┐  ┌──────────┐  └──────────────────┘  │
│  │  Models   │  │  Data   │  ┌──────────────────┐  │
│  │ User,     │  │ DbContext│  │ PostgreSQL +     │  │
│  │ Game,     │  │ Migrations│  │ pgvector       │  │
│  │ Character │  └──────────┘  └──────────────────┘  │
│  └──────────┘                                        │
│  ┌──────────────────────────────────────────┐        │
│  │  MediatR Event Bus                       │        │
│  │  Events: GameCreated, DiceRolled, etc.   │        │
│  │  Handlers: GameLifecycle, Chat, Combat   │        │
│  └──────────────────────────────────────────┘        │
│  ┌──────────────────────────────────────────┐        │
│  │  GameAgentManager (singleton)             │        │
│  │  Per-game GameAgents (polling DB for     │        │
│  │  pending AgentCalls)                     │        │
│  │  Recovery on startup via Hangfire+PostgreSQL│       │
│  └──────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────┘
```

## 🎲 Supported RPG Systems

| System | ID | Description |
|--------|----|-------------|
| D&D 5e | `dnd5e` | Attributes, skills, spells, proficiency |
| Pathfinder 2e | `pf2e` | Proficiency levels (untrained → legendary) |
| Call of Cthulhu 7e | `coc7e` | d100 skill rolls, sanity, critical/fumble |
| Custom | `custom` | Define your own via the admin panel |

## 🤖 AI Game Master & Agentic Framework

### Creator / AI-GM Split

| Role | Responsibility |
|------|---------------|
| **Creator** | Defines game premise (plot seed), tone, difficulty, and LLM preset. Can "sway" the story with narrative nudges. |
| **AI-GM** | Runs the game autonomously using the Creator's LLM preset. Generates narrative, manages plot threads, handles combat narration. |
| **Player** | Plays the game, rolls dice, makes skill checks, engages in combat. |

### Agent Architecture

```
Player Action → GameHub (SignalR)
              → MediatR Event (e.g., DiceRolled)
              → GameAgentManager.GetOrCreate(gameId)
              → GameAgent (per-game, polls AgentCalls DB table)
              → AgentBus.ExecuteCall()
              → LLM Agent (narrative) → GM Agent → Players via SignalR
                           ↓
                      RAG Agent (consistency check)
                           ↓
                      Dice Agent (if needed)
```

| Agent | Responsibility |
|-------|---------------|
| **Creator Agent** | Sends narrative nudges (story sway) to the GM |
| **GM Agent** | Per-game orchestrator — processes events, delegates to other agents |
| **LLM Agent** | Narrative generation, plot suggestions, NPC dialogue |
| **Dice Agent** | Dice rolling per system rules |
| **RAG Agent** | Plot consistency checks, context retrieval |
| **NPC Agent** | NPC behavior and dialogue |
| **Player Agent** | Character sheet queries, rules lookups |
| **System Agent** | RPG system rules engine |

### GameAgent Lifecycle

```
Game Created → GameAgentManager.GetOrCreate(gameId)
             → GameAgent.StartAsync(gameId, creatorId)
             → Processing loop polls AgentCalls table
             → Each event → AgentBus.ExecuteCall() → LLM call
             → Creator can pause/resume at any time
             → On restart: GameAgentManager.StartAllActiveGamesAsync() recovers agents
```

## 📡 API Overview

All API endpoints are under `/api/`. Swagger docs are available at `/swagger`.

| Area | Endpoints |
|------|-----------|
| **Auth** | `POST /register`, `/login`, `/refresh`, `/logout` · `GET /me` |
| **Games** | CRUD games/sessions/players · invite · join/leave · start · archive |
| **Admin** | NPCs, plot threads, characters, LLM presets, systems |
| **GM Agent** | `GET/POST /games/{id}/gm-status`, `/gm/pause`, `/gm/resume`, `/sway` |
| **RAG** | Plot context, similar threads, consistency checks, summaries |
| **Whispers** | Private messaging (player↔player, player↔creator, creator↔player) |
| **Agent Calls** | Agent-to-agent communication and history |
| **Combat** | Initiative, attacks, skill checks, game state |

### SignalR Hub (`/gamehub`)

| Client Method | Server Events |
|---------------|---------------|
| `JoinGame` / `LeaveGame` | `NewMessage`, `NewWhisper` |
| `SendMessage` | `DiceRollResult`, `SkillCheckResult` |
| `RollDice` / `SkillCheck` / `Attack` | `AttackResult`, `PlayerJoined`/`Left` |
| `SendWhisper` / `SendCreatorWhisper` | `AgentCallStarted`/`Completed` |
| `CallAgent` | `Error` |
| `SendHeartbeat` | — |
| `StartCombat` / `EndCombat` | `CombatStarted`, `CombatEnded`, `CombatLogUpdated` |
| `AddParticipant` / `RollInitiative` | `CombatParticipantAdded`, `InitiativeRolled` |
| `CombatAttack` / `CombatSaveThrow` | `CombatAttack`, `CombatSaveThrow` |
| `CombatApplyCondition` | `ConditionApplied`, `ConditionRemoved` |
| `SpendAction` / `RefreshActions` | `ActionSpent`, `ActionsRefreshed` |
| `ConfirmPlayerRoll` / `DeclinePlayerRoll` | `PlayerRollRequested`, `PlayerRollConfirmed`, `PlayerRollDeclined` |
| `ConfirmToolCall` | `ToolCallConfirmed` |
| `SendHeartbeat` | — |
| `StartCombat` / `EndCombat` | `CombatStarted`, `CombatEnded`, `CombatLogUpdated` |
| `AddParticipant` / `RollInitiative` | `CombatParticipantAdded`, `InitiativeRolled` |
| `CombatAttack` / `CombatSaveThrow` | `CombatAttack`, `CombatSaveThrow` |
| `CombatApplyCondition` | `ConditionApplied`, `ConditionRemoved` |
| `SpendAction` / `RefreshActions` | `ActionSpent`, `ActionsRefreshed` |
| `ConfirmPlayerRoll` / `DeclinePlayerRoll` | `PlayerRollRequested`, `PlayerRollConfirmed`, `PlayerRollDeclined` |
| `ConfirmToolCall` | `ToolCallConfirmed` |

## 🗄️ Dice Formula Support

- **Basic:** `1d20`, `2d6`, `4d6`, `1d100`
- **Modifiers:** `1d20+3`, `2d6-1`, `4d6+2d4`
- **Keep/Drop:** `4d6kh3` (keep highest 3), `3d6kl2` (keep lowest 2)
- **Drop:** `dr2` (drop lowest 2), `dh1` (drop highest 1)

## 📁 Project Structure

```
├── src/
│   ├── Adnd.Server/          # ASP.NET Core 10 API
│   │   ├── Agent/            # Per-game GameAgent + GameAgentManager
│   │   ├── Controllers/      # Auth, Games, Admin REST endpoints
│   │   ├── Data/             # DbContext, migrations, auto-migration service
│   │   ├── Events/           # MediatR event types (30+)
│   │   ├── Handlers/         # MediatR event handlers + PlotWeaverHandler
│   │   ├── Hubs/             # SignalR GameHub (partial classes by concern)
│   │   ├── Models/           # EF Core entities (User, Game, Character, etc.)
│   │   ├── Services/         # GameEngine, LLMProvider, RAG, AgentBus, etc.
│   │   └── Program.cs        # DI, auth, Swagger, CORS, SPA middleware
│   └── Adnd.Client/          # React 19 + TypeScript + MUI SPA
│       ├── src/
│       │   ├── api/          # APIClient, auth hook, game hooks, SignalR wrapper
│       │   ├── components/   # Layout, shared UI components
│       │   ├── pages/        # Home, Auth, Dashboard, GameRoom, Admin, LLM presets
│       │   └── types/        # Shared TypeScript types
│       └── vite.config.ts    # Dev server with API proxy
├── docker-compose.yml        # PostgreSQL + pgvector + app
├── docker/                   # Docker helper scripts
└── .github/                  # GitHub workflows
```

## 🔧 Tech Stack

- **Backend:** ASP.NET Core 10, EF Core, JWT Auth
- **Frontend:** React 19, TypeScript, MUI, Vite
- **Database:** PostgreSQL 17 + pgvector (embeddings)
- **Real-time:** SignalR
- **LLM:** Pluggable provider system (OpenAI, Anthropic, Ollama, etc.)
- **Deployment:** Docker Compose

## 📝 Creator / AI-GM / Player Roles

| Role | Permissions |
|------|-------------|
| **Creator** | Creates game, sets plot seed/tone/LLM preset, sways story, pauses/resumes GM |
| **AI-GM** | Runs the game autonomously — generates narrative, manages plot, handles combat |
| **Player** | Plays the game, rolls dice, makes skill checks, engages in combat |
| **Spectator** | Watches the game, no interaction |
| **Observer** | Creator who can watch but not play (read-only) |

## 📜 License

MIT — see [LICENSE](LICENSE) for details.
