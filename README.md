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
- **LLM-powered Game Master** — Pluggable provider system (OpenAI, Anthropic, Ollama, etc.)
- **Agentic framework** — GM, LLM, Dice, RAG, NPC, Player, and System agents collaborate
- **RAG for plot consistency** — Embedding-based context retrieval and continuity checks
- **Real-time multiplayer** — SignalR hub for chat, dice rolls, skill checks, and combat
- **Private whispers** — Player-to-player, player-to-GM, GM-to-player, and group whispers
- **Character management** — Full character sheets, creation wizard, and proficiency tracking
- **Combat system** — Initiative, attacks, skill checks, and per-system resolution
- **JWT authentication** — Register, login, refresh tokens with role-based game access
- **Docker-first deployment** — One-command setup with PostgreSQL + pgvector

## 🚀 Quick Start

### Docker (recommended)

```bash
docker compose up -d
```

Database is created automatically and migrations are applied. The app runs at **http://localhost:5010**.

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
└─────────────────────────────────────────────────────┘
```

## 🎲 Supported RPG Systems

| System | ID | Description |
|--------|----|-------------|
| D&D 5e | `dnd5e` | Attributes, skills, spells, proficiency |
| Pathfinder 2e | `pf2e` | Proficiency levels (untrained → legendary) |
| Call of Cthulhu 7e | `coc7e` | d100 skill rolls, sanity, critical/fumble |
| Custom | `custom` | Define your own via the admin panel |

## 🤖 Agentic Framework

| Agent | Responsibility |
|-------|---------------|
| **GM Agent** | Orchestrator — manages game state, delegates to other agents |
| **LLM Agent** | Narrative generation, plot suggestions, NPC dialogue |
| **Dice Agent** | Dice rolling per system rules |
| **RAG Agent** | Plot consistency checks, context retrieval |
| **NPC Agent** | NPC behavior and dialogue |
| **Player Agent** | Character sheet queries, rules lookups |
| **System Agent** | RPG system rules engine |

```
Player Action → GM Agent → LLM Agent (narrative) → GM Agent → Players
                           ↓
                      RAG Agent (consistency check)
                           ↓
                      Dice Agent (if needed)
```

## 📡 API Overview

All API endpoints are under `/api/`. Swagger docs are available at `/swagger`.

| Area | Endpoints |
|------|-----------|
| **Auth** | `POST /register`, `/login`, `/refresh`, `/logout` · `GET /me` |
| **Games** | CRUD games/sessions/players · invite · join/leave · promote |
| **Admin** | NPCs, plot threads, characters, LLM presets, systems |
| **RAG** | Plot context, similar threads, consistency checks, summaries |
| **Whispers** | Private messaging (player↔player, player↔GM, GM↔player) |
| **Agent Calls** | Agent-to-agent communication and history |
| **Combat** | Initiative, attacks, skill checks, game state |

### SignalR Hub (`/gamehub`)

| Client Method | Server Events |
|---------------|---------------|
| `JoinGame` / `LeaveGame` | `NewMessage`, `NewWhisper` |
| `SendMessage` | `DiceRollResult`, `SkillCheckResult` |
| `RollDice` / `SkillCheck` / `Attack` | `AttackResult`, `PlayerJoined`/`Left` |
| `SendWhisper` / `SendGMWhisper` | `AgentCallStarted`/`Completed` |
| `CallAgent` | `Error` |

## 🗄️ Dice Formula Support

- **Basic:** `1d20`, `2d6`, `4d6`, `1d100`
- **Modifiers:** `1d20+3`, `2d6-1`, `4d6+2d4`
- **Keep/Drop:** `4d6kh3` (keep highest 3), `3d6kl2` (keep lowest 2)
- **Drop:** `dr2` (drop lowest 2), `dh1` (drop highest 1)

## 📁 Project Structure

```
├── src/
│   ├── Adnd.Server/          # ASP.NET Core 10 API
│   │   ├── Controllers/      # Auth, Games, Admin REST endpoints
│   │   ├── Hubs/             # SignalR GameHub
│   │   ├── Services/         # GameEngine, LLMProvider, RAG, AgentBus, etc.
│   │   ├── Models/           # EF Core entities (User, Game, Character, etc.)
│   │   ├── Data/             # DbContext, migrations, auto-migration service
│   │   └── Program.cs        # DI, auth, Swagger, CORS, SPA middleware
│   └── Adnd.Client/          # React 19 + TypeScript + MUI SPA
│       ├── src/
│       │   ├── api/          # APIClient, auth hook, game hooks, SignalR wrapper
│       │   ├── pages/        # Home, Auth, Dashboard, GameRoom, Admin, Character
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

## 📝 Game Master & Creator Roles

| Role | Permissions |
|------|-------------|
| **Creator** | Creates game, sets plot seed, assigns GM, sees all data |
| **Game Master** | Runs game, manages plot, sends whispers, sees agent logs |
| **Player** | Plays the game, whispers (if permitted), sees public chat |
| **Spectator** | Watches the game, no interaction |
| **Observer** | Creator who can watch but not play (read-only) |

## 📜 License

MIT — see [LICENSE](LICENSE) for details.
