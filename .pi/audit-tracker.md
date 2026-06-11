# ADnD Deep Audit Tracker

> Started: 2026-06-11
> Scope: C (Deep Audit) — Correctness, Performance, Completeness
> User feedback: "Security is fine for now. Corrections must be corrected, Performance is fine, HasGmRoleAsync needs to be investigated separately."

---

## Audit Progress

| # | System | Status | Issues | Blockers |
|---|--------|--------|--------|----------|
| 1 | Auth & Authorization | ✅ Done | 14 | 0 |
| 2 | SignalR Hub | ✅ Done | 13 | 1 |
| 3 | Game Management | ✅ Done | 16 | 0 |
| 4 | Database/EF Core | ✅ Done | 18 | 2 |
| 5 | Security Layer | ✅ Done | 16 | 1 |
| 6 | Resilience (Polly) | ✅ Done | 14 | 1 |
| 7 | Agent Framework | ✅ Done | 17 | 2 |
| 8 | MediatR Event Bus | ⏳ Pending | — | — |
| 9 | Dice Engine | ⏳ Pending | — | — |
| 10 | Combat System | ⏳ Pending | — | — |
| 11 | Character System | ⏳ Pending | — | — |
| 12 | PlotWeaver | ⏳ Pending | — | — |
| 13 | LLM Provider System | ⏳ Pending | — | — |
| 14 | RAG/Embeddings | ⏳ Pending | — | — |
| 15 | GM Tool Calls | ⏳ Pending | — | — |
| 16 | Message System | ⏳ Pending | — | — |
| 17 | LLM Presets Mgmt | ⏳ Pending | — | — |
| 18 | Game Templates | ⏳ Pending | — | — |
| 19 | Session Notes | ⏳ Pending | — | — |
| 20 | Dice Statistics | ⏳ Pending | — | — |
| 21 | Prompt Templates | ⏳ Pending | — | — |
| 22 | NPC Management | ⏳ Pending | — | — |
| 23 | System Registry | ⏳ Pending | — | — |
| 24 | Frontend App | ⏳ Pending | — | — |

---

## System 1: Auth & Authorization

### Issues
#### Correctness
1. Two `SaveChangesAsync` in Register/Login — refresh token insert failure leaves user stranded
2. `LastLoginAt` field exists but never set
3. `CanAdminAsync` doc says "creator OR GM agent" but only checks CreatorId

#### Performance
10. No index on `RefreshToken.UserId`

#### Completeness
11. No forgot password flow
12. No user deactivation/deletion
13. `HasGmRoleAsync` grants GM to all active Players — [SEPARATE INVESTIGATION]
14. Client-side token refresh race condition

---

## System 2: SignalR Hub

### Issues
#### Correctness
1. `OnConnectedAsync` queries Players without GameId filter
2. **KEY MISMATCH: `_playerConnections` keys use user ID in OnConnectedAsync but player ID in OnDisconnectedAsync/SendHeartbeat** — disconnect detection broken
3. Same key mismatch in SendHeartbeat
4. `CombatDealDamage` has duplicate `if` check (copy-paste)
5. `PublishAsync` fire-and-forget with no lifecycle management
6. `GetConnectionIdForPlayer` called in DeclinePlayerRoll — not standard Hub method
7. `StartCombat` checks `p.Role != PlayerRole.Creator` — Creator is Game.CreatorId, not Player role
8. `AddParticipant` doesn't validate caller is in game

#### Performance
10. `_playerConnections` is static ConcurrentDictionary — never cleaned up (memory leak)
11. `OnConnectedAsync` loads Players without GameId

#### Completeness
12. No group cleanup on disconnect (multi-game players leave stale groups)
13. `CombatDeathSave` returns custom DTO instead of CombatLogResponse

### Blocker
- **Issue #2**: Key mismatch — disconnect detection fundamentally broken

---

## System 3: Game Management

### Issues
#### Correctness
1. `CreateGameAsync` queries Users after SaveChanges — wasteful
2. `DeleteGameAsync` cascades to ALL child tables
3. `JoinByCodeAsync` uses `.ToLower()` — prevents index usage
4. `PromotePlayerAsync` error lists 3 roles, enum has 4
5. `GetGamesAsync` projection loads navigation properties
6. `GetGameAsync` loads ALL players for access check

#### Performance
7. `GetSessionsAsync` correlated subquery for MessageCount — N+1
8. `GetPlayersAsync` loads User and Character for every player
9. `JoinByCodeAsync`/`JoinGameAsync` use Include just to check Any()
10. `GenerateInviteCode` full table scan for collision check

#### Completeness
11. No game status transition validation on delete
12. No game name uniqueness or validation
13. `CloseSessionAsync` doesn't check if already closed
14. `LeaveGameAsync` doesn't check if user is Creator
15. No hard cap on player count
16. `CreateGameAsync` doesn't auto-create Creator player record

### User Fixes
- DeleteGameAsync: Must NOT cascade delete LLM interactions. Set GameId to NULL.
- InviteCode: Rework to datetime concat + random.
- Two games can have same title: Fix.
- LeaveGame: Should leave as player, not creator.

---

## System 4: Database/EF Core

### Issues
#### Correctness
1. `DeleteGameAsync` cascades to ALL child tables (AgentCalls, GMToolCalls, Messages, etc.)
2. `Ignore((EventId)10620)` suppresses sensitive data warning — use named constant
3. `PendingModelChangesWarning` ignored — model drift not detected
4. `Whispers` has both `Targets` (text) and `TargetPlayerIds` (uuid[]) — duplicate
5. `WhisperFromId`/`WhisperToId` on Messages — ambiguous FK directions
6. `GMToolCalls.ParentToolCallId` FK has no onDelete specified
7. `Characters.PlayerId` unique constraint — orphaned if player leaves/rejoins

#### Performance
8. `UseNpgsql` with baked-in `MigrationsAssembly` — assembly name change breaks migrations
9. `ConfigureWarnings` ignores `PendingModelChangesWarning`
10. `Whispers.TargetPlayerIds` is `uuid[]` but `Targets` is also text — redundant
11. No query compilation for hot paths

#### Completeness
12. No soft delete anywhere
13. No migration rollback strategy
14. `LLMInteractionLogs.OriginGameId` nullable but no index
15. `NPCs.PlotThreadId` FK has no cascade — orphaned NPCs
16. `CombatParticipants.Notes` is `JsonElement` but no value converter
17. No `CreatedAt` index on Games table
18. `SessionNotes.Content` non-nullable `text` — should allow empty

### User Fixes
- Soft-delete: Replace hard deletes with `IsDeleted` flag
- Whispers: Make one-to-one, remove duplicate columns
- JsonElement: Questioning necessity — user to decide
- Blockers: Fix cascade delete issues, warning suppression

---

## System 5: Security Layer

### Issues
#### Correctness
1. `ApiKeyEncryptionService` throws if `Encryption:MasterKey` missing
2. `RateLimitMiddleware` uses `path.Contains("/api/auth/")` — substring match
3. `RateLimitMiddleware` uses `path.Contains("/api/llm-presets")` — same issue
4. Custom rate limiting middleware instead of ASP.NET Core built-in
5. `CleanupExpired` runs every 5 min while window is 1 min
6. `GetClientIp` checks `X-Real-IP` after `X-Forwarded-For` — fragile

#### Performance
7. `lock` on every request — serializes all requests
8. New `List<DateTime>` for every new IP
9. `CleanupExpired` iterates ALL IPs every 5 min — O(n)
10. `List.RemoveAll` is O(n) per IP per request

#### Completeness
11. No rate limiting on SignalR hub
12. No rate limiting on `/health` endpoints
13. No IPv6 handling edge cases
14. No rate limiting on file uploads
15. Middleware runs before auth — can't do per-user limits
16. No audit logging for rate-limited requests

### User Fixes
- Issue #1: Fine to crash with error
- Issues #2-17: Rate limiter is broken overall — will work on separately

---

## System 6: Resilience (Polly retry, circuit breaker, health checks)

### Issues
#### Correctness
1. Circuit breaker is stateless — each call creates new policy, state lost
2. `GetCombinedPolicy` wraps circuitBreaker around retry — retry should be outer
3. `ExecuteWithResilienceAsync` catches `Exception` broadly — swallows cancellation/OOM
4. Swallows original exception — no stack trace
5. Retry only handles `HttpRequestException` and `TimeoutRejectedException`
6. Circuit breaker is per-instance, not per-provider

#### Performance
7. `Math.Pow` on every retry — should precompute
8. `GetCombinedPolicy` creates new policies on every call — should cache

#### Completeness
9. No health check for Redis/DistributedCache
10. No health check for Hangfire
11. No health check for pgvector
12. `LlmProvidersHealthCheck` always returns Healthy — no-op
13. No graceful degradation path
14. No max retry delay cap

### Blocker
- **Issue #1**: Circuit breaker state lost on every call — effectively disabled

### User Fixes
- All correctness, performance, completeness issues must be fixed

---

## System 7: Agent Framework (GameAgent, AgentBus, Hangfire)

### Issues
#### Correctness
1. `ProcessLoopAsync` holds ONE `AppDbContext` for entire lifetime — will throw/stale data
2. `GetOrCreate` race condition — two concurrent calls can both create agents
3. `StartAllActiveGamesAsync` calls `GetOrCreate` but never calls `StartAsync` — loop never starts
4. `HandleGMCall` tool call loop breaks on confirmation — LLM blocked, JSON returned
5. `HandleNPCCall` uses `FindAsync` without game-scoped check
6. `HandlePlayerCall` uses `FindAsync` without game-scoped check
7. `HandleCreateCharacter` doesn't validate against system rules
8. `GetProvider` caches `DecryptedApiKey` on shared EF entity

#### Performance
9. Polling every 1s when idle — user wants to know if MediatR can replace polling
10. Auto-narrate loads ALL active plot threads — no pagination
11. Loads game + LLM preset on every call — should cache
12. Creates new scope for every call — wasteful
13. `BroadcastNarrationAsync` generates embedding in same context — should be async

#### Completeness
14. No dead letter queue for failed calls
15. `SendCallAsync` doesn't validate required fields
16. `CompleteCallAsync`/`FailCallAsync` don't check ownership
17. `GetCallTreeAsync` loads ALL children — no limit

### Blockers
- **Issue #1**: Single DbContext for entire loop — memory leak and stale data
- **Issue #3**: Recovered agents never start processing loop

### User Fixes
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

---

## User-Specified Fixes (Summary)

### System 2: SignalR Hub
- Fix `_playerConnections` key mismatch (user ID vs player ID)
- Fix memory leak (never cleaned up)
- Fix all correctness, performance, completeness issues

### System 3: Game Management
- DeleteGameAsync: Must NOT cascade delete LLM interactions. Set GameId to NULL.
- InviteCode: Rework to datetime concat + random
- Two games can have same title: Fix
- LeaveGame: Should leave as player, not creator

### System 4: Database/EF Core
- Soft-delete: Replace hard deletes with `IsDeleted` flag
- Whispers: Make one-to-one, remove duplicate columns
- JsonElement: Questioning necessity
- Blockers: Fix cascade delete issues, warning suppression

### System 5: Security Layer
- Rate limiter is broken overall — will work on separately

### System 6: Resilience
- All correctness, performance, completeness issues must be fixed

### System 7: Agent Framework
- All issues listed above must be fixed per user request

### General
- User wants: correctness + performance + completeness fixes only
- Security findings logged but not to be fixed per user request
- HasGmRoleAsync: separate investigation
- Total systems: 24 planned, may extend during audit
