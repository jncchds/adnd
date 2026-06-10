# ADnD — Advanced Dungeon Network

## Recreation Guide

This document contains all the information needed to recreate the ADnD project from scratch.

---

## 1. Project Overview

**ADnD** is a multi-system TTRPG (Tabletop Role-Playing Game) web framework featuring:

- **ASP.NET Core 10** backend with REST API + SignalR real-time hub + MediatR event bus
- **React 19** + **TypeScript** + **MUI 7** frontend with dark-mode theme
- **PostgreSQL 17** + **pgvector** database for embeddings and vector similarity search
- Per-game **GameAgent** background processor with Hangfire-compatible persistence (database-backed)
- **Creator / AI-GM** split architecture: Creator defines plot seed/tone/LLM preset; AI-GM runs the game autonomously
- Pluggable **LLM provider system** (Ollama, LM Studio, OpenAI, Google AI Studio) with agentic framework
- Real-time **player roll negotiation** (GM requests rolls, players confirm/decline)
- Full **action economy** (actions/bonus actions/reactions/movements tracking)
- **Spell slot management** with visual tracker
- **Condition manager** with emoji icons and duration countdown
- **Disconnected player detection** via SignalR heartbeat
- **PlotWeaver** — automatic plot thread generation, evolution, and milestone tracking
- **RAG (Retrieval-Augmented Generation)** for plot consistency and context retrieval

### Architecture Diagram

```
┌─────────────────┐     HTTPS/WSS     ┌──────────────────────────────────┐
│  React Frontend  │ ◄──────────────► │     ASP.NET Core 10 Server       │
│  (port 3000 dev) │                   │     (port 5010 / 8080 prod)      │
└─────────────────┘                   │                                    │
                                      │  REST API (Controllers)            │
                                      │  SignalR Hub (GameHub)           │
                                      │  MediatR Event Bus               │
                                      │  GameAgent (per-game)            │
                                      └────────┬─────────────────────────┘
                                               │
                                               ▼
                                      ┌────────────────┐
                                      │ PostgreSQL 17   │
                                      │ + pgvector      │
                                      └────────────────┘
```

---

## 2. Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | 10.0+ | Backend compilation |
| [Node.js](https://nodejs.org) | 20.x | Frontend build |
| [Docker / Docker Compose](https://docker.com) | latest | PostgreSQL + pgvector |
| [pgvector/pgvector:pg17](https://github.com/pgvector/pgvector) | pg17 | PostgreSQL with vector extension |

---

## 3. Repository Structure

```
adnd/
├── docker-compose.yml              # Full-stack orchestration
├── src/
│   ├── Adnd.Server/                # Backend (.NET 10)
│   │   ├── Adnd.Server.csproj      # Project file (see §3.1)
│   │   ├── Program.cs              # App entry, DI, auth, Swagger
│   │   ├── appsettings.json        # Dev config
│   │   ├── appsettings.Production.json  # Prod config
│   │   ├── Dockerfile              # Multi-stage build (client + server)
│   │   ├── Controllers/            # REST API endpoints (25+ controllers)
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs     # EF Core DbContext + all entities
│   │   │   ├── MigrationService.cs # Auto-applies migrations on startup
│   │   │   ├── GameExtensions.cs   # EF query helpers for games
│   │   │   └── Migrations/         # EF Core migrations (auto-generated)
│   │   ├── Events/
│   │   │   └── GameEvents.cs       # 30+ MediatR event types
│   │   ├── Handlers/
│   │   │   ├── GameEventHandlers.cs    # MediatR notification handlers
│   │   │   └── PlotWeaverHandler.cs    # PlotWeaver event handler
│   │   ├── Hubs/
│   │   │   ├── GameHub.cs              # SignalR hub (core)
│   │   │   ├── GameHub.ChatMethods.cs  # Chat hub methods
│   │   │   ├── GameHub.Combat.cs       # Combat hub methods
│   │   │   ├── GameHub.Dice.cs         # Dice hub methods
│   │   │   ├── GameHub.Spells.cs       # Spell hub methods
│   │   │   ├── GameHub.ToolCalls.cs    # Player roll negotiation
│   │   │   ├── GameHub.Whispers.cs     # Whisper hub methods
│   │   │   ├── GameHub.JoinLeave.cs    # Join/leave hub methods
│   │   │   ├── GameHub.Inventory.cs    # Inventory hub methods
│   │   │   ├── GameHub.Grid.cs         # Grid/map hub methods
│   │   │   ├── GameHub.Combat.cs       # Combat hub methods
│   │   │   ├── GameHub.AICombat.cs     # AI combat hub methods
│   │   │   ├── GameHub.SAN.cs          # SAN hub methods (CoC)
│   │   │   ├── GameHub.RestSystem.cs   # Rest hub methods
│   │   │   ├── GameHub.Progression.cs  # Progression hub methods
│   │   │   ├── GameHub.CharacterCreation.cs
│   │   │   ├── GameHub.HelperMethods.cs
│   │   │   ├── GameHub.Helpers.cs
│   │   │   ├── GameHub.FlavorText.cs
│   │   │   ├── GameHub.SystemSpecific.cs
│   │   │   ├── GameHub.AgentMethods.cs
│   │   │   └── ResponseDTOs.cs         # SignalR response DTOs
│   │   ├── Models/                     # Entity models (20+ classes)
│   │   ├── Services/                   # Business logic (40+ services)
│   │   ├── Agent/
│   │   │   └── GameAgent.cs            # GameAgent + GameAgentManager
│   │   └── Properties/
│   │       └── launchSettings.json     # Dev launch config
│   └── Adnd.Client/                  # Frontend (React 19 + TS + MUI)
│       ├── package.json              # Dependencies (see §3.2)
│       ├── tsconfig.json             # TypeScript config
│       ├── vite.config.ts            # Vite dev server + proxy
│       ├── index.html                # SPA entry point
│       └── src/
│           ├── main.tsx              # Entry point + MUI theme
│           ├── App.tsx               # Router with auth guards
│           ├── api/
│           │   ├── client.ts         # APIClient class (fetch wrapper)
│           │   ├── authHook.tsx      # useAuth context provider
│           │   ├── gameHooks.ts      # useGames, useGame, useSessions
│           │   ├── hubHook.ts        # SignalR wrapper (useGameHub)
│           │   ├── toolCallsHook.ts  # Tool call state management
│           │   ├── gameToolsHook.ts  # Game tools hook
│           │   ├── useEntity.ts      # Generic entity CRUD hook
│           │   └── types/
│           │       └── index.ts      # Shared TypeScript types (300+ lines)
│           ├── components/
│           │   ├── AppShell.tsx      # App shell layout
│           │   ├── Layout.tsx        # App layout wrapper
│           │   ├── SidePanel.tsx     # Collapsible side panel
│           │   ├── WelcomeScreen.tsx # Welcome/onboarding screen
│           │   ├── ToolCallBanner.tsx     # Tool call notifications
│           │   ├── PlayerRollDialog.tsx   # Player dice roll dialog
│           │   └── MarkdownRenderer.tsx   # React-markdown wrapper
│           └── pages/
│               ├── HomePage.tsx              # Landing page
│               ├── AuthPage.tsx              # Login/Register tabs
│               ├── DashboardPage.tsx         # Game list, create/join
│               ├── GameRoomPage.tsx          # Chat, dice, players, actions
│               ├── AdminPage.tsx             # NPCs, plots, characters, LLM
│               ├── CharacterSheetPage.tsx    # View/edit character
│               ├── CharacterCreateWizard.tsx # Multi-step character creation
│               ├── CombatTab.tsx             # Combat panel (initiative, attacks)
│               ├── CombatLogViewerPage.tsx   # Combat log viewer
│               ├── GameStatePage.tsx         # Game state management
│               ├── DiceHistoryTab.tsx        # Dice roll history
│               ├── GMToolPanel.tsx           # GM tool panel
│               ├── LLMPresetsPage.tsx        # LLM preset management
│               ├── LLMUsagePanel.tsx         # LLM usage statistics
│               ├── PlotBoardTab.tsx          # Plot board (player view)
│               ├── PlotBoardAdminTab.tsx     # Plot board (admin view)
│               ├── SystemsPage.tsx           # RPG system management
│               └── UserSettingsPage.tsx      # User settings
├── AGENTS.md                   # Agent instructions for LLMs
├── IDEAS.md                    # Roadmap and priority matrix
├── README.md                   # Project README
└── RECREATION_GUIDE.md         # This file
```

---

## 3.1 Backend Project File (`Adnd.Server.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <!-- Authentication -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />

    <!-- Database -->
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />

    <!-- MediatR -->
    <PackageReference Include="MediatR" Version="12.0.0" />
    <PackageReference Include="MediatR.Extensions.Microsoft.DependencyInjection" Version="11.0.0" />

    <!-- Swagger -->
    <PackageReference Include="Swashbuckle.AspNetCore" Version="7.0.0" />

    <!-- Resilience (Polly) -->
    <PackageReference Include="Microsoft.Extensions.Resilience" Version="10.0.0" />
    <PackageReference Include="Polly" Version="8.0.0" />

    <!-- HttpClient factory -->
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="10.0.0" />

    <!-- JSON -->
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.0" />

    <!-- IP Network parsing -->
    <PackageReference Include="IPNetwork2" Version="2.1.11" />
  </ItemGroup>

</Project>
```

---

## 3.2 Frontend Package.json

```json
{
  "name": "adnd-client",
  "private": true,
  "version": "0.0.1",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "@emotion/react": "^11.14.0",
    "@emotion/styled": "^11.14.0",
    "@microsoft/signalr": "^8.0.7",
    "@mui/icons-material": "^7.1.0",
    "@mui/material": "^7.1.0",
    "react": "^19.1.0",
    "react-dom": "^19.1.0",
    "react-markdown": "^10.1.0",
    "react-router-dom": "^7.1.0",
    "remark-gfm": "^4.0.1"
  },
  "devDependencies": {
    "@types/node": "^22.13.4",
    "@types/react": "^19.1.4",
    "@types/react-dom": "^19.1.5",
    "@vitejs/plugin-react": "^4.4.1",
    "typescript": "~5.7.2",
    "vite": "^6.1.0"
  }
}
```

---

## 3.3 Frontend TypeScript Config (`tsconfig.json`)

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "isolatedModules": true,
    "moduleDetection": "force",
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "baseUrl": ".",
    "paths": {
      "@/*": ["src/*"]
    }
  },
  "include": ["src"]
}
```

---

## 3.4 Vite Config (`vite.config.ts`)

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom'],
          mui: ['@mui/material'],
          signalr: ['@microsoft/signalr'],
        },
      },
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'https://localhost:5001',
        secure: false,
      },
      '/gamehub': {
        target: 'https://localhost:5001',
        ws: true,
      }
    }
  }
})
```

---

## 4. Docker Setup

### `docker-compose.yml`

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg17
    container_name: adnd-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-adnd}
      POSTGRES_USER: ${POSTGRES_USER:-adnd}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-adnd_secret}
    volumes:
      - adnd-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-adnd}"]
      interval: 5s
      timeout: 5s
      retries: 5

  app:
    build:
      context: .
      dockerfile: src/Adnd.Server/Dockerfile
    container_name: adnd-app
    restart: unless-stopped
    ports:
      - "${APP_PORT:-5010}:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Production}
      - ConnectionStrings__Default=Host=postgres;Port=5432;Database=${POSTGRES_DB:-adnd};Username=${POSTGRES_USER:-adnd};Password=${POSTGRES_PASSWORD:-adnd_secret}
      - JwtSettings__SecretKey=${JWT_SECRET_KEY:-CHANGE_ME_TO_A_LONG_RANDOM_STRING}
      - JwtSettings__Issuer=${JWT_ISSUER:-adnd-server}
      - JwtSettings__Audience=${JWT_AUDIENCE:-adnd-client}
      - Encryption__MasterKey=${ENCRYPTION_MASTER_KEY:-CHANGE_ME_TO_A_32_BYTE_MIN_KEY_FOR_AES256}
      - Resilience__RetryDelayMs=${RESILIENCE_RETRY_DELAY_MS:-500}
      - Resilience__MaxRetries=${RESILIENCE_MAX_RETRIES:-3}
      - Resilience__FailureThreshold=${RESILIENCE_FAILURE_THRESHOLD:-5}
      - Resilience__HalfOpenAfterSec=${RESILIENCE_HALF_OPEN_AFTER_SEC:-30}
      - RateLimiting__UseForwardedHeaders=${RATE_LIMITING_USE_FORWARDED_HEADERS:-true}
      - RateLimiting__TrustAllProxies=${RATE_LIMITING_TRUST_ALL_PROXIES:-true}
      - RateLimiting__GlobalLimit=${RATE_LIMITING_GLOBAL_LIMIT:-100}
      - RateLimiting__GlobalWindowMinutes=${RATE_LIMITING_GLOBAL_WINDOW:-1}
      - RateLimiting__AuthLimit=${RATE_LIMITING_AUTH_LIMIT:-30}
      - RateLimiting__AuthWindowMinutes=${RATE_LIMITING_AUTH_WINDOW:-1}
      - RateLimiting__LlmPresetLimit=${RATE_LIMITING_LLM_PRESET_LIMIT:-30}
    depends_on:
      postgres:
        condition: service_healthy

volumes:
  adnd-data:
```

### `src/Adnd.Server/Dockerfile`

```dockerfile
# Stage 1: Build the React Client
FROM node:20-alpine AS build-client
WORKDIR /app
COPY src/Adnd.Client/package.json src/Adnd.Client/package-lock.json* ./
RUN npm ci
COPY src/Adnd.Client/ ./
RUN npm run build

# Stage 2: Build the .NET Server
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build-server
WORKDIR /src
COPY src/Adnd.Server/Adnd.Server.csproj ./
RUN dotnet restore
COPY src/Adnd.Server/ .
RUN dotnet publish "Adnd.Server.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:SymbolPackageFormat=none

# Stage 3: Minimal runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache krb5-libs
COPY --from=build-server /app/publish .
COPY --from=build-client /app/dist ./wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "Adnd.Server.dll"]
```

---

## 5. Configuration

### `appsettings.json` (Development)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=adnd;Username=adnd;Password=adnd_secret;MaxPoolSize=100;MinPoolSize=10;Connection Idle Lifetime=300;Connection Prune Lifetime=60"
  },
  "JwtSettings": {
    "SecretKey": "CHANGE_ME_TO_A_LONG_RANDOM_STRING_IN_PRODUCTION",
    "Issuer": "adnd-server",
    "Audience": "adnd-client"
  },
  "Encryption": {
    "MasterKey": "CHANGE_ME_TO_A_32_BYTE_MIN_KEY_FOR_AES256"
  },
  "Resilience": {
    "RetryDelayMs": 500,
    "MaxRetries": 3,
    "FailureThreshold": 5,
    "HalfOpenAfterSec": 30
  }
}
```

### `appsettings.Production.json` (Production)

Same structure but with `Host=postgres` in the connection string.

### Environment Variables (Production)

| Variable | Purpose | Required in Prod? |
|----------|---------|-------------------|
| `JWT_SECRET_KEY` | JWT signing key (HS256) | ✅ Yes — 32+ char random string |
| `ENCRYPTION_MASTER_KEY` | AES-256-GCM key for API key encryption at rest | ✅ Yes — 32+ char hex |
| `ConnectionStrings__Default` | PostgreSQL connection | ✅ Yes |
| `JWT_ISSUER` | JWT issuer claim | Optional (default: `adnd-server`) |
| `JWT_AUDIENCE` | JWT audience claim | Optional (default: `adnd-client`) |
| `RESILIENCE_RETRY_DELAY_MS` | Polly retry delay | Optional (default: 500) |
| `RESILIENCE_MAX_RETRIES` | Max LLM call retries | Optional (default: 3) |
| `RESILIENCE_FAILURE_THRESHOLD` | Circuit breaker trip threshold | Optional (default: 5) |
| `RESILIENCE_HALF_OPEN_AFTER_SEC` | Circuit breaker half-open delay | Optional (default: 30) |
| `RATE_LIMITING_GLOBAL_LIMIT` | Global request limit per window | Optional (default: 100) |
| `RATE_LIMITING_AUTH_LIMIT` | Auth endpoint rate limit | Optional (default: 30) |
| `RATE_LIMITING_LLM_PRESET_LIMIT` | LLM preset rate limit | Optional (default: 30) |

---

## 6. Database Schema

### Entity Relationships

```
User (1) ──── (N) RefreshToken
User (1) ──── (N) LLMPreset
User (1) ──── (N) LLMInteractionLog
User (1) ──── (N) AuditLog
User (1) ──── (N) SessionNote (as Creator)
User (1) ──── (N) PromptTemplate
User (1) ──── (N) GameTemplate
User (1) ──── (N) Player

Game (1) ──── (1) LLMPreset (nullable)
Game (1) ──── (N) Player
Game (1) ──── (N) GameSession
Game (1) ──── (N) NPC
Game (1) ──── (N) PlotThread
Game (1) ──── (N) PlotReview
Game (1) ──── (N) AgentCall
Game (1) ──── (N) GMToolCall
Game (1) ──── (N) GameTemplate (as owner)
Game (1) ──── (N) CustomSystemDefinition
Game (1) ──── (N) PromptTemplate

Player (1) ──── (1) Character (one-to-one)
Player (1) ──── (N) Whisper (as sender)
Player (1) ──── (N) CombatParticipant
Player (1) ──── (N) Message (as sender)

GameSession (1) ──── (N) Message
GameSession (1) ──── (N) Whisper
GameSession (1) ──── (N) SessionNote
GameSession (1) ──── (N) AgentCall
GameSession (1) ──── (N) GMToolCall
GameSession (1) ──── (N) Combat

Character (1) ──── (1) Player
Character ──── (JSON) Attributes, Skills, Inventory, Spells, Conditions, CustomFields

NPC (1) ──── (N) PlotThread (as owner)

PlotThread (N) ──── (1) Game
PlotThread ──── (N) MilestoneEvent (embedded JSON)

Combat (1) ──── (N) CombatParticipant
Combat (1) ──── (N) CombatEvent
CombatParticipant ──── (JSON) Conditions, SavingThrows, DeathSaveState, TemporaryHP
CombatEvent ──── (JSON) Metadata

LLMPreset (1) ──── (1) User
LLMInteractionLog (1) ──── (1) LLMPreset (nullable)
LLMInteractionLog (1) ──── (1) User
LLMInteractionLog ──── (JSON text) SystemPrompt, UserPrompt, Response, RequestJson, ResponseJson

Whisper (1) ──── (1) GameSession
Whisper ──── (text) Targets (comma-separated or JSON)

AgentCall (1) ──── (1) Game
AgentCall (1) ──── (1) GameSession (nullable)
AgentCall (N) ──── (1) AgentCall (parent) — hierarchical call tree

GMToolCall (1) ──── (1) Game
GMToolCall (1) ──── (1) GameSession (nullable)
GMToolCall (N) ──── (1) GMToolCall (parent) — hierarchical call tree
GMToolCall ──── (JSON) Arguments, Result

CustomSystemDefinition (1) ──── (1) Game
PromptTemplate (1) ──── (1) Game
PromptTemplate (1) ──── (1) User
GameTemplate (1) ──── (1) User
GameTemplate (1) ──── (1) LLMPreset (nullable)

AuditLog (1) ──── (1) User
SessionNote (1) ──── (1) GameSession
SessionNote (1) ──── (1) User (as Creator)
```

### All Entity Models

#### `User`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK, default: `Guid.NewGuid()` |
| `Email` | string | Unique index |
| `PasswordHash` | string | BCrypt hashed |
| `DisplayName` | string? | |
| `CreatedAt` | DateTime | UTC, default: `DateTime.UtcNow` |
| `LastLoginAt` | DateTime? | |

#### `Game`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `CreatorId` | Guid | FK → User |
| `LLMPresetId` | Guid? | FK → LLMPreset (SET NULL on delete) |
| `GMStatus` | GMStatus enum | Idle/Running/Paused |
| `LastGMAction` | string? | |
| `LastGMActionAt` | DateTime? | |
| `Name` | string | |
| `SystemId` | string | Default: `"dnd5e"` |
| `SystemVersion` | string? | |
| `CustomSystemJson` | string? | JSON for custom systems |
| `Status` | GameStatus enum | Draft/Active/Archived/Finished |
| `CreatedAt` | DateTime | UTC |
| `StartedAt` | DateTime? | |
| `EndedAt` | DateTime? | |
| `InviteCode` | string? | Unique index (non-null filter) |
| `Language` | string | Default: `"English"` |
| `PlotSeed` | string? | JSON: initial plot, tone, themes |
| `GameParameters` | string? | JSON: difficulty, tone, pacing |
| `GameState` | string? | JSON: current game state |

#### `Player`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `UserId` | Guid | FK → User |
| `CharacterName` | string | |
| `Role` | PlayerRole enum | Creator/Player/Spectator/Observer |
| `Status` | PlayerStatus enum | Active/Disconnected/Left |
| `JoinedAt` | DateTime | UTC |
| `LeftAt` | DateTime? | |
| `CanWhisper` | bool | Default: `true` |
| `WhisperGroups` | List<string> | e.g., `"party"`, `"stealth"` |

Unique index: `{GameId, UserId}`

#### `Character`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `PlayerId` | Guid | FK → Player (one-to-one) |
| `Name` | string | |
| `Class` | string | |
| `Level` | int | |
| `ProficiencyBonus` | int | |
| `CurrentHP` | int | |
| `MaxHP` | int | |
| `Attributes` | JsonElement | System-specific stats |
| `Skills` | JsonElement | Skill values |
| `Inventory` | JsonElement | Items |
| `Spells` | JsonElement | Spells |
| `Conditions` | JsonElement | Active conditions |
| `CustomFields` | JsonElement | Custom data |
| `Background` | string? | |
| `BackgroundSkills` | JsonElement? | |
| `BackgroundProficiencies` | JsonElement? | |
| `BackgroundFeatures` | JsonElement? | |
| `SpellSlots` | JsonElement? | `{ "1": { "total": 4, "remaining": 4 } }` |
| `SpellcastingAbility` | JsonElement? | |
| `SpellSaveDC` | int? | |
| `SpellAttackBonus` | int? | |
| `UpdatedAt` | DateTime | UTC |

#### `GameSession`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `Title` | string | |
| `Description` | string? | |
| `PlotNodes` | string? | JSON: story node tree |
| `GameState` | string? | JSON: current state |
| `StartedAt` | DateTime | UTC |
| `EndedAt` | DateTime? | |

#### `Message`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `SessionId` | Guid | FK → GameSession |
| `PlayerId` | Guid? | FK → Player |
| `Content` | string | |
| `Type` | MessageType enum | 0–102, see full enum below |
| `Metadata` | JsonElement | JSONB |
| `IsOOC` | bool | Default: `false` |
| `WhisperFromId` | Guid? | FK → Player |
| `WhisperToId` | Guid? | FK → Player |
| `WhisperTarget` | string? | `"all"`, `"player:{id}"`, `"group:{name}"` |
| `Embedding` | float[]? | PGVector vector column |
| `CreatedAt` | DateTime | UTC |

Index: `{SessionId, CreatedAt}` descending

#### `NPC`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `Name` | string | |
| `Description` | string? | |
| `Attributes` | JsonElement | |
| `Skills` | JsonElement | |
| `Inventory` | JsonElement | |
| `Spells` | JsonElement | |
| `PlotThreadId` | Guid? | FK → PlotThread |
| `CreatedAt` | DateTime | UTC |

#### `PlotThread`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `Title` | string | |
| `Description` | string | |
| `Category` | PlotThreadCategory enum | General/Faction/Mystery/Personal/Threat/WorldEvent/Relationship |
| `Status` | PlotThreadStatus enum | Active/Resolved/Abandoned |
| `Momentum` | float (real) | -10 (abandoned) to +10 (urgent) |
| `RelevanceScore` | float (real) | Computed by PlotWeaver |
| `NextMilestone` | string? | |
| `Foreshadowing` | string? | |
| `AdaptationHistory` | List<string> | JSONB |
| `MilestoneEvents` | List<MilestoneEvent> | JSONB, embedded |
| `IsDynamic` | bool | |
| `KeyEventMessageIds` | List<Guid> | |
| `Embedding` | float[]? | PGVector vector column |
| `CreatedAt` | DateTime | UTC |
| `UpdatedAt` | DateTime? | |

Index: `{GameId, Status}`, `{GameId, Title}`

#### `MilestoneEvent` (embedded in JSONB)
| Field | Type |
|-------|------|
| `Id` | Guid |
| `Title` | string |
| `Description` | string |
| `Status` | MilestoneStatus enum |
| `TriggeredAt` | DateTime? |
| `CompletedAt` | DateTime? |
| `CreatedAt` | DateTime |

#### `Combat`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `SessionId` | Guid? | FK → GameSession |
| `Name` | string? | e.g., "Goblin Ambush" |
| `Status` | CombatStatus enum | Active/Paused/Finished |
| `CurrentRound` | int | Default: 0 |
| `CurrentTurnIndex` | int | Index into Participants |
| `InitiativeCount` | int | Tiebreak counter |
| `StartedAt` | DateTime | UTC |
| `EndedAt` | DateTime? | |
| `Notes` | JsonElement? | JSON: grid settings, custom data |

Index: `{GameId, Status}`

#### `CombatParticipant`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `CombatId` | Guid | FK → Combat |
| `ParticipantType` | string | `"Player"` or `"NPC"` |
| `PlayerId` | Guid? | FK → Player |
| `NpcId` | Guid? | FK → NPC |
| `DisplayName` | string | |
| `Initiative` | int | |
| `InitiativeCount` | int | Tiebreak (lower = earlier) |
| `CurrentHP` | int | |
| `MaxHP` | int | |
| `AC` | int | |
| `InitiativeBonus` | int? | Dex mod or other |
| `Conditions` | JsonElement | JSONB array |
| `TemporaryHP` | JsonElement? | JSONB |
| `SavingThrows` | JsonElement? | JSONB |
| `DeathSaveState` | JsonElement? | JSONB |
| `Notes` | JsonElement? | GM notes |
| `ActionsRemaining` | int | Default: 1 |
| `BonusActionsRemaining` | int | Default: 0 |
| `ReactionsRemaining` | int | Default: 1 |
| `MovementsRemaining` | int | Default: 1 |
| `FreeActions` | JsonElement? | JSONB |

#### `CombatEvent`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `CombatId` | Guid | FK → Combat |
| `Round` | int | |
| `TurnIndex` | int | |
| `Type` | CombatEventType enum | CombatStart/CombatEnd/TurnChange/Attack/Damage/... |
| `ActorName` | string | |
| `TargetName` | string | |
| `Content` | string | |
| `Metadata` | JsonElement? | JSONB |
| `CreatedAt` | DateTime | UTC |

#### `LLMPreset`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `UserId` | Guid | FK → User |
| `Name` | string | |
| `ProviderType` | string | `"ollama"`, `"lmstudio"`, `"openai"`, `"google"` |
| `BaseModel` | string | |
| `EndpointUrl` | string? | API base URL |
| `ApiKey` | string? | Encrypted in production |
| `Temperature` | float | Default: 0.7 |
| `MaxTokens` | int | Default: 2048 |
| `TopP` | float | Default: 0.9 |
| `FrequencyPenalty` | float? | |
| `PresencePenalty` | float? | |
| `Stream` | bool | Default: `false` |
| `EmbeddingModel` | string? | |
| `EmbeddingEndpointUrl` | string? | |
| `IsActive` | bool | Default: `true` |
| `IsDefault` | bool | Default: `false` |
| `ExtraParams` | JsonElement? | JSON |
| `CreatedAt` | DateTime | UTC |
| `UpdatedAt` | DateTime? | |

Unique index: `{UserId, Name}`

#### `AgentCall`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `SessionId` | Guid? | FK → GameSession |
| `FromAgent` | AgentType enum | Creator/GM/LLM/Dice/RAG/NPC/Player/System |
| `ToAgent` | AgentType enum | |
| `Action` | AgentAction enum | Query/Generate/Roll/Check/Narrate/Suggest/Execute/Notify/Recall/ManageState/Nudge/CreateCharacter |
| `Input` | string? | JSONB text |
| `Output` | string? | JSONB text |
| `OutputMessage` | string? | Human-readable output |
| `Status` | AgentCallStatus enum | Pending/Running/Completed/Failed/Cancelled |
| `Error` | string? | |
| `CreatedAt` | DateTime | UTC |
| `StartedAt` | DateTime? | |
| `CompletedAt` | DateTime? | |
| `DurationMs` | int | |
| `Metadata` | JsonElement? | JSON |
| `ParentCallId` | Guid? | FK → AgentCall (self-ref, cascade delete) |

Index: `{GameId, Status, CreatedAt}` descending

#### `GMToolCall`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `SessionId` | Guid? | FK → GameSession |
| `ToolName` | string | |
| `ToolCallId` | string? | OpenAI-style tool call ID |
| `Arguments` | string? | JSON |
| `Status` | ToolCallStatus enum | Pending/WaitingConfirmation/Confirmed/Executing/Completed/Failed/Cancelled |
| `Result` | string? | JSON result |
| `OutputMessage` | string? | Human-readable output |
| `Error` | string? | |
| `RequiresConfirmation` | bool | |
| `ConfirmedBy` | Guid? | FK → Player |
| `ConfirmedAt` | DateTime? | |
| `CreatedAt` | DateTime | UTC |
| `StartedAt` | DateTime? | |
| `CompletedAt` | DateTime? | |
| `DurationMs` | int | |
| `ParentToolCallId` | Guid? | FK → GMToolCall (self-ref, cascade delete) |

#### `Whisper`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `SessionId` | Guid | FK → GameSession |
| `FromPlayerId` | Guid | FK → Player |
| `Targets` | string | Comma-separated or JSON array |
| `TargetPlayerIds` | List<Guid> | |
| `Content` | string | |
| `Type` | WhisperType enum | InGamePlayerToGM/InGameGMToPlayer/OOCPlayerToGM/OOCGMToPlayer/PlayerToPlayer/GMToGroup/GMToAll |
| `CreatedAt` | DateTime | UTC |

Index: `{GameId, CreatedAt}` descending

#### `RefreshToken`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `UserId` | Guid | FK → User |
| `Token` | string | Unique index |
| `ExpiresAt` | DateTime | |
| `IsRevoked` | bool | Default: `false` |
| `RevokedAt` | DateTime? | |
| `CreatedAt` | DateTime | UTC |

#### `LLMInteractionLog`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `UserId` | Guid | FK → User |
| `PresetId` | Guid? | FK → LLMPreset (SET NULL) |
| `ProviderType` | string | |
| `Model` | string | |
| `PromptTokens` | int? | |
| `CompletionTokens` | int? | |
| `TotalTokens` | int? | |
| `DurationMs` | int | |
| `StartedAt` | DateTime | UTC |
| `CompletedAt` | DateTime | UTC |
| `Success` | bool | Default: `true` |
| `Error` | string? | |
| `SystemPrompt` | string? | Text column |
| `UserPrompt` | string? | Text column |
| `Response` | string? | Text column |
| `RequestJson` | string? | Text column |
| `ResponseJson` | string? | Text column |
| `Origin` | string | "game"/"session"/"agent"/"manual" |
| `OriginGameId` | Guid? | |
| `OriginSessionId` | Guid? | |
| `OriginAgent` | string? | Agent type name |
| `OriginAction` | string? | Agent action |
| `EndpointUrl` | string? | API endpoint URL |

Index: `{UserId, OriginGameId, StartedAt}` descending, `{StartedAt}`

#### `AuditLog`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `UserId` | Guid | FK → User |
| `Action` | string | |
| `Details` | string? | |
| `CreatedAt` | DateTime | UTC |

Index: `{UserId, CreatedAt}` descending

#### `SessionNote`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `SessionId` | Guid | FK → GameSession |
| `CreatorId` | Guid | FK → User |
| `Title` | string | |
| `Content` | string | |
| `CreatedAt` | DateTime | UTC |
| `UpdatedAt` | DateTime? | |

Index: `{SessionId, CreatedAt}` descending

#### `PromptTemplate`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game (cascade delete) |
| `UserId` | Guid | FK → User (cascade delete) |
| `Name` | string | |
| `Type` | string | "narration"/"consistency"/"review"/"session_summary"/"custom" |
| `Prompt` | string | |
| `IsActive` | bool | Default: `true` |
| `IsDefault` | bool | Default: `false` |
| `CreatedAt` | DateTime | UTC |
| `UpdatedAt` | DateTime? | |

Unique index: `{GameId, Type, Name}`

#### `GameTemplate`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `Name` | string | |
| `UserId` | Guid | FK → User (cascade delete) |
| `IsPublic` | bool | Default: `false` |
| `DefaultName` | string? | |
| `SystemId` | string | Default: `"dnd5e"` |
| `LLMPresetId` | Guid? | FK → LLMPreset (SET NULL) |
| `LLMPresetName` | string? | Display name (snapshot) |
| `Language` | string | Default: `"English"` |
| `PlotSeed` | string? | |
| `GameParameters` | string? | |
| `CreatedAt` | DateTime | UTC |
| `UpdatedAt` | DateTime | UTC |

Unique index: `{UserId, Name}`, `{UserId, CreatedAt}` descending

#### `CustomSystemDefinition`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `GameId` | Guid | FK → Game |
| `Name` | string | |
| `JsonDefinition` | string | JSON schema |
| `CreatedAt` | DateTime | UTC |

---

## 7. Backend: `Program.cs` Entry Point

The `Program.cs` is the central configuration file. Key setup:

### 7.1 Service Registration

```
Controllers + JSON options (CamelCase, JsonStringEnumConverter)
├── HttpContextAccessor
├── Swagger (OpenAPI v1, JWT auth, XML docs, custom operation filter)
├── SignalR
├── CORS (AllowAll for dev)
├── DbContext (Npgsql + pgvector, warnings ignored)
├── JWT Auth (HS256, token validation from JwtSettings)
├── Authorization
├── Auth services (IAuthService → AuthService)
├── MigrationService
├── UserIdProvider
├── GameAuthorizationService
├── Game Engine (IDiceEngine, ISystemRulesFactory, ISystemRegistry, IGameEngine)
├── Combat domain services (13 interfaces → implementations)
│   ├── ICombatActionFactory → CombatActionFactory
│   ├── ICombatLifecycleService → CombatLifecycleService
│   ├── ICombatParticipantService → CombatParticipantService
│   ├── ICombatInitiativeService → CombatInitiativeService
│   ├── ICombatTurnService → CombatTurnService
│   ├── ICombatStateService → CombatStateService
│   ├── ICombatSpellService → CombatSpellService
│   ├── ICombatInventoryService → CombatInventoryService
│   ├── ICombatProgressionService → CombatProgressionService
│   ├── ICombatGridService → CombatGridService
│   ├── ICombatAIService → CombatAIService
│   ├── ICombatQueryService → CombatQueryService
│   └── ISANService → SANService
├── ICombatService → CombatService (facade)
├── AgentBus (IAgentBus → AgentBus)
├── GMToolRegistry (IGMToolRegistry → GMToolRegistry)
├── GameAgentManager (singleton, IGameAgentManager → GameAgentManager)
├── MediatR (auto-register handlers from GameEventHandlers assembly)
├── WhisperService
├── ApiKeyEncryptionService
├── ResiliencePolicies (Polly, singleton)
├── HealthChecks (liveness, database, llm-providers)
├── HttpClient
├── RAGService, EmbeddingService
├── LLMPresetService, LLMInteractionLogger
├── PlotWeaver
├── CharacterCreationFactory
├── GameStartService, NarrativeGenerationFactory
├── LLMProviderFactory (singleton)
├── GameManagementService, SessionManagementService, PlayerManagementService
├── SessionNoteService, PromptTemplateService
├── DiceStatsService, GameTemplateService
├── RateLimiting (bound from appsettings RateLimiting section)
├── ForwardedHeaders (X-Forwarded-For, X-Real-IP)
```

### 7.2 Request Pipeline Order

```
1. UseDatabaseMigrations() — auto-apply EF migrations
2. Recover active GameAgents from DB
3. UseSwagger() + UseSwaggerUI()
4. MapHealthChecks("/health", "/health/ready")
5. UseCors (dev only)
6. UseExceptionHandler + UseHsts (prod only)
7. UseHttpsRedirection
8. UseStaticFiles
9. UseRouting
10. Security Headers (X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy, Cache-Control, Pragma)
11. ConfigureForwardedHeaders
12. UseRateLimiting
13. UseAuthentication
14. UseAuthorization
15. MapControllers
16. MapHub<GameHub>("/gamehub")
17. MapFallbackToFile("index.html") — SPA fallback
```

### 7.3 Health Check Endpoints

| Endpoint | Purpose |
|----------|---------|
| `/health` | Liveness check |
| `/health/ready` | Readiness check (database + LLM providers) |

---

## 8. Backend: Key Services

### 8.1 Authentication (`AuthService`)

- **Register**: Email validation → BCrypt hash → create User → generate JWT (15 min) + refresh token (7 days) → save refresh token
- **Login**: Email lookup → BCrypt verify → generate tokens
- **RefreshToken**: Revoke old → generate new pair → save new
- **RevokeToken**: Mark refresh token as revoked
- **ChangePassword**: Verify current → BCrypt new → save
- **UpdateDisplayName**: Trim and save

### 8.2 Dice Engine (`DiceEngine`)

Parses and rolls dice formulas:
- Basic: `2d6`, `4d20`, `1d100`
- Keep: `4d6kh3` (keep highest 3), `4d6kl2` (keep lowest 2)
- Drop: `4d6dr2` (drop lowest 2), `4d6dh1` (drop highest 1)
- Modifier: `2d6+3`, `1d20-2`
- Auto: `1` (single number = auto-success/failure)

### 8.3 System Registry (`SystemRegistry`)

Builtin RPG systems:

| System ID | Name | Version | Key Feature |
|-----------|------|---------|-------------|
| `dnd5e` | Dungeons & Dragons 5th Edition | 5.4 | d20 + modifier vs DC, 6 attributes, 18 skills |
| `pf2e` | Pathfinder 2nd Edition | 2.3 | d20 + modifier, ancestry/class, proficiency levels (untrained/trained/expert/master/legendary) |
| `coc7e` | Call of Cthulhu 7th Edition | 7.1 | d100 under skill, criticals at 1/5 of skill, SAN (sanity) tracking |

Each system defines:
- Default character JSON template
- Attribute names and valid ranges
- Required character fields
- Supported dice formulas
- Skill names list

### 8.4 Agent Bus (`AgentBus`)

Core agentic framework. Routes calls between agents:

| To Agent | Action | Handler |
|----------|--------|---------|
| `LLM` | Generate/Narrate/Suggest/Query | LLM provider (via ILLMProviderFactory) |
| `Dice` | Roll | DiceEngine |
| `RAG` | Check/Recall/Generate/Suggest | RAGService (pgvector similarity) |
| `NPC` | Query NPC data | NPC lookup |
| `Player` | Query character data | Character lookup |
| `System` | Apply system rules | SystemRegistry |
| `GM` | ManageState/Narrate/Suggest/Nudge/Notify | GM agent with tool calling |
| — | CreateCharacter | Character creation |

### 8.5 Game Agent (`GameAgent` + `GameAgentManager`)

- **Per-game background processor** that polls the `AgentCalls` table for pending events
- **Survives restarts** — recovers active games from DB on startup
- **Processing loop**: polls every 1-5 seconds, processes pending calls sequentially
- **Auto-narrate**: After 3 minutes of silence, checks plot thread momentum and triggers narrative
  - High momentum (>3): Escalate threads
  - Failed (<0): Create turning point
  - Normal: Advance story naturally
  - No threads: Check if opening was delivered

### 8.6 Combat Service (`CombatService`)

Domain-services architecture delegated to:
- `ICombatActionFactory` — attacks, save throws
- `ICombatLifecycleService` — start/end/pause/resume
- `ICombatParticipantService` — add/remove participants
- `ICombatInitiativeService` — initiative rolls
- `ICombatTurnService` — advance/retreat turns
- `ICombatStateService` — conditions, HP, death saves, rest
- `ICombatSpellService` — spell casting
- `ICombatInventoryService` — equipment management
- `ICombatProgressionService` — XP, leveling
- `ICombatGridService` — grid/map positioning
- `ICombatAIService` — AI tactical suggestions, auto-resolve
- `ICombatQueryService` — queries
- `ISANService` — Sanity tracking (CoC)

### 8.7 PlotWeaver (`PlotWeaver`)

Automatic plot thread management. Orchestrates 4 strategies:
1. **PlotThreadGenerationStrategy** — initial + dynamic thread generation
2. **PlotThreadAdaptationStrategy** — review and adapt existing threads
3. **PlotMilestoneSpawningStrategy** — spawn milestone events
4. **PlotOpportunityDetectionStrategy** — detect story opportunities

### 8.8 RAG Service (`RAGService`)

Uses pgvector for semantic search:
- `GeneratePlotContextAsync` — retrieves relevant plot threads, NPCs, recent messages
- `FindSimilarPlotThreadsAsync` — vector similarity search
- `GenerateSessionSummaryAsync` — GM summary of recent events
- `CheckPlotConsistencyAsync` — detect contradictions
- `SuggestContinuationAsync` — plot continuation suggestions
- `EmbedMessagesAsync` — generate/store embeddings for messages

### 8.9 LLM Providers

Each game has its own LLM preset (provider type, model, endpoint, API key). Providers:

| Provider | Class | Notes |
|----------|-------|-------|
| Ollama | `OllamaLLMProvider` | Local, OpenAI-compatible API |
| LM Studio | `LmStudioLLMProvider` | Local, OpenAI-compatible API |
| OpenAI | `OpenAILLMProvider` | API key required |
| Google AI Studio | `GoogleAIStudioLLMProvider` | API key required |

All inherit from `BaseLLMProvider`. Created via `ILLMProviderFactory` from `LLMPreset` records.

### 8.10 Resilience (Polly)

- **Retry**: Configurable delay (default 500ms), max retries (default 3)
- **Circuit Breaker**: Failure threshold (default 5), half-open delay (default 30s)
- Applied to all LLM calls

### 8.11 Rate Limiting

Configurable per-endpoint:
| Setting | Default | Purpose |
|---------|---------|---------|
| `GlobalLimit` | 100 | Requests per window |
| `GlobalWindowMinutes` | 1 | Window size |
| `AuthLimit` | 30 | Auth endpoint limit |
| `AuthWindowMinutes` | 1 | Auth window |
| `LlmPresetLimit` | 30 | LLM preset endpoint limit |
| `UseForwardedHeaders` | true | Use X-Forwarded-For |
| `TrustAllProxies` | true | Trust all proxy IPs |

### 8.12 API Key Encryption

API keys are stored encrypted in the database. Uses AES-256-GCM with the `ENCRYPTION_MASTER_KEY` environment variable. Decrypted on-the-fly when creating LLM providers.

---

## 9. Backend: Controllers (25+)

| Controller | Endpoints |
|------------|-----------|
| `AuthController` | POST /register, /login, /refresh, /logout, /change-password, /update-display-name |
| `GamesController` | CRUD games, sessions, players, invite codes, GM status |
| `AdminController` | NPCs, plots, characters, LLM presets, systems, GM agent |
| `AgentFramework` | Agent call queries, game agent activation |
| `Characters` | Character CRUD |
| `CombatLog` | Combat log queries |
| `DiceHistory` | Dice roll history |
| `GMStatus` | GM status queries |
| `GMTool` | GM tool call management |
| `GameState` | Game state queries |
| `Helpers` | Utility endpoints |
| `LLM` | LLM queries |
| `LLMLogs` | LLM interaction log queries |
| `LLMPresets` | LLM preset CRUD |
| `LLMTrigger` | LLM trigger management |
| `NPCs` | NPC CRUD |
| `PlotWeaver` | Plot thread management |
| `Plots` | Plot review queries |
| `QuickWins` | Quick-win feature endpoints |
| `SpellManagement` | Spell slot management |
| `Sway` | Creator narrative sway |
| `Systems` | RPG system management |
| `Whispers` | Whisper message management |

---

## 10. Backend: SignalR Hub (`GameHub`)

### 10.1 Connection Management

- Inherits from `Hub`
- `_playerConnections`: `ConcurrentDictionary<string, string>` mapping `PlayerId → ConnectionId`
- On connect: logs connection
- On disconnect: finds players by connection ID, marks as `Disconnected`, broadcasts `PlayerDisconnected`

### 10.2 Heartbeat System

- `SendHeartbeat(gameId)`: Player sends heartbeat to confirm active connection
- `CheckDisconnectedPlayersAsync(TimeSpan timeout)`: Background check for stale connections (default 60s)

### 10.3 Hub Method Categories

| Partial File | Methods |
|--------------|---------|
| `GameHub.cs` | Connection management, message persistence, embedding generation |
| `GameHub.ChatMethods.cs` | Send public/OOC messages, whispers |
| `GameHub.Combat.cs` | Combat lifecycle, initiative, turns, damage |
| `GameHub.AICombat.cs` | AI tactical suggestions, auto-resolve |
| `GameHub.Dice.cs` | Dice rolling |
| `GameHub.Spells.cs` | Spell casting, spell slot management |
| `GameHub.ToolCalls.cs` | Player roll negotiation (confirm/decline) |
| `GameHub.Whispers.cs` | Whisper messaging |
| `GameHub.JoinLeave.cs` | Join/leave game, role changes |
| `GameHub.Inventory.cs` | Equipment management |
| `GameHub.Grid.cs` | Grid/map positioning |
| `GameHub.SAN.cs` | Sanity checks (CoC) |
| `GameHub.RestSystem.cs` | Short/long rest |
| `GameHub.Progression.cs` | XP, leveling |
| `GameHub.CharacterCreation.cs` | Character creation wizard |
| `GameHub.HelperMethods.cs` | Utility hub methods |
| `GameHub.Helpers.cs` | Additional utilities |
| `GameHub.FlavorText.cs` | Flavor text generation |
| `GameHub.SystemSpecific.cs` | System-specific operations |
| `GameHub.AgentMethods.cs` | Agent framework hub methods |

### 10.4 Group-Based Broadcasting

All game-scoped broadcasts use:
```csharp
await Clients.Group(gameId.ToString()).SendAsync("EventName", data);
```

---

## 11. Backend: MediatR Events

### 11.1 Event Types (30+)

```
GameCreated, GameStarted, GameArchived, GamePaused, GameResumed
PlayerJoined, PlayerLeft, PlayerDisconnected, PlayerReconnected, PlayerRoleChanged
SessionCreated, SessionClosed
MessageSent, WhisperSent, OOCMessageSent, OOCWhisperSent, OOCWhisperReceived
DiceRolled, SkillCheckRequested, AttackRequested
CombatStarted, CombatEnded, ParticipantAdded, ParticipantRemoved, InitiativeRolled, InitiativeRolledForAll
TurnAdvanced, TurnRetreated
CombatAttackExecuted, CombatSaveThrowExecuted, CombatSpellCast
CombatConditionApplied, CombatConditionRemoved
CombatDamageDealt, CombatHealed, CombatXPGranted, CombatLevelUp
CombatRestStarted, CombatRestEnded
CombatGridSet, CombatPositionSet, CombatMove
CharacterUpdated
NPCCreated, NPCUpdated, NPCDeleted
PlotThreadCreated, PlotThreadUpdated
StorySwayed, GMActioned
```

### 11.2 Event Handlers

| Handler | Handles |
|---------|---------|
| `GameLifecycleHandler` | Game lifecycle events |
| `PlayerHandler` | Player join/leave/disconnect/reconnect |
| `PlotWeaverHandler` | Plot thread events → triggers PlotWeaver |

---

## 12. Frontend: Entry Point & Theme

### 12.1 `main.tsx`

```typescript
import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import { ThemeProvider, createTheme } from '@mui/material/styles'
import CssBaseline from '@mui/material/CssBaseline'

const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#9147ff' },
    secondary: { main: '#f50057' },
    background: { default: '#0a0a0a', paper: '#1a1a1a' },
  },
  typography: {
    fontFamily: '"Roboto", "Helvetica", "Arial", sans-serif',
  },
})

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <App />
    </ThemeProvider>
  </React.StrictMode>,
)
```

### 12.2 `App.tsx` — Router

```
/login          → AuthPage
/register       → AuthPage
/               → HomePage
/dashboard      → DashboardPage
/llm-presets    → LLMPresetsPage
/llm-presets/new → LLMPresetsPage (new preset form)
/systems        → SystemsPage
/systems/new    → SystemsPage (new system form)
/user-settings  → UserSettingsPage
/game/:id       → GameRoomPage
/admin/:id      → AdminPage
/character/:id  → CharacterSheetPage
*               → Navigate to /
```

All routes under main layout wrapped in `AuthProvider` + `AppShell`.

---

## 13. Frontend: API Client

### 13.1 `api/client.ts` — APIClient

Fetch wrapper with:
- JWT auth headers (`Authorization: Bearer <token>`)
- Refresh token handling
- Error handling with typed responses
- Methods for all API endpoints

### 13.2 `api/authHook.tsx` — useAuth

Context provider for:
- `user` — current user info
- `accessToken` — current JWT token
- `login(email, password)` — authenticate
- `register(email, password, displayName)` — create account
- `refreshToken(token)` — refresh JWT
- `logout()` — revoke refresh token + clear state
- `changePassword(current, new)` — change password
- `updateDisplayName(name)` — update display name

### 13.3 `api/gameHooks.ts` — Game Hooks

| Hook | Purpose |
|------|---------|
| `useGames()` | List user's games |
| `useGame(id)` | Get single game |
| `useSessions(id)` | Get game sessions |
| `useGMStatus(id)` | Get GM agent status |
| `useSway(id)` | Send narrative sway |

### 13.4 `api/hubHook.ts` — useGameHub

SignalR wrapper:
- Connects to `/gamehub`
- Joins game group: `game:{gameId}`
- Subscribes to all events (NewMessage, PlayerDisconnected, etc.)
- Provides methods to call hub methods (roll dice, attack, etc.)

### 13.5 `api/toolCallsHook.ts` — Tool Calls

Manages pending tool call state, confirmation/decline actions.

### 13.6 `api/gameToolsHook.ts` — Game Tools

Manages game tool state (GM tools, AI combat suggestions).

### 13.7 `api/useEntity.ts` — useEntity

Generic CRUD hook: `useEntity<T>(endpoint, id?)` → `{ data, loading, error, create, update, delete }`

---

## 14. Frontend: TypeScript Types

Shared in `api/types/index.ts` (~300+ lines):

### Enums
```typescript
MessageType { InGamePublic, InGameWhisper, OOCPublic, OOCWhisper, Action, Dice, System, GM, AgentCall, AgentResponse }
GameStatus { Draft, Active, Archived, Finished }
GMStatus { Idle, Running, Paused }
PlayerRole { Creator, Player, Spectator, Observer }
PlayerStatus { Active, Disconnected, Left }
PlotThreadStatus { Active, Resolved, Abandoned }
WhisperType { InGamePlayerToGM, InGameGMToPlayer, OOCPlayerToGM, OOCGMToPlayer, PlayerToPlayer, GMToGroup, GMToAll }
AgentType { Creator, GM, LLM, Dice, RAG, NPC, Player, System }
AgentAction { Query, Generate, Roll, Check, Narrate, Suggest, Execute, Notify, Recall, ManageState, Nudge, CreateCharacter }
AgentCallStatus { Pending, Running, Completed, Failed, Cancelled }
CombatStatus { Active, Paused, Finished }
CombatEventType { CombatStart, CombatEnd, TurnChange, Attack, Damage, Healing, Condition, SaveThrow, Initiative, Death, Revival, RoundStart }
CombatParticipantType { Player, NPC }
LLMProviderType { Ollama, LmStudio, OpenAI, Google }
PlotThreadCategory { General, Faction, Mystery, Personal, Threat, WorldEvent, Relationship }
MilestoneStatus { Pending, Triggered, Completed, Abandoned }
OpportunityType { NewThread, SpawnMilestone, AdaptThread, MergeThreads, EscalateThreat }
```

### Interfaces
```typescript
LLMPreset { id, name, providerType, baseModel, endpointUrl?, hasApiKey, temperature, maxTokens, topP, isDefault, isActive, createdAt, updatedAt? }
LLMInteractionLog { id, presetId?, presetName?, providerType, model, promptTokens?, completionTokens?, totalTokens?, durationMs, success, error?, systemPrompt?, userPrompt?, response?, requestJson?, responseJson?, origin, originGameId?, originSessionId?, originAgent?, originAction?, startedAt, completedAt }
PresetUsageSummary { presetId, presetName, providerType, totalCalls, successfulCalls, failedCalls, totalTokens, totalPromptTokens, totalCompletionTokens, avgDurationMs, lastUsed }
GameProviderUsageSummary { providerType, model, totalCalls, successfulCalls, failedCalls, successRate, totalTokens, totalPromptTokens, totalCompletionTokens, avgDurationMs, maxDurationMs, firstCall, lastCall }
CombatLog { combatId, name?, status, currentRound, currentTurnIndex, participants, events }
CombatParticipantSummary { id, displayName, participantType, currentHP, maxHP, ac, initiative, conditions, isCurrentTurn, isDead }
ConditionEntry { name, duration, description? }
CombatLogEvent { id, round, turnIndex, type, actorName, targetName, content, createdAt }
CombatAttackResult { attacker, weapon, target, hit, isCritical, isFumble, attackRoll, attackDice, ac, damageDice, damageTotal, damageInfo, targetHP, targetMaxHP }
CombatSaveThrowResult { participant, saveType, diceRoll, dc, success }
CombatDeathSaveResult { participant, success, successes, failures, isStabilized, isDead }
CombatSummary { id, name?, status, currentRound, participantCount, startedAt }
InitiativeRoll { participantId, displayName, initiative, rolls }
SpellCastResult { caster, spellName, spellLevel, target, saveType, saveDC, saveSuccess, isCritical, damageType, damageTotal, damageInfo, effect, targetHP?, targetMaxHP? }
LevelUpResult { participantId, newLevel, systemId }
RestStatus { restType, isInProgress, roundsRemaining, hpRecovered, effects }
GridPosition { participantId, gridX, gridY, displayName, moveSpeed }
AISuggestions { combatId, threatLevel, recommendedStrategy, suggestions, npcActions, warnings }
AITacticalAction { actor, action, target, reason, priority, details? }
AINPCAction { npcName, behavior, target, action, reason }
AICombatWarning { message, severity, affectedParticipant? }
SANCheckResult { participant, currentSAN, roll, dc, success, isCritical, sanLoss, effect }
PlotThreadResponse { id, title, category, description, status, momentum, relevanceScore, nextMilestone, foreshadowing, adaptationHistory, createdAt, updatedAt? }
ThreadUpdate { threadId, threadTitle, oldMomentum, newMomentum, oldStatus, newStatus, oldDescription, newDescription, oldMilestone, newMilestone, reason }
PlotReviewResponse { id, trigger, summary, updates, reviewedAt }
AdjustMomentumRequest { delta, reason }
GameTemplate { id, name, defaultName?, systemId, llmPresetId?, llmPresetName?, language, plotSeed?, gameParameters?, createdAt, updatedAt }
CreateGameTemplateRequest { name, defaultName?, systemId, llmPresetId?, llmPresetName?, language, plotSeed?, gameParameters? }
UpdateGameTemplateRequest { name, defaultName?, systemId, llmPresetId?, llmPresetName?, language, plotSeed?, gameParameters? }
```

---

## 15. Frontend: Pages

### 15.1 `HomePage.tsx` — Landing Page
- Welcome screen with project description
- Links to login/register

### 15.2 `AuthPage.tsx` — Login/Register
- Two-tab interface (Login / Register)
- Email + password fields
- Form validation
- Error display

### 15.3 `DashboardPage.tsx` — Game Dashboard
- List of user's games
- Create game form (LLM preset selection, plot seed, game parameters)
- Join game via invite code
- Game status indicators

### 15.4 `GameRoomPage.tsx` — Main Game Interface
- Chat panel (messages, whispers)
- Player list with status
- Actions panel (dice, combat, spells, inventory)
- GM status indicator
- Pause/resume buttons
- Creator sway input
- Settings panel

### 15.5 `AdminPage.tsx` — Admin Panel
- NPCs management (CRUD)
- Plot board (admin view)
- Characters management
- LLM preset selection
- System configuration
- GM agent controls

### 15.6 `CharacterSheetPage.tsx` — Character Sheet
- View/edit character details
- Attributes display
- Skills list
- Inventory management
- Spell slot tracker
- Conditions display

### 15.7 `CharacterCreateWizard.tsx` — Character Creation
- Multi-step wizard (8 steps)
- 8 backgrounds: Acolyte, Criminal, Soldier, Sage, Gladiator, Folk Hero, Urchin, Noble
- Class selection
- Attribute assignment
- Skill selection
- Equipment selection

### 15.8 `CombatTab.tsx` — Combat Panel
- Initiative tracker
- Turn order display
- Attack/skill check buttons
- Condition management
- Spell slot tracker
- Grid/map view
- AI combat suggestions

### 15.9 `CombatLogViewerPage.tsx` — Combat Log
- Full combat event history
- Filterable by type
- Timestamp display

### 15.10 `GameStatePage.tsx` — Game State
- Game state JSON viewer/editor
- Plot thread management
- Session notes

### 15.11 `DiceHistoryTab.tsx` — Dice History
- Roll history with formulas
- Results display
- Filter by game/session

### 15.12 `GMToolPanel.tsx` — GM Tool Panel
- Available GM tools
- Tool call management
- Pending confirmations

### 15.13 `LLMPresetsPage.tsx` — LLM Preset Management
- List of user's presets
- Create/edit presets
- Provider selection (Ollama/LM Studio/OpenAI/Google)
- Model configuration
- Test connection

### 15.14 `LLMUsagePanel.tsx` — LLM Usage Statistics
- Usage per preset
- Token consumption
- Success/failure rates
- Duration statistics
- Provider breakdown

### 15.15 `PlotBoardTab.tsx` — Plot Board (Player View)
- Active plot threads
- Momentum indicators
- Milestone status
- Thread categories

### 15.16 `PlotBoardAdminTab.tsx` — Plot Board (Admin View)
- All plot threads
- Momentum adjustment
- Thread management
- Plot review history
- Opportunity detection

### 15.17 `SystemsPage.tsx` — RPG System Management
- List of systems
- Create/edit custom systems
- System configuration
- Default character templates

### 15.18 `UserSettingsPage.tsx` — User Settings
- Display name
- Password change
- Account info

---

## 16. Frontend: Components

### 16.1 `AppShell.tsx` — App Shell
- Top navigation bar
- Side panel (collapsible)
- Main content area
- Responsive layout

### 16.2 `Layout.tsx` — Layout Wrapper
- Standard page layout
- Container padding
- Header support

### 16.3 `SidePanel.tsx` — Side Panel
- Collapsible panel
- Tabbed content
- Player list, actions, settings

### 16.4 `WelcomeScreen.tsx` — Welcome Screen
- Onboarding content
- Project info
- Quick actions

### 16.5 `ToolCallBanner.tsx` — Tool Call Notification
- Banner for pending tool calls
- Confirmation buttons
- Roll request display

### 16.6 `PlayerRollDialog.tsx` — Player Roll Dialog
- Dice rolling interface
- Confirm/decline buttons
- Roll result display

### 16.7 `MarkdownRenderer.tsx` — Markdown Renderer
- React-markdown wrapper
- GFM support (tables, strikethrough, etc.)
- Code highlighting

---

## 17. Running the Project

### Development

```bash
# Start PostgreSQL (Docker)
docker compose up -d

# Backend
cd src/Adnd.Server
dotnet run

# Frontend (new terminal)
cd src/Adnd.Client
npm run dev
```

### Production Build

```bash
# Frontend
cd src/Adnd.Client
npm run build

# Backend
cd ../Adnd.Server
dotnet publish -c Release -o ../publish
```

### Docker (Full Stack)

```bash
# Start
docker compose up -d

# Stop
docker compose down
```

### URLs

| Service | URL |
|---------|-----|
| Backend API (dev) | `http://localhost:5010` |
| Swagger UI (dev) | `http://localhost:5010/swagger` |
| Frontend (dev) | `http://localhost:3000` |
| Health check | `http://localhost:5010/health` |
| Readiness check | `http://localhost:5010/health/ready` |
| SignalR hub | `ws://localhost:5010/gamehub` |

---

## 18. Adding New Features

### Adding a New RPG System

1. Add system definition in `SystemRegistry.RegisterBuiltinSystems()` with all attributes, skills, proficiency levels, default character JSON
2. Update `DiceEngine.cs` if system has unique dice mechanics
3. Add system-specific logic to `GameEngine.cs`
4. Create system rules in `ISystemRules.cs` implementations (e.g., `DnD5eRules`, `PF2eRules`, `CoC7eRules`)
5. Update `src/Adnd.Client/src/types/index.ts` with new types
6. Add admin UI in `SystemsPage.tsx` for system configuration

### Adding a New LLM Provider

1. Create a new class inheriting from `BaseLLMProvider`
2. Implement `CompleteAsync`, `CompleteWithToolsAsync`, `GetTokenUsage`
3. Add provider type to `LLMProviderType` enum in both backend and frontend
4. Register in `LLMProviderFactory`
5. Add provider selection UI in `LLMPresetsPage.tsx`
6. Update `LLMInteractionLogger.cs` to handle the new provider

### Adding a New Agent

1. Add agent type to `AgentType` enum in `AgentCall.cs`
2. Add action type to `AgentAction` enum in `AgentCall.cs` (if needed)
3. Add dispatch handler in `AgentBus.DispatchCall()` method
4. Define agent call contract in `AgentCall.cs` model
5. Add agent-specific logic in a dedicated service class
6. Update `GameHub.cs` to route agent calls through the bus
7. Add event type in `Events/GameEvents.cs` if it should trigger narrative
8. Add handler in `Handlers/GameEventHandlers.cs` if it should be event-driven

### Adding a New API Endpoint

1. Add endpoint to appropriate controller (`AuthController`, `GamesController`, or `AdminController`)
2. Add service method if business logic is needed
3. Add EF entity if persistence is needed (update `AppDbContext.cs`)
4. Create migration: `dotnet ef migrations add <Name>`
5. Add frontend API call in `api/client.ts`
6. Add hook in `api/gameHooks.ts` if reusable
7. Add UI component/page as needed
8. If it triggers game narrative, add event in `Events/GameEvents.cs` and handler in `Handlers/GameEventHandlers.cs`

### Adding a New Combat Domain Service

1. Create interface in `Services/ICombat*.cs`
2. Create implementation class
3. Register in `Program.cs` DI
4. Delegate from `CombatService` facade
5. Add hub methods in appropriate `GameHub.*.cs` partial file

---

## 19. Key Conventions

### Backend
- **Controllers are thin** — delegate to services. No business logic in controllers.
- **Services are single responsibility** — each registered in `Program.cs` via DI.
- **Models use EF Core conventions** — primary keys are convention-based (`Id`).
- **Migrations are auto-applied** at startup via `MigrationService`. No manual `dotnet ef` needed in prod.
- **Auth uses JWT bearer tokens** with refresh token rotation. `UserIdProvider` extracts user ID from JWT.
- **Authorization is game-scoped** — `GameAuthorizationService` checks role permissions (Creator/Player/Spectator/Observer).
- **SignalR hub is `GameHub`** — use `Clients.Group($"game:{gameId}")` for game-scoped broadcasts.
- **LLM providers are per-game** — created from `LLMPreset` DB records via `ILLMProviderFactory`. No global provider registry.
- **RAG uses pgvector** — `RAGService` handles similarity search and consistency checks.
- **AgentBus** is the agentic framework — agents register handlers and call each other via `CallAgent`.
- **DiceEngine** parses formulas like `4d6kh3+2d4-1`. System-aware resolution.
- **MediatR event bus** decouples GameHub from services. Events are published for all game actions.
- **GameAgent** per-game background processor polls `AgentCalls` table for pending events.
- **GameAgentManager** is a singleton that manages per-game agents and recovers them on startup.
- **Creator/GM split**: Creator defines plot seed/tone/LLM preset; AI-GM runs the game autonomously.
- **PlotWeaver** handles dynamic plot generation with `PlotWeaver.cs` service and `PlotWeaverHandler.cs` event handler.
- **Player disconnect detection** via SignalR heartbeat + `CheckDisconnectedPlayersAsync`.
- **Action economy** tracks actions/bonus actions/reactions/movements per combat participant.
- **Spell slot management** stored as JSON in Character model with visual tracker in UI.
- **Character creation wizard** includes 8 backgrounds (Acolyte, Criminal, Soldier, Sage, Gladiator, Folk Hero, Urchin, Noble).
- **Player roll negotiation** uses `GMToolCall` model with `WaitingConfirmation` status and `ConfirmPlayerRoll`/`DeclinePlayerRoll` hub methods.

### Frontend
- **API Client** (`api/client.ts`) wraps fetch with auth headers. All API calls go through it.
- **Hooks follow `useXxx` pattern**: `useAuth`, `useGames`, `useGame`, `useGameHub`, `useGMStatus`, `useSway`.
- **GameHub** (`hubHook.ts`) manages SignalR connection, groups, and event subscriptions.
- **Pages are route-based** with auth guards.
- **MUI theme is dark-mode** by default. All components use MUI theming.
- **Types are shared** in `api/types/index.ts`. Match backend models.
- **Game creation** includes LLM preset selection, plot seed, and game parameters.
- **Game room** shows GM status indicator, pause/resume buttons, and creator sway input.
- **LLM presets** managed via `LLMPresetsPage.tsx` and `LLMUsagePanel.tsx`.
- **Plot board** has separate player (`PlotBoardTab.tsx`) and admin (`PlotBoardAdminTab.tsx`) views.

---

## 20. Security Headers

The app adds these headers to every response:

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Content-Type-Options` | `nosniff` | Prevent MIME sniffing |
| `X-Frame-Options` | `DENY` | Prevent clickjacking |
| `X-XSS-Protection` | `1; mode=block` | XSS filter |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Control referrer info |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Disable browser APIs |
| `Cache-Control` | `no-store, no-cache, must-revalidate` | No caching |
| `Pragma` | `no-cache` | HTTP/1.0 cache control |

---

## 21. File Count Summary

| Category | Count |
|----------|-------|
| Backend Controllers | 25+ |
| Backend Services | 40+ |
| Backend Models | 20+ |
| Backend Hub Partial Files | 19 |
| Frontend Pages | 18 |
| Frontend Components | 7 |
| Frontend API Hooks | 6 |
| Frontend Types | 300+ lines |
| Total Source Files | 150+ |

---

## 22. Quick Start Checklist

- [ ] Install .NET 10 SDK
- [ ] Install Node.js 20.x
- [ ] Install Docker
- [ ] Clone repository
- [ ] `docker compose up -d` (starts PostgreSQL)
- [ ] `cd src/Adnd.Server && dotnet run` (starts backend)
- [ ] `cd src/Adnd.Client && npm run dev` (starts frontend)
- [ ] Open `http://localhost:3000` in browser
- [ ] Register a new account
- [ ] Create a game with LLM preset
- [ ] Join the game and start playing

---

## 23. Important Notes

1. **Never hand-write SQL migrations** — always use `dotnet ef migrations add <Name>`
2. **All changes must be backwards-compatible** — add nullable columns, default values; never remove or rename
3. **JWT_SECRET_KEY and ENCRYPTION_MASTER_KEY must be set in production** — 32+ character random strings
4. **pgvector extension** is required — the Docker image `pgvector/pgvector:pg17` includes it
5. **GameAgents survive restarts** — they recover from the `AgentCalls` table automatically
6. **Rate limiting** uses `UseForwardedHeaders` and `TrustAllProxies` for Docker/k8s compatibility
7. **API keys are encrypted at rest** using AES-256-GCM with the master key from environment
