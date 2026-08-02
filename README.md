# ADnD — AI-Powered TTRPG Platform

ADnD is a multiplayer tabletop RPG platform where a configurable AI Game Master runs your campaign. Players connect in real time over WebSockets, roll dice, manage characters, and watch the story unfold — all driven by an LLM backend you choose and control.

## Features

- **AI Game Master** — powered by Ollama, LM Studio (any OpenAI-compatible endpoint), OpenAI, or Google AI Studio
- **Real-time multiplayer** — SignalR WebSocket hub; all events broadcast instantly
- **Full combat tracker** — initiative, turns, HP, conditions, death saves, action economy
- **Plot Intelligence** — RAG-backed plot threads with automatic momentum tracking and LLM-driven adaptation
- **Character sheets** — attributes, skills, spells, inventory, backgrounds, custom fields
- **Multi-system** — D&D 5e, Pathfinder 2e, Call of Cthulhu 7e, and custom systems
- **Durable sagas** — Wolverine/PostgreSQL-backed AI work queue; no lost narration on restart
- **JWT auth** — refresh token rotation, encrypted API keys at rest (AES-256-GCM)
- **Whispers** — private GM-to-player and player-to-player messaging
- **Secret dice rolls** — result visible only to roller and GM
- **Session recaps** — on-demand or automatic "previously on..." summaries

## Quick Start

### Prerequisites

- Docker + Docker Compose v2+

### 1. Configure environment

```bash
cp .env.example .env
```

Edit `.env` and fill in:

| Variable | Description |
|----------|-------------|
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `JWT_SECRET_KEY` | JWT signing key, min 32 bytes — generate with `openssl rand -base64 48`. The app refuses to start on a placeholder or a short key. |
| `JWT_ISSUER` | JWT issuer string (e.g. `adnd-server`) |
| `JWT_AUDIENCE` | JWT audience string (e.g. `adnd-client`) |
| `ENCRYPTION_MASTER_KEY` | AES-256-GCM key — generate with `openssl rand -base64 32` |

Optional settings (see `.env.example` for the full list):

| Variable | Default | Description |
|----------|---------|-------------|
| `SECURITY__ALLOW_PRIVATE_LLM_ENDPOINTS` | `true` | Permits LLM presets to target private/LAN addresses, which self-hosted Ollama and LM Studio need. Set `false` when untrusted users can register. |
| `SWAGGER__ENABLED` | Development only | Swagger UI cannot sit behind bearer auth, so it is not served in Production unless you opt in. |
| `HANGFIRE__DASHBOARD_ENABLED` | `false` | The `/hangfire` dashboard. Off by default; pair with `HANGFIRE__DASHBOARD_ALLOWED_IPS`. |

### 2. Start the stack

```bash
docker compose up --build
```

This builds the .NET server (which also builds the Vite client inside Docker) and starts PostgreSQL. EF Core migrations run automatically on first start.

Open **http://localhost:5010** in your browser.

### 3. First-time setup

1. Register an account at `/register`
2. Go to **LLM Presets** and add your LLM backend (Ollama, LM Studio, OpenAI, or Google AI Studio)
3. Go to **Dashboard**, create a game, select your preset
4. Share the invite code with players and start the game

## LLM Provider Setup

| Provider | `ProviderType` | Notes |
|----------|---------------|-------|
| Ollama | `ollama` | Local; set `EndpointUrl` to `http://host.docker.internal:11434` |
| LM Studio / any OpenAI-compatible | `openaicompatible` | Set `EndpointUrl` to your server's `/v1` base URL |
| OpenAI | `openai` | Requires API key |
| Google AI Studio | `google` | Requires API key; supports `ReasoningEffort` |

## EF Core Migrations

Migrations are applied automatically on startup. To generate a new migration:

```bash
docker compose run --rm app-sdk dotnet ef migrations add <Name> --project src/Adnd.Server
```

## Architecture

```
Browser (React 19 + MUI)
    │
    ├── REST  /api/**          — CRUD, auth, admin
    └── WebSocket /gamehub     — SignalR real-time events
                    │
            Adnd.Server (.NET 10)
                    │
            ┌───────┴────────┐
            PostgreSQL 17     Wolverine message bus
            + pgvector        (PostgreSQL-backed sagas)
```

**Key design principles:**

- Everything is a Message — dice rolls, combat events, narration all flow through the message feed
- Server-side dice only — no client-side RNG
- LLM calls are durable — Wolverine sagas survive restarts
- Docker-first — no local dev workflow needed; `docker compose up --build` is the only workflow

## Endpoints

| Path | Description |
|------|-------------|
| `http://localhost:5010` | React SPA |
| `http://localhost:5010/swagger` | API documentation — Development only unless `SWAGGER__ENABLED=true` |
| `http://localhost:5010/health` | Liveness check (database only, so an LLM outage cannot restart-loop the container) |
| `http://localhost:5010/health/ready` | Readiness check — also probes the configured LLM provider and pgvector |
| `http://localhost:5010/hangfire` | Background job dashboard — requires `HANGFIRE__DASHBOARD_ENABLED=true` |

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 10 |
| ORM | EF Core 10 + Npgsql |
| Vector search | pgvector + HNSW indexes |
| Message bus | WolverineFx 6 (PostgreSQL-backed) |
| Background jobs | Hangfire (PostgreSQL-backed) |
| Real-time | ASP.NET Core SignalR |
| Auth | JWT HS256 + refresh token rotation |
| Frontend | React 19 + TypeScript + Vite 6 |
| UI | MUI v7 |

## License

MIT — see [LICENSE](LICENSE).
