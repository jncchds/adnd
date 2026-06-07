# ADnD - Advanced Dungeon Network

A multi-system TTRPG web framework with LLM-powered Game Master assistance, real-time chat, and custom system support.

## Tech Stack
- **Backend:** ASP.NET Core 10
- **Frontend:** React 19 + TypeScript + MUI
- **Database:** PostgreSQL 17 + PGVector
- **Real-time:** SignalR
- **LLM:** Pluggable provider system (OpenAI, Anthropic, Ollama, etc.)

## Quick Start

### 1. Full Stack (Database + App)
```bash
docker compose up -d
# Database is created automatically, migrations applied
# App runs on http://localhost:5010
```

### 2. Development (Hot Reload)
**Backend:**
```bash
cd src/Adnd.Server
dotnet run
```

**Frontend:**
```bash
cd src/Adnd.Client
npm install
npm run dev
```

**Open:** `http://localhost:3000` (Vite dev server proxies API calls to `localhost:5000`)

**For migrations in dev:**
```bash
cd src/Adnd.Server
dotnet tool install -g dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 3. Production Build
```bash
cd src/Adnd.Client
npm run build

cd ../Adnd.Server
dotnet publish -c Release -o ../publish

# Deploy the publish folder
docker compose up -d app
```

## Project Structure
```
├── src/
│   ├── Adnd.Server/          # ASP.NET Core API
│   │   ├── Controllers/      # REST endpoints
│   │   │   ├── AuthController.cs       # Register, login, refresh, logout, me
│   │   │   ├── GamesController.cs      # CRUD games, sessions, players, join/leave
│   │   │   └── AdminController.cs      # NPCs, plots, characters, RAG, systems
│   │   ├── Hubs/
│   │   │   └── GameHub.cs              # SignalR: chat, dice, skill checks, attacks
│   │   ├── Services/         # Business logic
│   │   │   ├── AuthService.cs              # JWT auth, refresh tokens
│   │   │   ├── DiceEngine.cs               # Dice rolling (1d20, 4d6kh3, etc.)
│   │   │   ├── SystemRegistry.cs           # RPG system definitions (D&D5e, PF2e, CoC7e)
│   │   │   ├── GameEngine.cs               # Core game logic (dice, skills, attacks, chars)
│   │   │   ├── LLMProvider.cs              # Pluggable LLM providers (Ollama base)
│   │   │   └── RAGService.cs               # Plot context, similarity, consistency checks
│   │   ├── Models/           # EF Core entities
│   │   │   ├── User.cs, Player.cs, Game.cs, GameSession.cs
│   │   │   ├── Character.cs, Message.cs, NPC.cs, PlotThread.cs
│   │   │   ├── CustomSystemDefinition.cs, RefreshToken.cs
│   │   │   └── AuthResponse.cs (DTOs)
│   │   ├── Data/             # DbContext, migrations
│   │   │   ├── AppDbContext.cs
│   │   │   └── MigrationService.cs
│   │   └── Program.cs        # DI, auth, Swagger, CORS, SPA
│   └── Adnd.Client/          # React SPA
│       ├── src/
│       │   ├── api/
│       │   │   ├── client.ts             # APIClient with auth, games, admin, RAG
│       │   │   ├── authHook.ts           # useAuth context
│       │   │   ├── gameHooks.ts          # useGames, useGame, useSessions, etc.
│       │   │   ├── hubHook.ts            # useGameHub SignalR wrapper
│       │   │   └── client.ts             # TypeScript types
│       │   ├── pages/
│       │   │   ├── HomePage.tsx           # Landing page with features
│       │   │   ├── AuthPage.tsx           # Login/Register with tabs
│       │   │   ├── DashboardPage.tsx      # Game list, create/join dialogs
│       │   │   ├── GameRoomPage.tsx       # Chat, dice, players, actions, settings
│       │   │   └── AdminPage.tsx          # NPCs, plots, characters, consistency
│       │   ├── App.tsx              # Router with auth guards
│       │   └── main.tsx             # Theme provider (dark mode)
│       └── public/
├── docker-compose.yml
└── README.md
```

## API Endpoints

### Auth (`/api/auth`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/register` | Register new user |
| POST | `/login` | Login and get JWT |
| POST | `/refresh` | Refresh access token |
| POST | `/logout` | Revoke refresh token |
| GET | `/me` | Get current user |

### Games (`/api/games`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | List user's games |
| GET | `/{id}` | Get game details |
| POST | `/` | Create new game |
| DELETE | `/{id}` | Delete game |
| POST | `/{id}/invite` | Generate new invite code |
| POST | `/{id}/join` | Join a game |
| POST | `/{id}/leave` | Leave a game |
| GET | `/{id}/sessions` | List game sessions |
| POST | `/{id}/sessions` | Create new session |
| POST | `/{id}/sessions/{sid}/close` | Close a session |
| GET | `/{id}/players` | List game players |
| POST | `/{id}/players/{pid}/promote` | Change player role |

### Admin (`/api/admin`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/games/{gid}/npcs` | List NPCs |
| POST | `/games/{gid}/npcs` | Create NPC |
| PUT | `/npcs/{id}` | Update NPC |
| DELETE | `/npcs/{id}` | Delete NPC |
| GET | `/games/{gid}/plot-threads` | List plot threads |
| POST | `/games/{gid}/plot-threads` | Create plot thread |
| PUT | `/plot-threads/{id}` | Update plot thread |
| POST | `/plot-threads/{id}/add-event` | Add key event |
| GET | `/games/{gid}/characters` | List characters |
| GET | `/characters/{id}` | Get character |
| PUT | `/characters/{id}` | Update character |
| POST | `/games/{gid}/start` | Start game |
| POST | `/games/{gid}/archive` | Archive game |
| GET | `/llm/providers` | LLM provider status |
| GET | `/games/{gid}/plot-context` | Generate plot context |
| POST | `/games/{gid}/rag/similar-threads` | Find similar plot threads |
| GET | `/games/{gid}/rag/consistency` | Check plot consistency |
| GET | `/games/{gid}/rag/summary` | Generate session summary |
| GET | `/systems` | List custom systems |
| POST | `/systems` | Create custom system |
| GET | `/games/{gid}/whispers` | Get whisper history |
| POST | `/games/{gid}/whispers` | Send whisper |
| POST | `/games/{gid}/whispers/gm` | Send GM whisper |
| GET | `/games/{gid}/agent-calls` | Get agent call history |
| GET | `/agent-calls/{id}` | Get specific agent call |
| POST | `/games/{gid}/agent-calls` | Create agent call |
| GET | `/games/{gid}/state` | Get game state |
| PUT | `/games/{gid}/state` | Update game state |

### SignalR Hub (`/gamehub`)
| Method | Description |
|--------|-------------|
| `JoinGame(gameId)` | Join game group |
| `LeaveGame(gameId)` | Leave game group |
| `SendMessage(sessionId, content, type, metadata)` | Send chat message |
| `SendWhisper(targets, content)` | Send whisper (targets: "player:{id}", "all", "group:{name}") |
| `SendGMWhisper(targetPlayerId, content)` | GM whisper to specific player |
| `GetWhisperHistory(gameId, limit)` | Get whisper history |
| `CallAgent(fromAgent, toAgent, action, input, sessionId?)` | Agent-to-agent call |
| `GetAgentCallHistory(gameId, fromAgent?, action?, limit)` | Get agent call log |
| `GetAgentCall(callId)` | Get specific agent call |
| `RollDice(sessionId, formula, playerId)` | Roll dice |
| `SkillCheck(sessionId, skill, playerId, dc)` | Perform skill check |
| `Attack(sessionId, weapon, target, playerId)` | Perform attack |

**Events received:** `NewMessage`, `NewWhisper`, `DiceRollResult`, `SkillCheckResult`, `AttackResult`, `PlayerJoined`, `PlayerLeft`, `AgentCallStarted`, `AgentCallCompleted`, `Error`

## Phases

1. **Docker Infra** ✅ - Database, app container, project structure
2. **Backend Foundation** ✅ - Auth, EF migrations, API endpoints
3. **Game Engine** ✅ - Multi-system support, dice, custom systems
4. **LLM + RAG** ✅ - Plot consistency, embeddings, context generation
5. **SignalR** ✅ - Real-time chat, dice, skill checks, attacks
6. **Admin + Plot** ✅ - Story nodes, NPC management, consistency checks
7. **React UI** ✅ - Auth, dashboard, game room, character sheets
8. **Agentic Framework + Whisper** ✅ - Agent bus, whisper system, agent calls, UI
9. **EF Core Relationships + Admin Enhancements** ✅ - AgentCall parent/child, Player.Character, Whisper.Session, GameState editor, System Registry, Character creation

## RPG Systems Supported

| System | ID | Description |
|--------|----|-------------|
| D&D 5e | `dnd5e` | D&D 5th Edition with attributes, skills, spells |
| Pathfinder 2e | `pf2e` | Pathfinder 2e with proficiency levels |
| Call of Cthulhu 7e | `coc7e` | CoC 7e with d100 skill rolls |

## Agentic Framework

The game is managed by an agentic framework where agents call each other to receive necessary information:

| Agent | Responsibility |
|-------|---------------|
| **GM Agent** | Orchestrator - manages game state, delegates to other agents |
| **LLM Agent** | Narrative generation, plot suggestions, NPC dialogue |
| **Dice Agent** | Dice rolling per system rules |
| **RAG Agent** | Plot consistency checks, context retrieval |
| **NPC Agent** | NPC behavior and dialogue |
| **Player Agent** | Character sheet queries, rules lookups |
| **System Agent** | RPG system rules engine |

**Agent Call Flow:**
```
Player Action → GM Agent → LLM Agent (narrative) → GM Agent → Players
                               ↓
                          RAG Agent (consistency check)
                               ↓
                          Dice Agent (if needed)
```

## Whisper System

Private messaging between players and GM:
- **Player-to-Player**: Players can whisper to each other (GM can see all)
- **Player-to-GM**: Players can whisper to the GM
- **GM-to-Player**: GM can whisper to specific players or groups
- **Group Whispers**: Players can be assigned to whisper groups

## Game Master & Creator Roles

| Role | Permissions |
|------|-------------|
| **Creator** | Creates the game, sets plot seed, can assign GM, sees all data |
| **Game Master** | Runs the game, manages plot, sends whispers, sees agent logs |
| **Player** | Plays the game, can whisper (if permitted), sees public chat |
| **Spectator** | Watches the game, no interaction |
| **Observer** | Creator who can watch but not play (read-only) |

## Dice Formula Support

- **Basic:** `1d20`, `2d6`, `4d6`, `1d100`
- **Modifiers:** `1d20+3`, `2d6-1`, `4d6+2d4`
- **Keep/Drop:** `4d6kh3` (keep highest 3), `3d6kl2` (keep lowest 2)
- **Drop:** `dr2` (drop lowest 2), `dh1` (drop highest 1)
