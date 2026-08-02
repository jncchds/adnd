# Release Notes

## v0.1.2 — 2026-08-02

### Game language now actually drives narration

`Game.Language` was captured at game creation and shown in the UI, but never once read anywhere
in the LLM call path — every game narrated in whatever language the model defaulted to,
regardless of what the GM selected.

- Added `Game.LanguageDirective`, appended to every GM system prompt: chat narration and
  GM-suggest (`GameHub.AgentMethods.cs`), opening narration (`NarrativeGenerationFactory`), the
  `/api/llmtrigger` endpoints, PlotWeaver's thread generation/adaptation/milestone/opportunity
  prompts, and RAGService's session recap/consistency-check/continuation-suggestion prompts.
- Plot thread `category` enum values are explicitly pinned to English in the prompt text so
  `Enum.TryParse` doesn't fail when the rest of the JSON response comes back translated.
- Added Ukrainian to the game-creation language dropdown.

### Game join no longer requires a character name upfront

- `POST /api/games/join` (`JoinByCodeRequest`) dropped the `CharacterName` field — character
  creation happens in-game after joining, and `CharactersController.Create` now syncs
  `Player.CharacterName` from the created character so the join-time placeholder isn't stale.

### GM tool results now reach chat directly instead of hoping the narrator mentions them

`rollDice` and `skillCheck` only ever returned a string that was folded into a second,
tool-disabled "narrate the results" LLM call — if that call's prose didn't happen to mention
the roll, the roll simply never appeared in chat, even though it succeeded and was visible in
the LLM logs.

- `rollDice`/`skillCheck` (and an approved `requestPlayerRoll`) now write and broadcast their
  own `DiceRoll`/`SkillCheck` chat message immediately via SignalR, the same way
  `GameHub.RollDice` already does for player-initiated rolls.
- `sendWhisper` saved its `Message` row but never broadcast it over SignalR — the target player
  only saw it after a manual refresh. It now pushes to `Clients.User(target.UserId)` live,
  mirroring `GameHub.SendGMWhisper`.
- `generateLoot` was a complete no-op stub (`return "Loot generated for combat";`, ignoring its
  own arguments). It now validates the defeated NPCs actually fought in that combat, pulls each
  NPC's `Inventory`, rolls gold, and posts a `Loot` chat message with the result. Items are not
  auto-transferred into any `Character.Inventory` — no claiming flow exists yet
  (`ICombatInventoryService` only has `UseItemAsync`, no `AddItemAsync`).

## v0.1.1 — 2026-08-02

### GM turn observability + live activity feedback

Players had no indication a GM turn was progressing versus hung — the chat view showed a
static "GM Active" badge whether the GM was idle, mid-turn, or dead. Admins had no visibility
into what an agent call actually did beyond its final output.

- New `IGmActivityBroadcaster` pushes a `GMActivity` SignalR event as `AgentCall` moves through
  each saga step (dispatch, tool execution, follow-up, etc.), and as `GameStartService`'s
  opening-narration pipeline (plot generation, recap, narration) progresses — a path that
  previously gave zero feedback for 20–30+ seconds.
- The chat bottom bar now shows a live, friendly label ("The GM is thinking…", "Rolling
  dice…", "Weaving the opening plot…") with a spinner, falling back to the static "GM
  Active"/"GM Paused" badge once idle. Removed the `GMThinking`/`GMDoneThinking` client
  listeners, which the server never actually emitted.
- `AgentCall.StepHistory` records every saga-step transition with a timestamp, rendered as a
  table in the admin UI. `AdvanceStep` collapses consecutive-duplicate entries so Wolverine's
  retry-on-transient-failure redeliveries don't show up as fake repeated progress.
- New `GET /api/gmtools/by-call/{agentCallId}` lists the tool calls executed for a turn.
- `LLMInteractionLog.Reasoning` persists the model's separate "thinking" output where the
  provider exposes it: Ollama's `Thinking` field, OpenAI-compatible gateways'
  `reasoning_content` (OpenRouter/DeepSeek-R1/vLLM), and Gemini's `thought`-flagged parts
  (now requesting `includeThoughts`). Native OpenAI's Chat Completions API doesn't expose
  reasoning text, so that path stays `null`. Shown in a dedicated panel in Admin > LLM Logs.
- `useToolCalls.decline()` is now wired to a Decline button in the chat UI — previously only
  Confirm was rendered, so a player who wanted to skip a gated roll had no way to say so and
  the call sat until the saga's 5-minute timeout failed the turn.
- Removed dead, unauthorized SignalR methods (`GameHub.ToolCalls.cs`: `ConfirmPlayerRoll`,
  `DeclinePlayerRoll`, `ConfirmToolCall`) — never called by the client, touched no real state,
  and skipped the `RequireMemberAsync` check every other hub method enforces.
- "Load older messages" no longer flashes on a brand-new, empty session — `hasMore` no longer
  defaults to `true` before the first page load resolves.
- Sidebar redesign: removed standalone caption text so it's composed entirely of interactive
  controls; header is now a single burger-icon button that collapses/expands the sidebar; added
  a version pill (linking to the repo) alongside the ALPHA badge.

### Core gameplay loop and hardening

The scaffolded build had never been exercised end to end. This pass made the core gameplay
loop function and closed broad cross-tenant access on the API.

**GM agent loop:**
- `AgentSaga` could not correlate 4 of its 5 messages — Wolverine resolves saga identity from
  `Id`/`SagaId`/`AgentSagaId`, and the events carried only `AgentCallId`. Added `[SagaIdentity]`.
- Tool calls are now persisted on `ToolCallCoordinator.ToolCalls` instead of being parsed out of
  `AgentCall.Output` (narrative prose).
- `LLMDispatchHandler` now passes `IGMToolRegistry.GetToolDefinitions()` so the GM can actually
  call tools; `ToolCount` is tracked accurately instead of hardcoded `0`.
- Confirmation-gated tools (`requestPlayerRoll`) are persisted and resumable via
  `ToolCallConfirmationResolved` instead of dead-ending the saga until timeout.
- Retry logic split into `AgentCallFailed` (retry decision) and `AgentCallAbandoned`
  (saga-owned terminal state) so two `DbContext`s no longer wrote contradictory status.
- Follow-up LLM calls carry full system/user prompts (GM persona and context) instead of empty
  ones. Real `CancellationToken`s throughout; coordinator rows cleaned up on completion.

**Client/server contract:**
- Corrected SignalR call signatures across chat, OOC, whispers, and combat events, which had
  drifted from the hub's actual method signatures.
- Fixed ~20 client calls pointed at nonexistent routes, invite code join/generate field-name
  mismatches, a stale closure that prevented chat history from loading, a StrictMode
  reconnect bug in `useGameHub.disconnect()`, and 401 handling that discarded error bodies.
- Replaced silent `catch {}` in every data hook with real error state; added a route-level
  `ErrorBoundary`.

**New endpoints and navigation:**
- `GET /api/games/sessions/{sessionId}/messages`, `GET /api/gmtools/pending`,
  `POST /api/gmtools/{id}/confirm|decline`, `GET /api/games/{id}/sessions`,
  `PATCH /api/characters/{id}/adjust`.
- Game and Game Admin split into separate sidebar sections, making all eight admin pages
  reachable (seven previously had routes but no navigation).

**Security:**
- Fixed data leaks: `GET /api/games/{id}/players` returning BCrypt hashes and emails; LLM
  preset endpoints returning provider API keys in cleartext (`[JsonIgnore]` + DTOs on both).
- Added a fail-closed `GameScopedController` base and applied it across ~14 controllers that
  previously accepted a caller-supplied id with no membership check (systemic IDOR).
- Added membership/creator checks across the SignalR hub, which previously enforced them on
  only one method (`JoinGameGroup`).
- Rejected the `OVERRIDE_IN_ENVIRONMENT` JWT signing key placeholder at startup with a 32-byte
  floor, closing a path where a missing env var would boot the app signing tokens with a value
  published in the repo.
- Added SSRF guarding on `POST /api/llmpresets/models`, partitioned rate limiters (previously
  one shared bucket could lock out every user), CSP headers ahead of static file serving,
  Swagger/Hangfire access restrictions, DTOs in place of direct entity binding (mass
  assignment), session revocation on password change, `RandomNumberGenerator` for invite
  codes, bounded dice formulas, and a global exception handler.

**Data correctness:**
- Fixed unlimited spell casting caused by case-sensitive deserialization reading all slots as
  `(0,0)`.
- Fixed the combat grid overwriting unrelated `Combat.Notes` keys, a value comparer that
  compared `MilestoneEvents` by count and missed in-place edits, and a PlotWeaver cutoff bug
  that soft-deleted every resolved thread on each pass (added `PlotThread.ResolvedAt`).
- Fixed RAG embedding cache key collisions, a message broadcast race that could send someone
  else's message, `sendWhisper` never reading its target, unscoped LLM-supplied entity ids,
  death saves not resetting on stabilisation, and a turn counter that never decremented.

**Performance/robustness:**
- Fixed a socket leak in the Ollama provider, unhandled safety-block/`MAX_TOKENS` responses
  from Google, opaque failures on OpenAI-compatible gateways returning `200` with an error
  body, silently disabled RAG from a missing embedding model config, a tool-call parser that
  could mistake narrative JSON for tool calls, a health check that only counted table rows, and
  N+1 queries in initiative rolling and PlotWeaver duplicate detection.

**Chore:**
- Removed the unreferenced `Services/Llm/*Strategy.cs` layer and a singleton `HandlerRegistry`
  holding a latent captive `DbContext`.
- Added `IDesignTimeDbContextFactory` so `dotnet ef` no longer boots the full host, a container
  `HEALTHCHECK`, and `restart: unless-stopped`.
- Migrations: `AgentLoopToolCallState`, `PlotThreadResolvedAt`.

### Initial build

Full scaffold and feature build across ten phases: backend data layer and JWT auth; LLM
provider abstraction (Ollama, OpenAI, OpenAI-compatible, Google) with encrypted key storage and
resilience policies; the Wolverine event/saga framework and 12-tool GM tool registry; the
combat system (12 domain services) and SignalR `GameHub`; RAG + PlotWeaver plot intelligence;
the full React 19 + MUI SPA (auth, dashboard, chat, combat panel, character sheet/wizard, eight
admin pages, LLM preset management); rate limiting, security headers, and Docker Compose
production packaging.
