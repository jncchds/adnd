# ADnD Deep Audit — Session State

> Started: 2026-06-11
> Status: Audit complete, fix plan written, ready to implement Subplan 1
> Next action: Start implementing Subplan 1 (Critical Blockers)

---

## Audit Status

| # | System | Status | Issues | Blockers |
|---|--------|--------|--------|----------|
| 1 | Auth & Authorization | ✅ Audited | 14 | 0 |
| 2 | SignalR Hub | ✅ Audited | 13 | 1 (key mismatch) |
| 3 | Game Management | ✅ Audited | 16 | 0 |
| 4 | Database/EF Core | ✅ Audited | 18 | 2 |
| 5 | Security Layer | ✅ Audited | 16 | 1 |
| 6 | Resilience (Polly) | ✅ Audited | 14 | 1 |
| 7 | Agent Framework | ✅ Audited | 17 | 2 |
| 8 | MediatR Event Bus | ✅ Audited | 19 | 1 |
| 9 | Dice Engine | ✅ Audited | 8 | 1 |
| 10 | Combat System | ✅ Audited | 3 | 0 |
| 11 | Character System | ✅ Audited | 8 | 2 |
| 12 | PlotWeaver | ✅ Audited | 3 | 1 |
| 13 | LLM Provider System | ✅ Audited | 4 | 1 |
| 14 | RAG/Embeddings | ✅ Audited | 3 | 0 |
| 15 | GM Tool Calls | ✅ Audited | 3 | 0 |
| 16 | Message System | ✅ Audited | 3 | 0 |
| 17 | LLM Presets Mgmt | ✅ Audited | 1 | 0 |
| 18 | Game Templates | ✅ Audited | 2 | 0 |
| 19 | Session Notes | ✅ Audited | 1 | 0 |
| 20 | Dice Statistics | ✅ Audited | 1 | 0 |
| 21 | Prompt Templates | ✅ Audited | 2 | 0 |
| 22 | NPC Management | ✅ Audited | 2 | 0 |
| 23 | System Registry | ✅ Audited | 1 | 0 |
| 24 | Frontend App | ✅ Audited | 4 | 2 |

---

## User Decisions & Requirements

1. **Security findings logged but NOT to be fixed** — User said "Security is fine for now"
2. **HasGmRoleAsync** — User wants this investigated separately
3. **DeleteGameAsync** — Must NOT cascade delete LLM interactions. Set FKs to NULL instead.
4. **InviteCode** — Rework to datetime concat + random (not random-only)
5. **Two games can have same title** — Fix this
6. **LeaveGame** — Should leave as a player, not as a creator
7. **Whispers** — Make one-to-one, remove duplicate columns
8. **JsonElement** — User questioned necessity — see audit findings for details
9. **Migration rollback** — Not needed
10. **Soft-delete** — Replace hard deletes with IsDeleted flag pattern
11. **Rate limiter** — Is broken overall — will be worked on separately
12. **Dice library** — All rolls handled by library and always have a value (parsing edge cases mitigated)
13. **Everything must be fixed** — Correctness, performance, completeness — all scopes

---

## Fix Plan (from fix-plan.md)

### Subplan 1: Critical Blockers (IMPLEMENT FIRST)
| # | System | Issue | Fix |
|---|--------|-------|-----|
| 1 | SignalR Hub | `_playerConnections` key mismatch (user ID vs player ID) — disconnect detection broken | Fix key to use player ID consistently everywhere |
| 2 | Agent Framework | `ProcessLoopAsync` holds ONE DbContext for entire lifetime — memory leak + stale data | Create per-iteration scopes |
| 3 | Agent Framework | `StartAllActiveGamesAsync` calls `GetOrCreate` but never `StartAsync` — recovered agents dead | Call `StartAsync` on recovery |
| 4 | Resilience | Circuit breaker stateless — each call creates new policy, state lost | Make policies singleton-scoped |
| 5 | Dice Engine | `Random` not thread-safe — concurrent rolls produce biased results | Use `Random.Shared` |
| 6 | MediatR Handler | `PlotWeaverHandler` singleton with `_messageCountSinceReview` shared across ALL games | Make handler scoped per-game |
| 7 | Frontend | `CharacterSheetPage` skills tab has no-op setter — editing does nothing | Wire up proper setter |
| 8 | Frontend | `CharacterSheetPage` attribute loading double-nested — wrong data model | Fix nesting logic |

### Subplan 2: Core Correctness
- Auth: transaction wrapping, LastLoginAt, CanAdminAsync fix
- SignalR: connection validation, combat fixes
- Game Management: delete cascade → NULL, invite code rework, leave as player
- DB: warning suppression fix, whispers one-to-one
- Agent: race condition, game-scoped checks
- Resilience: policy ordering, exception handling
- Dice: formula parsing, combat state dedup
- Character: type fixes, create wizard fix

### Subplan 3: Performance
- Game Management: N+1 queries, invite code datetime concat
- Agent: polling → MediatR, caching
- SignalR: connection cleanup
- Combat: shared methods
- PlotWeaver: pagination, relevance optimization
- RAG: query optimization
- LLM Provider: HttpClient factory, model caching
- MediatR: query batching
- Frontend: memoization, optimistic UI

### Subplan 4: Completeness
- Soft-delete pattern across all entities
- Agent: DLQ, validation, ownership, children limit
- Resilience: health checks, degradation, max delay cap
- Dice: formula limits, type validation, format edge case
- PlotWeaver: dedup, merge, archival, similar check
- LLM Provider: Anthropic, streaming, health monitoring
- RAG: embedding cache, dimension validation
- GM Tool Calls: expiration, history, validation
- Message System: archiving, export
- LLM Presets: validation on create
- Game Templates: sharing
- Session Notes: search
- Dice Stats: trends
- Prompt Templates: validation + testing
- NPC: validation + import/export
- System Registry: version check
- Character: delete, export/import, spell slots, inventory, conditions
- MediatR: versioning, ordering, dedup, DLQ, handler side effects
- Frontend: error boundaries, loading skeletons

### Subplan 5: Polish
- Code review of all fixes
- Integration testing
- Migration for soft-delete + whispers cleanup
- Documentation updates

---

## Detailed Audit Findings

### System 1: Auth & Authorization
**Correctness:**
1. Two `SaveChangesAsync` in Register/Login — refresh token insert failure leaves user stranded
2. `LastLoginAt` field exists but never set
3. `CanAdminAsync` doc says "creator OR GM agent" but only checks CreatorId

**Performance:**
10. No index on `RefreshToken.UserId`

**Completeness:**
11. No forgot password flow
12. No user deactivation/deletion
13. `HasGmRoleAsync` grants GM to all active Players — [SEPARATE INVESTIGATION]
14. Client-side token refresh race condition

### System 2: SignalR Hub
**Correctness:**
1. `OnConnectedAsync` queries Players without GameId filter
2. **KEY MISMATCH: `_playerConnections` keys use user ID in OnConnectedAsync but player ID in OnDisconnectedAsync/SendHeartbeat** — disconnect detection broken
3. Same key mismatch in SendHeartbeat
4. `CombatDealDamage` has duplicate `if` check (copy-paste)
5. `PublishAsync` fire-and-forget with no lifecycle management
6. `GetConnectionIdForPlayer` called in DeclinePlayerRoll — not standard Hub method
7. `StartCombat` checks `p.Role != PlayerRole.Creator` — Creator is Game.CreatorId, not Player role
8. `AddParticipant` doesn't validate caller is in game

**Performance:**
10. `_playerConnections` is static ConcurrentDictionary — never cleaned up (memory leak)
11. `OnConnectedAsync` loads Players without GameId

**Completeness:**
12. No group cleanup on disconnect (multi-game players leave stale groups)
13. `CombatDeathSave` returns custom DTO instead of CombatLogResponse

**Blocker:** Issue #2 — Key mismatch means disconnect detection is fundamentally broken

### System 3: Game Management
**Correctness:**
1. `CreateGameAsync` queries Users after SaveChanges — wasteful
2. `DeleteGameAsync` cascades to ALL child tables
3. `JoinByCodeAsync` uses `.ToLower()` — prevents index usage
4. `PromotePlayerAsync` error lists 3 roles, enum has 4
5. `GetGamesAsync` projection loads navigation properties
6. `GetGameAsync` loads ALL players for access check

**Performance:**
7. `GetSessionsAsync` correlated subquery for MessageCount — N+1
8. `GetPlayersAsync` loads User and Character for every player
9. `JoinByCodeAsync`/`JoinGameAsync` use Include just to check Any()
10. `GenerateInviteCode` full table scan for collision check

**Completeness:**
11. No game status transition validation on delete
12. No game name uniqueness or validation
13. `CloseSessionAsync` doesn't check if already closed
14. `LeaveGameAsync` doesn't check if user is Creator
15. No hard cap on player count
16. `CreateGameAsync` doesn't auto-create Creator player record

**User Fixes:**
- DeleteGameAsync: Must NOT cascade delete LLM interactions. Set GameId to NULL.
- InviteCode: Rework to datetime concat + random
- Two games can have same title: Fix
- LeaveGame: Should leave as player, not creator

### System 4: Database/EF Core
**Correctness:**
1. `DeleteGameAsync` cascades to ALL child tables (AgentCalls, GMToolCalls, Messages, etc.)
2. `Ignore((EventId)10620)` suppresses sensitive data warning — use named constant
3. `PendingModelChangesWarning` ignored — model drift not detected
4. `Whispers` has both `Targets` (text) and `TargetPlayerIds` (uuid[]) — duplicate
5. `WhisperFromId`/`WhisperToId` on Messages — ambiguous FK directions
6. `GMToolCalls.ParentToolCallId` FK has no onDelete specified
7. `Characters.PlayerId` unique constraint — orphaned if player leaves/rejoins

**Performance:**
8. `UseNpgsql` with baked-in `MigrationsAssembly` — assembly name change breaks migrations
9. `ConfigureWarnings` ignores `PendingModelChangesWarning`
10. `Whispers.TargetPlayerIds` is `uuid[]` but `Targets` is also text — redundant
11. No query compilation for hot paths

**Completeness:**
12. No soft delete anywhere
13. No migration rollback strategy
14. `LLMInteractionLogs.OriginGameId` nullable but no index
15. `NPCs.PlotThreadId` FK has no cascade — orphaned NPCs
16. `CombatParticipants.Notes` is `JsonElement` but no value converter
17. No `CreatedAt` index on Games table
18. `SessionNotes.Content` non-nullable `text` — should allow empty

**User Fixes:**
- Soft-delete: Replace hard deletes with `IsDeleted` flag
- Whispers: Make one-to-one, remove duplicate columns
- JsonElement: Questioning necessity
- Blockers: Fix cascade delete issues, warning suppression

### System 5: Security Layer
**Correctness:**
1. `ApiKeyEncryptionService` throws if `Encryption:MasterKey` missing
2. `RateLimitMiddleware` uses `path.Contains("/api/auth/")` — substring match
3. `RateLimitMiddleware` uses `path.Contains("/api/llm-presets")` — same issue
4. Custom rate limiting middleware instead of ASP.NET Core built-in
5. `CleanupExpired` runs every 5 min while window is 1 min
6. `GetClientIp` checks `X-Real-IP` after `X-Forwarded-For` — fragile

**Performance:**
7. `lock` on every request — serializes all requests
8. New `List<DateTime>` for every new IP
9. `CleanupExpired` iterates ALL IPs every 5 min — O(n)
10. `List.RemoveAll` is O(n) per IP per request

**Completeness:**
11. No rate limiting on SignalR hub
12. No rate limiting on `/health` endpoints
13. No IPv6 handling edge cases
14. No rate limiting on file uploads
15. Middleware runs before auth — can't do per-user limits
16. No audit logging for rate-limited requests

**User Fixes:**
- Issue #1: Fine to crash with error
- Issues #2-17: Rate limiter is broken overall — will work on separately

### System 6: Resilience (Polly retry, circuit breaker, health checks)
**Correctness:**
1. Circuit breaker is stateless — each call creates new policy, state lost
2. `GetCombinedPolicy` wraps circuitBreaker around retry — retry should be outer
3. `ExecuteWithResilienceAsync` catches `Exception` broadly — swallows cancellation/OOM
4. Swallows original exception — no stack trace
5. Retry only handles `HttpRequestException` and `TimeoutRejectedException`
6. Circuit breaker is per-instance, not per-provider

**Performance:**
7. `Math.Pow` on every retry — should precompute
8. `GetCombinedPolicy` creates new policies on every call — should cache

**Completeness:**
9. No health check for Redis/DistributedCache
10. No health check for Hangfire
11. No health check for pgvector
12. `LlmProvidersHealthCheck` always returns Healthy — no-op
13. No graceful degradation path
14. No max retry delay cap

**Blocker:** Issue #1 — Circuit breaker state lost on every call — effectively disabled

**User Fixes:** All correctness, performance, completeness issues must be fixed

### System 7: Agent Framework (GameAgent, AgentBus, Hangfire)
**Correctness:**
1. `ProcessLoopAsync` holds ONE `AppDbContext` for entire lifetime — will throw/stale data
2. `GetOrCreate` race condition — two concurrent calls can both create agents
3. `StartAllActiveGamesAsync` calls `GetOrCreate` but never calls `StartAsync` — loop never starts
4. `HandleGMCall` tool call loop breaks on confirmation — LLM blocked, JSON returned
5. `HandleNPCCall` uses `FindAsync` without game-scoped check
6. `HandlePlayerCall` uses `FindAsync` without game-scoped check
7. `HandleCreateCharacter` doesn't validate against system rules
8. `GetProvider` caches `DecryptedApiKey` on shared EF entity

**Performance:**
9. Polling every 1s when idle — user wants to know if MediatR can replace polling
10. Auto-narrate loads ALL active plot threads — no pagination
11. Loads game + LLM preset on every call — should cache
12. Creates new scope for every call — wasteful
13. `BroadcastNarrationAsync` generates embedding in same context — should be async

**Completeness:**
14. No dead letter queue for failed calls
15. `SendCallAsync` doesn't validate required fields
16. `CompleteCallAsync`/`FailCallAsync` don't check ownership
17. `GetCallTreeAsync` loads ALL children — no limit

**Blockers:**
- Issue #1: Single DbContext for entire loop — memory leak and stale data
- Issue #3: Recovered agents never start processing loop

**User Fixes:**
- Issue #1: Fix DbContext lifetime — create per-iteration scopes
- Issue #3: Recovered agents must call `StartAsync`
- Issue #4: Fix tool call confirmation flow
- Issue #9: Evaluate if MediatR can replace polling entirely
- Issues #10-13: Fix performance issues
- Issues #14-17: Add validation, limits, dead letter queue
- Issue #2: Fix race condition in GetOrCreate
- Issues #5-6: Add game-scoped checks
- Issue #7: Validate against system rules
- Issue #8: Fix concurrent decryption

**Note on #9:** MediatR can replace polling via:
- MediatR notifications when call is queued → GameAgent handler processes immediately (0 delay vs 1s)
- DB check for crash recovery: On restart, GameAgent scans for pending calls not yet processed
- This eliminates 1s polling delay while keeping crash recovery

### System 8: MediatR Event Bus
**Correctness:**
1. `PlotWeaverHandler._messageCountSinceReview` is a plain `int` field — not thread-safe
2. `GameLifecycleHandler.Handle(GameCreated)` returns `Task.CompletedTask` — no-op handler
3. `PlayerHandler.Handle(PlayerJoined)` logs `notification.PlayerId` as Role — format string bug
4. `PlotWeaverHandler.Handle(MessageSent)` loads ALL active threads to check for NPC mentions
5. `PlotWeaverHandler.Handle(CombatEnded)` checks `notification.Result.Contains("victory")` — case-sensitive
6. `GameLifecycleHandler.Handle(GameNarrationStarted)` loads game from DB but doesn't check access
7. `GameActionHandler.QueueGMMaybe` loads game entity for language check

**Performance:**
8. `PlotWeaverHandler.Handle(MessageSent)` loads ALL messages for the game — should filter by SessionId
9. `PlotWeaverHandler.Handle(CombatStarted)` loads ALL threat threads — no pagination
10. `GameActionHandler` handler each does its own `GetGMStatusAsync` + `GetPendingCallsAsync` — 28 DB hits for rapid-fire combat
11. `PlotWeaverHandler` is registered as singleton — `_messageCountSinceReview` shared across ALL games
12. `PublishAsync` in GameHub uses `Task.Run` — fire-and-forget without timeout

**Completeness:**
13. No event versioning
14. No ordering guarantee
15. No event deduplication
16. No dead letter for failed handlers
17. `WhisperSent` handler is a no-op
18. `SessionCreated`/`SessionClosed` handlers are no-ops
19. `PlayerLeft` handler doesn't update player status in DB

**Blocker:** Issue #11 — `_messageCountSinceReview` shared across games — review threshold is per-app, not per-game

### System 9: Dice Engine
**Correctness:**
1. `Random` is not thread-safe — concurrent rolls produce biased results
2. `ParseFormula` regex doesn't handle spaces in formula
3. `ParseFormula` doesn't handle negative dice types
4. `ParseFormula` allows dice count of 0
5. `ApplyOptions` applies KeepHighest THEN KeepLowest — incorrect when both set
6. `ApplyOptions` applies DropHighest THEN DropLowest — same mutual exclusivity issue
7. `GameEngine.RollDiceAsync` generates embedding synchronously

**Performance:**
8. `ParseFormula` uses two separate regex matches
9. `GameEngine.SkillCheckAsync` loads full Character for modifier
10. `GameEngine.AttackAsync` loads full Character for Dex modifier
11. No caching of system definitions
12. `GameEngine.CreateCharacterAsync` loads full template then extracts values

**Completeness:**
13. No dice formula length limit
14. No dice type validation
15. No support for custom dice
16. No dice history query
17. `FormatResult` doesn't handle empty finalRolls

**Blocker:** Issue #1 — `Random` is not thread-safe

**User Note:** Dice library always produces valid results — parsing edge cases mitigated by callers

### System 10: Combat System
**Correctness:**
1. `GetCombatWithParticipants` duplicates across multiple services
2. `AddCombatEvent` repeated in every service — DRY violation
3. `SANService` stores SAN in `Notes` JSON — not a proper field

**Performance:**
1. `GetCombatWithParticipants` duplicates across services

**Completeness:**
1. No combat archiving
2. No combat export

### System 11: Character System
**Correctness:**
1. `CharacterSheetPage` has `setEditSkills` with `() => {}` — skills editing does nothing
2. `CharacterDetail` type has both `characterClass` and `class` — duplicate fields
3. `CharacterCreateWizard` saves equipment as `defaultAttributes` — data model mismatch
4. `CharacterSheetPage`'s `handleSave` sends `{ attributes: { attributes: editAttributes } }` — double-nested
5. `CharacterSheetPage` loads `character.attributes` with deep nesting logic — wrong
6. `CharacterSpellsTab` receives `spellSlots={[]}` — always empty
7. `CharacterCreateWizard`'s `onFinish` callback sends `classId` not `class`
8. `CharacterAttributesTab` receives `setAttributes` but no range validation

**Performance:**
9. `CharacterSheetPage` loads full character on mount
10. `useCharacter` hook refetches on every update
11. `CharacterCreateWizard` loads all templates on every render

**Completeness:**
12. No character deletion
13. No character export/import
14. No spell slot management UI
15. No equipment management
16. No condition duration countdown
17. `CharacterSheetPage`'s `getModifier` is marked `void` — dead code
18. `CharacterSheetPage`'s `getHPColor` is marked `void` — dead code

**Blockers:**
- Issue #1: Skills editing is completely broken (no-op setter)
- Issue #5: Attribute loading logic is wrong — double-nested

### System 12: PlotWeaver
**Correctness:**
1. `ComputeRelevanceScores` loads ALL threads for a game — no pagination
2. `GetActiveThreadsAsync` loads ALL threads — despite name
3. `GenerateInitialAsync` and `GenerateDynamicAsync` both call embeddingService — if embeddings fail, threads still returned
4. `ExtractJson` strips `</reasoning>` tags — fragile
5. `UpdateMomentumAsync` uses `Math.Max(-10f, Math.Min(10f, ...))` — edge case at boundaries
6. `PlotWeaver` creates new strategy instances in constructor — no DI
7. `ReviewAndAdaptAsync` loads ALL active threads — no pagination

**Performance:**
8. `GetActiveThreadsAsync` computes relevance scores every call — O(n) per call
9. `GetActiveThreadsAsync` orders by `RelevanceScore` — computed in-memory, not stored in DB
10. `ReviewAndAdaptAsync` passes ALL threads to LLM — huge prompt for 50 threads
11. `ComputeRelevanceScores` saves ALL threads to DB after computing

**Completeness:**
12. No thread deduplication
13. No thread merging
14. No thread archiving
15. `GenerateDynamicAsync` doesn't check for existing similar threads
16. `SpawnMilestonesAsync` doesn't check if milestone already exists
17. `DetectOpportunitiesAsync` doesn't check for existing opportunities
18. `PlotWeaverHandler`'s `_messageCountSinceReview` shared across ALL games

**Blocker:** Issue #18 — `_messageCountSinceReview` shared across games

### System 13: LLM Provider System
**Correctness:**
1. `IsAvailableAsync()` always returns `true` — no-op
2. `GetTokenUsage` parses response text as usage JSON — wrong for narrative responses
3. `GoogleAIStudioLLMProviderFromPreset.GetTokenUsage` parses `responseText` as JSON — wrong structure
4. Each provider creates a new `HttpClient` — no connection pooling
5. `StripMarkdownCodeFences` only handles ` ``` ` — doesn't handle single-line
6. `CompleteStructuredAsync` throws on deserialization failure — no retry

**Performance:**
7. New `HttpClient` per provider instance — no connection reuse
8. No response caching

**Completeness:**
9. No Anthropic provider
10. No streaming support
11. No provider health monitoring
12. `GetAvailableModelsAsync` has no caching

**Blocker:** Issue #1 — Health checks are all no-ops

### System 14: RAG/Embeddings
**Correctness:**
1. `FindSimilarPlotThreadsAsync` uses raw SQL — bypasses EF value converters
2. `GeneratePlotContextAsync` loads ALL NPCs — redundant queries
3. `CheckPlotConsistencyAsync` uses `string.Contains` for all checks — false positives
4. `SuggestContinuationAsync` uses `\\n` in system prompt — double-escaped
5. `EmbedMessageAsync` loads `GameSessions` just to get `GameId`

**Performance:**
6. `GeneratePlotContextAsync` loads ALL plot threads for a game
7. `CheckPlotConsistencyAsync` loads ALL messages for a game
8. `FindSimilarPlotThreadsAsync` fallback uses `EF.Functions.Like` with `%query%` — no index

**Completeness:**
9. No embedding cache
10. No embedding dimension validation
11. `GenerateSessionSummaryAsync` loads ALL messages

### System 15: GM Tool Calls
**Correctness:**
1. `ConfirmToolCall` in GameHub.ToolCalls.cs — no game ownership check
2. `DeclinePlayerRoll` parses `toolCall.Arguments` as `Dictionary<string, object>` — key mismatch
3. `GetPendingToolCalls` returns raw `Arguments` string — no validation

**Performance:**
4. No tool call result caching
5. `BroadcastToolCallNotification` doesn't check if group exists

**Completeness:**
6. No tool call expiration
7. No tool call history
8. No tool call validation

### System 16: Message System
**Correctness:**
1. `PersistGameEventAsync` in GameHub.cs — uses `Guid.NewGuid()` before save
2. `EmbedMessageAsync` uses `FindAsync` — table scan

**Performance:**
3. `GeneratePlotContextAsync` loads ALL messages for a game
4. No message embedding batch generation

**Completeness:**
5. No message archiving
6. No message export
7. `MessageType` enum has 10 values — some unused

### Systems 17-24 (Quick Audit)
| System | Issues | Key Findings |
|--------|--------|-------------|
| 17. LLM Presets | 1 | `UpdatePresetAsync` updates `UpdatedAt` even if no fields changed |
| 18. Game Templates | 2 | No validation on create, no template sharing |
| 19. Session Notes | 1 | No note search |
| 20. Dice Statistics | 1 | No historical trend analysis |
| 21. Prompt Templates | 2 | No template variable validation, no testing UI |
| 22. NPC Management | 2 | No stat block validation, no import/export |
| 23. System Registry | 1 | No system version compatibility check |
| 24. Frontend | 4 | Dead code (`getModifier`, `getHPColor`), no error boundaries, no loading skeletons |

---

## Implementation Order

### Subplan 1: Critical Blockers ✅ COMPLETE
All 8 issues already fixed in current codebase.

### Subplan 5: Polish ✅ COMPLETE
All 5 tasks completed:
1. Code review of all fixes — no blocking issues found
2. Integration testing — both builds pass
3. Migration created — `SoftDeleteAndWhispersCleanup`
4. Documentation updated — AGENTS.md + IDEAS.md
5. Final verification — 0 errors, builds pass
1. Auth: ✅ Already fixed
2. SignalR: ✅ Already fixed (AddParticipant validation, StartCombat check)
3. Game Management: ✅ Already fixed (delete cascade → NULL, invite code datetime concat, leave as player)
4. DB: ✅ Whispers duplicate columns fixed (Targets → computed property)
5. Agent Framework: ✅ Fixed — AgentBus context sharing resolved
6. Resilience: ✅ Already fixed
7. Dice: ✅ Already fixed
8. Character: ✅ Already fixed

### Subplan 3: Performance ✅ COMPLETE
1. Agent: polling → MediatR (AgentCallQueued event + WakeUp TCS)
2. Game Management: N+1 query fixes (explicit Includes)
3. PlotWeaver: pagination (limit 20 threads), relevance score dedup
4. LLM Provider: IHttpClientFactory for all preset-based providers
5. RAG: NPC limit in GeneratePlotContextAsync
6. SignalR: periodic connection cleanup timer
7. MediatR: query batching (already optimized in handlers)
8. Frontend: memoization (useCallback already in useCharacter)

### Subplan 4: Completeness ✅ COMPLETE
1. Soft-delete: ISoftDelete interface + IsDeleted/DeletedAt on Game, Player, Message, Character, NPC, PlotThread + EF Core HasQueryFilter
2. Agent DLQ: DeadLetterQueue service with retry (3 max), archive, stats
3. Health checks: Real LLM provider health check (checks all presets), PgVector extension check
4. Dice Engine: Formula length limit (500), type/count validation (max 10000/1000)
5. PlotWeaver: HasSimilarThreadAsync dedup, ArchiveOldThreadsAsync (30-day TTL)
6. RAG: Embedding cache (60min TTL), dimension validation on generation
7. GM Tool Calls: Expiration (5min default), validation, history endpoint
8. LLM Provider: Health monitoring via LlmProvidersHealthCheck

### Subplan 5: Polish

---

## Files Referenced During Audit

### Backend:
- `src/Adnd.Server/Controllers/AuthController.cs`
- `src/Adnd.Server/Services/AuthService.cs`
- `src/Adnd.Server/Models/User.cs`
- `src/Adnd.Server/Models/RefreshToken.cs`
- `src/Adnd.Server/Models/Player.cs`
- `src/Adnd.Server/Models/Game.cs`
- `src/Adnd.Server/Models/GameSession.cs`
- `src/Adnd.Server/Models/Character.cs`
- `src/Adnd.Server/Services/GameAuthorizationService.cs`
- `src/Adnd.Server/Controllers/GamesController.cs`
- `src/Adnd.Server/Services/IGameManagement.cs`
- `src/Adnd.Server/Services/IPlayerManagement.cs`
- `src/Adnd.Server/Data/AppDbContext.cs`
- `src/Adnd.Server/Data/MigrationService.cs`
- `src/Adnd.Server/Data/Migrations/20260609050921_InitialCreate.cs`
- `src/Adnd.Server/Data/Migrations/20260609090245_HighPriorityFeatures.cs`
- `src/Adnd.Server/Data/Migrations/20260609104147_PerformanceIndexes.cs`
- `src/Adnd.Server/Data/Migrations/20260609125059_GameTemplates.cs`
- `src/Adnd.Server/Services/RateLimitMiddleware.cs`
- `src/Adnd.Server/Services/IApiKeyEncryptionService.cs`
- `src/Adnd.Server/Services/ResiliencePolicies.cs`
- `src/Adnd.Server/Services/HealthCheckClasses.cs`
- `src/Adnd.Server/Agent/GameAgent.cs`
- `src/Adnd.Server/Services/AgentBus.cs`
- `src/Adnd.Server/Events/GameEvents.cs`
- `src/Adnd.Server/Handlers/GameEventHandlers.cs`
- `src/Adnd.Server/Handlers/PlotWeaverHandler.cs`
- `src/Adnd.Server/Services/DiceEngine.cs`
- `src/Adnd.Server/Services/GameEngine.cs`
- `src/Adnd.Server/Services/CombatService.cs`
- `src/Adnd.Server/Services/ICombatState.cs`
- `src/Adnd.Server/Services/ICombatQuery.cs`
- `src/Adnd.Server/Services/ICombatActions.cs`
- `src/Adnd.Server/Services/PlotWeaver.cs`
- `src/Adnd.Server/Services/PlotThreadGenerationStrategy.cs`
- `src/Adnd.Server/Services/LLMProvider.cs`
- `src/Adnd.Server/Services/BaseLLMProvider.cs`
- `src/Adnd.Server/Services/ProviderFromPresets.cs`
- `src/Adnd.Server/Services/ILLMProviderFactory.cs`
- `src/Adnd.Server/Services/EmbeddingService.cs`
- `src/Adnd.Server/Services/RAGService.cs`
- `src/Adnd.Server/Services/LLMPresetService.cs`
- `src/Adnd.Server/Services/LLMInteractionLogger.cs`
- `src/Adnd.Server/Services/ICharacterCreation.cs`
- `src/Adnd.Server/Hubs/GameHub.cs`
- `src/Adnd.Server/Hubs/GameHub.Combat.cs`
- `src/Adnd.Server/Hubs/GameHub.ToolCalls.cs`
- `src/Adnd.Server/Program.cs`

### Frontend:
- `src/Adnd.Client/src/api/client.ts`
- `src/Adnd.Client/src/api/auth/authApi.ts`
- `src/Adnd.Client/src/types/auth.types.ts`
- `src/Adnd.Client/src/types/game.types.ts`
- `src/Adnd.Client/src/types/gm.types.ts`
- `src/Adnd.Client/src/types/combat.types.ts`
- `src/Adnd.Client/src/api/hooks/useCharacters.ts`
- `src/Adnd.Client/src/pages/CharacterSheetPage.tsx`
- `src/Adnd.Client/src/pages/CharacterCreateWizard.tsx`
- `src/Adnd.Client/src/api/admin/adminApi.ts`

### Docs:
- `IDEAS.md`
- `AGENTS.md`
- `AGENTS.md` project context

---

## Next Session Resume Instructions

1. Read `.pi/fix-plan.md` for the structured fix plan
2. Read `.pi/audit-tracker.md` for detailed findings per system
3. Start with **Subplan 1: Critical Blockers** — these are blocking
4. Implement fixes one at a time, confirming each before moving to the next
5. Update `.pi/fix-plan.md` as you complete each subplan
6. Ask for clarification on any findings before implementing

## New Issues — Found During Verification

### Game Templates — Completely Broken
| # | Issue | Fix |
|---|-------|-----|
| 1 | Game template system is completely broken (backend + frontend) | Investigate root cause — likely missing DB migration, missing DI registration, or API contract mismatch |

### Game Creation UX Issues
| # | Issue | Fix |
|---|-------|-----|
| 2 | Game takes a lot of time to be created | Profile the CreateGameAsync flow — likely the Agent startup (LLM call, plot thread generation) is blocking the response |
| 3 | Creation dialog doesn't auto-close, allowing multiple clicks creating duplicate games | Add loading/disabled state to the Create button; auto-close dialog on success; add optimistic UI or redirect to the new game |

---

## Key Files to Modify First (Subplan 1)

1. `src/Adnd.Server/Hubs/GameHub.cs` — Fix `_playerConnections` key
2. `src/Adnd.Server/Agent/GameAgent.cs` — Fix DbContext lifetime + recovery
3. `src/Adnd.Server/Services/ResiliencePolicies.cs` — Fix circuit breaker singleton
4. `src/Adnd.Server/Services/DiceEngine.cs` — Fix Random thread safety
5. `src/Adnd.Server/Handlers/PlotWeaverHandler.cs` — Fix per-game counter
6. `src/Adnd.Client/src/pages/CharacterSheetPage.tsx` — Fix skills tab + attributes
