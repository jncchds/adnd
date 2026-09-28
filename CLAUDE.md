# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Working in this repo

Rules for anyone (or anything) making changes here:

- **Build with `docker compose build --no-cache`.** Layer caching has produced misleading
  green builds in this repo; a cold build is the only trusted signal.
- **Do not start, stop or recreate containers.** No `docker compose up`, `restart`, `down`
  or `stop` — running the stack is the maintainer's call. Build, report the result, and stop.
- **Never run `dotnet ef database update`.** Migrations apply on startup (see below).
- Use the local toolchain for day-to-day work: the .NET 10 SDK, `dotnet-ef` (pinned as a
  repo-local tool in `dotnet-tools.json` — `dotnet tool restore` on a fresh clone) and npm in
  `src/Adnd.Client`. Avoid `docker compose run sdk`: it writes `obj/`, `bin/` and scaffolded
  files into the repo as root, which then blocks the local tools. If a tool is missing locally,
  ask for it to be installed rather than routing around it.
- The backend must build with **0 warnings** (`TreatWarningsAsErrors`), and the frontend
  must pass `tsc --noEmit && vite build`. Never suppress a warning to get green.

## Versioning

Same scheme as nopds. `VERSION` at the repo root is the single source of truth: `Directory.Build.props`
reads it into the assembly version, `vite.config.ts` bakes it into the UI badge (and
`RELEASE_NOTES.md` into `/release-notes`), and CI tags the Docker image with it.

- Bump `VERSION` to the next unused patch version in the first commit of a release, together with
  a new `## vX.Y.Z — YYYY-MM-DD` section at the top of `RELEASE_NOTES.md`.
- Every later commit before the next push to origin adds its bullets to that same section — never a
  second bump for a version nobody has seen.
- Notes are one line per change, prefixed `feat:`, `fix:`, `migration:`, `build:`, `ci:` or `docs:`.

## Build & Run

**Docker Compose is the only supported workflow.** There is no `dotnet run` or Vite dev server path.

```bash
# First run: copy and fill in required env vars
cp .env.example .env

# Build the images (always --no-cache; see "Working in this repo")
docker compose build --no-cache

# Start the full stack
docker compose up

# Endpoints once running
# App:      http://localhost:5010
# Health:   http://localhost:5010/health        (liveness — database only)
# Ready:    http://localhost:5010/health/ready  (also probes LLM provider + pgvector)
# Swagger:  http://localhost:5010/swagger       (Development only unless SWAGGER__ENABLED=true)
# Hangfire: http://localhost:5010/hangfire      (requires HANGFIRE__DASHBOARD_ENABLED=true)
```

Required `.env` values: `POSTGRES_PASSWORD`, `JWT_SECRET_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `ENCRYPTION_MASTER_KEY`.

`JWT_SECRET_KEY` must be ≥32 bytes and not the `OVERRIDE_IN_ENVIRONMENT` placeholder — the app refuses to start otherwise. See `.env.example` for optional security settings (`SECURITY__ALLOW_PRIVATE_LLM_ENDPOINTS`, `SWAGGER__ENABLED`, `HANGFIRE__DASHBOARD_ENABLED`).

## EF Core Migrations

Use the repo-local `dotnet-ef` (same version as EF Core; no database needed to scaffold).

```bash
dotnet ef migrations add <Name> --project src/Adnd.Server
```

Migrations are applied automatically on startup by `MigrationService.ApplyMigrationsAsync()`. **Never run `dotnet ef database update`** — starting the app is how migrations get applied.

`DesignTimeDbContextFactory` builds the context for the EF tooling so `dotnet ef` does not boot the full host (which would demand a real JWT key and encryption key it has no need for).

Current migrations: `InitialCreate`, `AddCharacterBackstory`, `AgentLoopToolCallState`, `PlotThreadResolvedAt`, `AddLlmInteractionLogStatus`, `AddAgentCallStepHistoryAndLlmReasoning`, `AddAgentCallRequestedByPlayer`, `NPCStatusAndLastSeen`, `ClearPrivateMessageEmbeddings`,
`CharacterFeaturesAndSecretRolls`, `RollPromptMessageLink`, `CombatZones`, `ExperienceAndStoryNudges`.

**Check scaffolded migrations before accepting them.** EF emits `jsonb NOT NULL DEFAULT ''` for new `JsonElement` columns; `''` is not valid JSON and the `ALTER TABLE` fails. The converter's sentinel is the literal `"null"`.

## Project Structure

```
src/
  Adnd.Server/          # .NET 10 ASP.NET Core Web API (sole backend project)
    Agent/              # GameAgent, GameAgentManager
    Controllers/        # REST API endpoints
    Data/               # AppDbContext, MigrationService, migrations
    Events/             # GameEvents records (Wolverine message types)
    Handlers/           # Wolverine message handlers
    Hubs/               # SignalR GameHub (partial class: chat, narrate/suggest, join)
    Models/             # EF entities + Enums.cs
    Services/           # All domain services
      Llm/              # ILLMProvider + per-provider implementations
  Adnd.Client/          # React 19 + TypeScript + Vite 6 (compiled into wwwroot at Docker build time)
docker-compose.yml
DESIGN_DOCUMENT.md
```

The React frontend is compiled during the Docker build stage and served as static files; there is no separate frontend container.

## Architecture

**ADnD** is a multiplayer TTRPG platform where an AI Game Master runs campaigns in real time.

**The LLM is the GM** (and the narrator — they may become separate agents). Humans have
exactly two levers: the game's creator can **inspect the internals** (the admin pages) and
**nudge the narration** (`IStoryNudgeService`). Nobody else changes game state by hand:
players never roll dice, and neither the creator nor the players deal damage, move
combatants, hand out items or edit a sheet's mechanics. Every such change is a GM tool. Do not
add manual controls or player roll buttons; add a tool.

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
AgentCallQueued → AgentSaga.Start
    → LLMDispatchHandler → AgentSaga.Handle(LLMResponseReceived)
    → [ToolExecutionHandler → AgentSaga.Handle(ToolCallCompleted) → LLMFollowUpHandler] (loop if tools)
    → AgentSaga.Handle(NarrativeReady) → SignalR push to players
```

`AgentSaga` is the state machine keyed on `AgentCall.Id`. States: `Init → LLMDispatch → LLMResponse → ToolExecution → LLMFollowUp → NarrativeReady → Completed | Failed`. 5-minute timeout → `Failed`.

**A saga-routed message (one carrying `[property: SagaIdentity]`) is delivered only to that saga's `Start`/`Handle` methods.** A separate plain-handler class registered for the same message type is silently never invoked — no exception, no log, the message just doesn't reach it. This cost a full gameplay loop, four separate times over: `SagaOrchestratorHandler` (for `AgentCallQueued`), `LLMResponseHandler` (for `LLMResponseReceived`), `CoordinatorHandler` (for `ToolCallCompleted`), and `NarrativeHandler` (for `NarrativeReady`) each used to sit *alongside* the saga's own `Handle` method for the same message, silently dead on arrival every time — Wolverine gave no error, so each one shipped, got tested with a code path that happened not to exercise it, and only surfaced later. In the `NarrativeReady` case specifically, this meant the saga reached "Completed" while the chat message was never saved or broadcast and `AgentCall.Status` never left `Running` — from the admin UI it looked identical to a hung LLM call. All four are now merged directly into `AgentSaga`'s own `Handle` methods, and the standalone handler classes are deleted. Do not add a standalone handler for a message type a saga already owns — put the logic in the saga's `Handle` method, and grep for an existing plain handler on the same message type before assuming one doesn't already conflict.

**Any message routed to `AgentSaga` must carry `[property: SagaIdentity]` on `AgentCallId`.** Wolverine only recognises a property named `Id`, `SagaId` or `AgentSagaId` by convention; without the attribute it cannot correlate the message to its saga and the whole chain silently stops. See `Events/GameEvents.cs`.

**Terminal state has exactly one owner.** `AgentCallFailed` is a *non-terminal* signal handled only by `AgentCallFailedHandler`, which decides whether to re-dispatch or give up. When it gives up it publishes `AgentCallAbandoned`, which the saga handles and which is the only path (besides `SagaTimeout`) that writes `AgentCallStatus.Failed`. Do not have both a handler and the saga write `AgentCall.Status` — they run on separate `DbContext`s.

### GM Tool Registry

28 registered tools. Narration and world: `narrate`, `wait`, `rollDice` (NPCs and the world),
`queryNPCs`, `registerNPC`, `updateNPCStatus`, `searchPlotContext` (substring search over
narrator-visible history, plot threads and NPCs), `updateGameState`, `sendWhisper`. Player
characters: `requestPlayerRoll`, `deathSave`, `queryCharacter`, `damage`, `heal`,
`setCondition`, `giveItem`, `removeItem`, `castSpell`, `rest`, `awardExperience`. Combat:
`startCombat`, `addCombatParticipant`, `moveParticipant`, `setZones`, `nextTurn`, `endCombat`,
`generateLoot`. Executed sequentially by `ToolExecutionHandler`; the saga advances through them.

**Tools address everything by name, never by id.** The model is only ever shown names — player
characters, NPCs, combatants, zones — so an id argument is one it invented. `requestPlayerRoll`
used to take a `playerId` for exactly this reason and rolled for nobody. `ICharacterStateService`
resolves a character by name within the game; unknown names return an error listing the party,
which the model corrects itself from on its follow-up.

**A character's mechanics change only through tools.** `ICharacterStateService` owns hit points
and conditions outside combat, inventory (stacks, `[{name, quantity}]`; older sheets hold bare
strings and read as quantity 1), spell slots, rests and experience; `ICombatTracker` owns them
in a fight and writes a PC's HP and conditions through to the sheet. Both push
`CharacterUpdated`. `ClassRules` holds the numbers: hit dice, the full/half/pact slot tables,
proficiency and the XP thresholds — level-ups are applied by `awardExperience`, never set.
`PUT /api/characters/{id}` accepts only who the character is (name, background, backstory,
custom fields).

`registerNPC` is how an NPC introduced in narration becomes a real row instead of a name that scrolls out of the RAG window and comes back as a different person. It is deliberately a *same-turn* tool: `BuildNarrateSystemPromptAsync` lists the NPCs in play and tells the model to call it alongside `narrate` in the same response, so there is no second LLM pass to bill or to fail. Matching is by **name**, case-insensitively scoped to the game — never by an id, which the model would have to invent — and an existing name updates rather than duplicating. Only fields the model actually supplied are written, so a later attitude-only call doesn't blank the description.

`updateNPCStatus` is the other half: `NPCStatus` is `Active | Dead | Departed`, and only `Active` NPCs are offered to the prompt unprompted. Without it the roster only grows and the narrator keeps being handed people the party killed two sessions ago. It refuses to create — a status change naming an unregistered NPC means the model invented the name. `registerNPC` revives `Departed` (they were just written back into a scene) but never `Dead`, which needs an explicit status call.

### NPC relevance

`INPCRelevanceService.GetRelevantAsync(gameId, sessionId, limit = 8)` decides which NPCs the narrator sees, and is the **only** thing that should build an NPC list for a prompt — both `BuildNarrateSystemPromptAsync` and `RAGService.GeneratePlotContextAsync` (`=== NPCs IN PLAY ===`) go through it. A campaign accumulates NPCs indefinitely; a prompt cannot, and the old unbounded dump buried the two people actually in the room. Ranking is name-mentions in the last 20 table messages first, then `Active` NPCs by `LastSeenAt` desc; a `Dead`/`Departed` NPC is included only while the party is still talking about them. `LastSeenAt` is stamped by `registerNPC`/`updateNPCStatus` and on manual creation. This is recency, not semantics — NPCs have no embedding column, and adding one would cost an embedding call per turn to answer a question recency already answers. `queryNPCs` remains the unfiltered escape hatch and reports `Status`.

`wait` is the GM declining to act this turn — every in-character player line fires a narrate call, so without it the GM is structurally obliged to interject on both halves of a conversation the characters are having with each other. It never reaches `GMToolRegistry.ExecuteToolAsync` in the normal case: `AgentSaga.Handle(LLMResponseReceived)` intercepts a response whose tool calls are *all* `wait` (and only for `AgentAction.Narrate`) and completes the saga right there — no tools run, no follow-up LLM call, no `Message` row, nothing broadcast but the `Completed` step that clears the client's activity chip. Silence is therefore not the same as the empty-narrative guard in `Handle(NarrativeReady)`, which still treats a blank response as a failure to retry. `GameHub.BuildNarrateSystemPromptAsync` forbids `wait` once the GM has been silent for `WaitStreakLimit` (3) consecutive table messages, so a model that settles into waiting can't leave the table talking to itself forever.

### Combat is zones, driven from chat

The game is played in chat, so combat has no grid. A fight is laid out as 2-5 named **zones**
(`Combat.Zones`, in order — neighbours are adjacent), and each participant stands in one
(`CombatParticipant.ZoneId`). Same zone is melee reach, the next zone is a move. A player *says*
"I run to the tower door"; the narrator calls `moveParticipant`. The client's combat strip only
shows where everyone is — nothing is dragged.

**`ICombatTracker` is the only thing that changes combat state**, and only the AI's combat tools
call it — there are no manual combat controls. After every change it pushes the whole combat as
`CombatUpdated` (also sent to a client on `JoinGameGroup`, so a reload restores the panel). The
old per-change events mostly had no client listener, and the AI's `startCombat` broadcast
nothing at all, so fights the narrator started were invisible. Tools address combatants and
zones **by name** and always act on the game's single active combat — the model is never given
ids it would have to invent. `startCombat` puts every player character in the fight with
initiative already rolled; a second `startCombat` while one is active is refused.

`DescribeForNarratorAsync` puts the fight into the narrate system prompt: zones, who is where,
HP, whose turn, and whether the player speaking right now is the one acting — an in-character
line out of turn is table talk or a reaction, not a full turn. Turn changes and the fight's
start and end are posted as `Type "Combat"` messages, so the log reads as the table heard it.
Damage to a player character's participant is written through to `Character.CurrentHP`.

### Rolls, rerolls, and who is asked what

`requestPlayerRoll` takes a `mandatory` flag. A mandatory roll — a saving throw, initiative,
anything the rules give the character no choice about — resolves immediately with no prompt;
asking was always meaningless, because the confirm endpoint never collected a rolled value
and approval only ever meant "yes, apply the rules to me". Optional rolls still gate on
confirmation. `IGMToolRegistry.RequiresConfirmation` therefore takes the *arguments*, not just
the tool name.

**Players never roll.** Every roll attributed to a player is the GM's `requestPlayerRoll` or
`deathSave`, made for them through `IPlayerRollService` — optional rolls ask first, mandatory
ones are rolled and reported — and any reroll offer holds the turn. `IGMToolRegistry.IsPlayerRoll`
routes both tools through `ToolExecutionHandler`'s roll path; `PlanPlayerRollAsync` resolves the
named character at every stage (ask, confirm, reroll) and `FinishPlayerRollAsync` applies the
settled number — a death save is recorded only after any reroll is decided.

`IRerollService` answers it, against `Character.Features` (`[{id, name, usesRemaining}]`) and
`IFeatureCatalogue`. The character row stores only the id and the remaining uses; the trigger,
the maximum and which rest restores it live in the catalogue, so a rules fix is a one-line
change and not a backfill across every campaign. Triggers are `AnyRoll`, `NaturalOne` (needs
`DiceResult.NaturalD20` — a natural 1 is a property of the die, not of `Total`, which the
modifier has already moved) and `FailedCheck` (only offered when the roll had a stated DC, since
a roll with no DC cannot be known to have failed). The GM's `rest` tool refills them (through
`IRerollService.RestoreOnRestAsync`) — there is no player rest button.

**A reroll offer holds the GM's turn open.** `ToolExecutionHandler` parks the `GMToolCall` in
`AwaitingReroll` rather than publishing `ToolCallCompleted`, so the narrator is told the final
number. Without that hold the GM narrates a failure the player then rerolls into a success.

### Character suggestions

`ICharacterConceptService` is the wizard's "fill in the rest" button. It sends the campaign
premise, `RAGService.GeneratePlotContextAsync` (the narrator's own context block) and the
existing party, and asks for one JSON object back.

Two constraints are non-negotiable and easy to lose. **Race and background must come back as
values the wizard offers** — they are matched against `ICharacterCreationFactory`'s catalogues
and dropped when nothing matches, because an unmatched value renders as a silently empty MUI
dropdown rather than an error. And **attributes are forced onto the standard array by rank**:
models return point-buy totals, a 17, or five of the six, and the wizard warns about anything
that isn't the standard array — so an unconstrained suggestion would arrive pre-flagged as
suspect. Ranking keeps which ability the model thought mattered most.

Whatever the player has already typed is fixed and echoed back; both fields empty is the
"invent everything" path. The call goes through
`INarrativeGenerationFactory.GenerateStructuredAsync`, which shares preset resolution and LLM
logging with narration but deliberately applies neither the GM persona nor
`Game.LanguageDirective` — the directive would have translated the race and background ids too,
and they are matched against an English catalogue.

### Roll prompts are messages, not banners

A roll request and a reroll offer are real `Message` rows addressed to one player via
`WhisperToId`, carrying their actions in `Metadata` (`kind`, `toolCallId`, `options`). Answering
one **replaces that row in place** — `IRollPromptService.ResolveAsync` rewrites the content and
type and clears the whisper routing, so a private "the GM asks you to roll" becomes the public
roll where it already sat, pushed as `MessageUpdated`. `WithdrawAsync` (→ `MessageRemoved`)
handles a prompt that resolved into nothing worth showing.

Two consequences worth knowing. Because prompts are whispers, `MessageVisibility` already keeps
them out of the narrator's context — the GM asked the question and does not need to be told it
asked. And because they are persisted rows, a reload restores an unanswered prompt with its
buttons intact; there is no separate polling path keeping the UI honest.

When a prompt exists, the roll must not also post its own message — pass
`PlayerRollRequest.PostMessage = false` and resolve the prompt into the result, or the table
sees the roll twice.

The pending tool list lives on `ToolCallCoordinator.ToolCalls` — **not** on `AgentCall.Output`, which holds narrative text. Tools requiring confirmation (`requestPlayerRoll`) are persisted as `GMToolCall` rows with `Status = AwaitingConfirmation` and resume the saga via `ToolCallConfirmationResolved` when `/api/gmtools/{id}/confirm|decline` is called.

Tool arguments are LLM-generated: parse ids with `TryGetGuid` rather than `Guid.Parse`, and always scope looked-up entities to `gameId`.

### Nudging the narration

`IStoryNudgeService` is the creator's lever: a direction ("bring the cult back in") applied for
N narrator turns or until withdrawn (`/api/games/{gameId}/nudges`, creator-only, UI in the chat
page's Nudge dialog and on the admin dashboard). `BuildNarrateSystemPromptAsync` and the opening
narration spend a turn from each limited nudge (`TakeForNarrationAsync`); `PlotWeaver` reads
them without spending one, and creating one triggers a plot review. They are never shown to
players and never enter `RAGService` context — that block also feeds player-facing OOC
answers. This replaced "sway", whose direction text nothing ever read.

### LLM Provider Abstraction

`ILLMProviderFactory` resolves to `ILLMProvider` based on `LLMPreset.ProviderType`. Supported values: `ollama`, `openaicompatible`, `openai`, `google`. API keys stored encrypted at rest via AES-256-GCM (`ApiKeyEncryptionService`).

### What the narrator is allowed to have seen

`MessageVisibility.IsVisibleToNarration` (`Data/MessageVisibility.cs`) is the **only** definition
of which messages may reach the GM: not OOC, and no whisper routing in either direction. Anything
that builds prompt context or writes `Message.Embedding` goes through `.VisibleToNarration()` —
`RAGService.GeneratePlotContextAsync`, `GenerateSessionSummaryAsync`, `EmbedMessagesAsync`,
`EmbedMessageAsync`, and `NPCRelevanceService`. This was previously spelled out inline in four
places with three different predicates, and the weakest one (`!IsOOC` alone, in the session
summary) put whispers into the "Previously..." recap that `GameStartService` publishes to the
whole table.

Decide on `WhisperFromId`/`WhisperToId`, never on `Type == "Whisper"`: a private GM-suggest reply
is stored with `Type "GM"` and only its `WhisperToId` marks it private. The converse needs no
rule — a public in-character line carries no routing fields and is therefore included, which is
exactly why a character repeating a whisper publicly *should* influence narration.

Embeddings are filtered at the **write** site, not at each read. Nothing similarity-searches
`Message.Embedding` today (only `PlotThread.Embedding`), so a read-side filter would be a rule
the next feature could forget; a null vector cannot leak.

`Message.IsSecret` is a **different** predicate and is deliberately not consulted here. A secret
roll is secret from the other *players*; the GM is who it is secret for, and a narrator that
could not see it could not resolve the check it just asked for. Player-facing visibility lives in
`MessagesController` (roller, or the game's creator). Do not express "secret" with whisper
routing — that would hide it from the narrator too.

### RAG + Plot Intelligence

`RAGService` builds GM prompt context from recent messages, active plot threads, and NPC states using pgvector HNSW embeddings on `Message.Embedding` and `PlotThread.Embedding`. `PlotWeaver` runs periodic LLM-driven reviews to adapt and generate plot threads.

### SignalR Hub

`GameHub` is a partial class split across ~19 files (combat, dice, spells, inventory, whispers, character creation, etc.). JWT is extracted from the WebSocket query string (`?access_token=…`). Online players tracked in a static `ConcurrentDictionary<string, string>` (connectionId → userId).

**Every hub method that takes a `gameId` must start with `RequireMemberAsync(gameId)`** (on `GameHub.cs`). `Clients.Group(...)` does *not* require the caller to be in the group, so an unchecked method lets any authenticated user act in any game. The hub is deliberately small — chatting, whispering, asking the GM — because nothing a human does changes game state directly. Prefer `Clients.User(userId)` over scanning `_playerConnections`.

### JSONB Storage

Character attributes, skills, inventory, spells, conditions, NPC data, plot adaptation history, and combat metadata are all stored as PostgreSQL `jsonb` using EF value converters. Avoids migrations for game-system-specific data changes.

Value comparers must compare by **content**, not by identity or length — the `MilestoneEvents` comparer compared `a.Count == b.Count`, so in-place edits were invisible to the change tracker and were never persisted.

## Authorization

This is the convention most easily broken by adding a new endpoint, and the one with the worst consequences.

**Controllers that operate on a game derive from `GameScopedController`** and gate every action:

```csharp
if (await RequireMember(gameId) is { } failure) return failure;   // any player in the game
if (await RequireCreator(gameId) is { } failure) return failure;  // the GM only
```

When the route carries an entity id rather than a `gameId`, load the entity first and authorize against **its** owning game — never against a game id supplied by the caller.

Rules of thumb:
- Reading game content → `RequireMember`. Mutating world state, driving the GM, or anything that spends LLM budget → `RequireCreator`.
- A `Character` reaches its game through `Player.GameId`, and `Player` is soft-deleted independently of `Character` — read past the query filter with `IgnoreQueryFilters()` or a kicked player's sheet throws.

## Serialization

**Never return EF entities that reach a `User` or an API key.** Use a DTO. Two live leaks came from this: `Player.User.PasswordHash` via `GET /api/games/{id}/players`, and `LLMPreset.DecryptedApiKey` via the preset endpoints. `[JsonIgnore]` now guards both, but the DTO is the real fix — see `PlayerDto`, `LLMPresetDto`.

Request bodies bind to DTOs, never to entities: binding `NPC`/`PlotThread`/`Character` directly let callers set `Id`, `GameId`, `UserId` and `IsDeleted`.

Client TypeScript types must describe the **wire DTO**, not the EF entity. Most of `src/types/` originally described entities, so fields the UI read were never actually sent and silently rendered `undefined`.

## Key Conventions

- **`TreatWarningsAsErrors = true`** — fix all compiler warnings before committing.
- **`nullable enable` + `ImplicitUsings`** — nullability annotations required throughout.
- **Primary constructor syntax** (C# 12) used everywhere.
- **Interface-per-service** — every service has a matching `IServiceName` registered in DI. Scoped for anything touching `AppDbContext`; singleton for stateless services (`IDiceEngine`, `ISystemRegistry`, `IGameAgentManager`, `IApiKeyEncryptionService`, `ILLMProviderFactory`).
- **Soft delete** — `ISoftDelete` on `Game`, `Player`, `Message`, `Character`, `NPC`, `PlotThread` with global EF query filters.
- **CamelCase JSON + `JsonStringEnumConverter`** for all API responses. Note this applies to MVC only — a bare `JsonSerializer.Deserialize` call uses case-*sensitive* defaults, which is how spell-slot consumption silently broke.
- **Hangfire** (PostgreSQL-backed, 2 workers) for background jobs alongside Wolverine.
- **Swagger is Development-only by default** (`SWAGGER__ENABLED` to override). It documents the full attack surface and cannot sit behind bearer auth, so it is not served anonymously in Production.
- **No HTTPS in the app layer** — TLS terminates at a reverse proxy. Never add `UseHttpsRedirection` or `UseHsts`.
- **Never swallow errors.** No bare `catch { }` on the server, and no `catch { /* silent */ }` in client hooks — expose an `error` and surface it. Silent failure is why a non-functional chat, ~20 dead routes and a broken agent loop all went unnoticed.
- No test projects exist in this solution. There is consequently no safety net: verify changes by running the stack and exercising the affected flow.
