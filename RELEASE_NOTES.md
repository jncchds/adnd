# Release Notes

## v0.1.2 — 2026-08-02

### The GM no longer answers every line with a paragraph

The narrate system prompt told the GM to narrate vividly and "end with an open question or clear
call to action" — on every single turn, because every in-character player line fires one narrate
call. A character asking the innkeeper a question came back as scene-setting the players already
had, plus a "what do you do next?" welded onto the end. Between that and `wait`, the GM had only
two volumes, full scene or total silence, and defaulted to the loud one.

- `BuildNarrateSystemPromptAsync` now instructs the model to match response length to the moment,
  and names the small turns explicitly — a single line of NPC dialogue, a one-sentence answer to
  what a character asks or examines, a brief result of a minor action, a short ruling — as
  complete, correct turns rather than lazy ones. Vivid narration is reserved for a new place, a
  new arrival, combat opening or turning, a revelation.
- The call to action is now conditional on the party actually facing a choice, instead of being
  mandatory on every response.
- Prompt-only change: no new tool, no second LLM pass, no schema or migration.

### NPCs introduced in narration now register themselves

An NPC only existed if the human GM typed it into the admin UI. Anyone the AI GM invented in
narration lived exactly as long as the RAG window that quoted the message, then came back the
next scene with a different attitude, faction, or name spelling.

- New `registerNPC` GM tool (name, description, attitude, faction). Matching is by name,
  case-insensitively and scoped to the game: an existing name updates that NPC instead of
  creating a duplicate, and only the fields the model supplied are written, so an
  attitude-only follow-up call can't blank the description.
- `GameHub.BuildNarrateSystemPromptAsync` lists the already-registered NPCs and instructs the
  model to call `registerNPC` **in the same response as `narrate`** — no second LLM pass to bill
  or to fail — while excluding unnamed background extras.
- Ids are never taken from the model here; there is no `npcId` argument to hallucinate.

### NPCs can now die, leave, and stop crowding the prompt

- `NPC.Status` (`Active | Dead | Departed`) and `NPC.LastSeenAt`, plus the `updateNPCStatus` GM
  tool the narrator calls in the same response when someone dies or is written out. It refuses
  to create: a status change naming an unregistered NPC means the model invented the name.
  `registerNPC` revives a `Departed` NPC (they were just written back into a scene) but never a
  dead one. Migration `NPCStatusAndLastSeen`; existing NPCs default to `Active`.
- New `INPCRelevanceService` picks the NPCs a prompt actually gets: whoever the last 20 table
  messages named, then `Active` NPCs by recency, capped at 8. Both the narrate system prompt
  and `RAGService.GeneratePlotContextAsync` now go through it, instead of each dumping the
  entire campaign roster — descriptions and all — and burying the two people in the room.
  Dead and departed NPCs appear only while the party is still talking about them.
- `queryNPCs` stays unfiltered as the escape hatch ("who was that innkeeper three towns back")
  and now reports status and faction. The admin NPC page shows and edits status.

### The GM can now stay silent while the party talks

Every in-character line a player sent fired a narrate call, and the GM had no way to answer
"nothing needs to happen here" — it either invented a beat or returned nothing, which the
empty-narrative guard turned into a retry and finally a red GM error for the whole table.

- New `wait` GM tool. A response whose tool calls are all `wait` is intercepted in
  `AgentSaga.Handle(LLMResponseReceived)` and completes the turn silently: no tools execute,
  no follow-up LLM call is billed, no chat message is written, and the only broadcast is the
  `Completed` step that clears the activity chip. Only applies to `Narrate` calls — the other
  actions are explicit requests for output, where silence would look broken.
- The reason (if the model gave one) is stored on `AgentCall.Output` so a waited turn is
  distinguishable from a broken one in the admin views.
- `GameHub.BuildNarrateSystemPromptAsync` explains when to wait, and forbids it once the GM
  has been silent for 3 consecutive table messages so a model can't settle into waiting
  permanently.

### Dice results were unreadable, and a bad formula silently under-rolled

- `DiceEngine.Roll` skipped any token its regex didn't recognise, so an LLM-emitted placeholder
  like `1d20+{strength}` dropped that whole term and quietly returned a wrong total. Unparsed
  fragments now throw with the offending text named.
- The GM prompt, `rollDice` and `requestPlayerRoll` tool descriptions, `queryCharacter`, and the
  per-character prompt lines now all carry ability modifiers as text (`STR +2, DEX +0, …`, via
  the new `AbilityScoreHelper`) so the model can write a real number into a formula itself.
- `rollDice`/`requestPlayerRoll` take an optional `dc`. When present the chat message states
  Success/Failure, the value is returned to the follow-up narration call (which previously had
  to guess the outcome), and the pending-roll banner shows the target number.
- Roll messages no longer print the total twice (`17 (+[14]d20=14 +3 = 17)`), and a single-term
  roll drops the redundant trailing `= N`. Dice chat bubbles render as plain highlighted text
  instead of dumping raw `JSON.stringify(metadata)`.

### GM-suggest replies were public, and the question vanished

- `TriggerSuggest` now saves the player's own question as a private OOC message so it stays in
  their log, and tags the agent call with `AgentCall.RequestedByPlayerId` (new migration).
- The reply is delivered to just that player and no longer overwrites the game's
  "last GM action" on the admin overview. `MessagesController` now hides any message carrying
  whisper routing rather than only `Type == "Whisper"`, so the private reply stays private on
  history reload too.
- The GM-suggest system prompt was a bare one-liner; a local model responded by reasoning at
  length and emitting no text at all. It now explicitly asks for a direct OOC answer, and
  `LLMDispatchHandler` no longer offers the 12-tool schema on `Suggest` calls at all.

### GM activity chip lost or stuck on reconnect

- `IGmActivityBroadcaster` caches the current step per game and `JoinGameGroup` replays it, so a
  client that joins mid-generation catches up instead of showing a static "GM Active".
- `GameStartService` now broadcasts `Completed` on the success path too — without it the cache
  stayed stuck on "Writing the opening scene…" for anyone who reconnected later.

### Narration lost context on every tool-following turn and every fresh player message

Admin > LLM Logs showed GM calls going out with almost nothing in them, and narration read as
disconnected turn to turn — confirmed live: a player action that triggered a dice-roll tool call
came back from the follow-up LLM call as if the roll were all that happened.

- `LLMFollowUpHandler` built its prompt from `Tool results:\n{summary}` alone — `LLMFollowUpRequested.UserPrompt`,
  which carries the original player action, was received but never read. Any GM response
  involving a tool (dice rolls, skill checks, combat) lost all memory of what the player had
  actually done.
- `GameHub.TriggerNarrate`/`TriggerSuggest` sent the LLM only the player's raw one-line message
  plus static campaign metadata (plot threads, PCs) — `RAGService.GeneratePlotContextAsync`
  already builds recent-message/plot/NPC/character context but was never wired into the live
  narration path. Both hub methods now prepend that context to the user prompt.
- Admin dashboard's "Start Game" control now subscribes to the same `GMActivity` SignalR feed
  the chat page uses, showing "Weaving the opening plot…" / "Writing the opening scene…" instead
  of just a disabled button for the ~30–60s the game-start pipeline runs. The label mapping was
  extracted to `utils/gmActivity.ts` so both pages share it.

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
