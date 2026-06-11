# ADnD Deep Audit — Fix Plan

> Audit completed: 2026-06-11
> Total issues found: ~100 across 24 systems
> Priority: 🔴 Critical → ⚠️ High → ⚡ Performance → 📦 Completeness

---

## 🔴 Critical Blockers (Must Fix First)

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

---

## ⚠️ High Priority — Correctness

### Auth & Authorization (3 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | Two `SaveChangesAsync` in Register/Login — refresh token failure leaves user stranded | Wrap in transaction |
| 2 | `LastLoginAt` field exists but never set | Update on login |
| 3 | `CanAdminAsync` doc says "creator OR GM agent" but only checks CreatorId | Fix implementation to match doc |

### SignalR Hub (4 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `OnConnectedAsync` queries Players without GameId filter | Add GameId filter |
| 2 | `CombatDealDamage` duplicate `if` check (copy-paste) | Remove duplicate |
| 3 | `StartCombat` checks `p.Role != PlayerRole.Creator` — wrong check | Check `Game.CreatorId` instead |
| 4 | `AddParticipant` doesn't validate caller is in game | Add authorization check |

### Game Management (4 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `CreateGameAsync` queries Users after SaveChanges — wasteful | Remove redundant query |
| 2 | `DeleteGameAsync` cascades to ALL child tables | Set FKs to NULL instead of cascade |
| 3 | `JoinByCodeAsync` uses `.ToLower()` — prevents index usage | Use case-insensitive collation or store normalized |
| 4 | `PromotePlayerAsync` error lists 3 roles, enum has 4 | Update error message |

### Database/EF Core (3 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `Ignore((EventId)10620)` suppresses sensitive data warning | Use named constant |
| 2 | `PendingModelChangesWarning` ignored — model drift undetected | Remove suppression or log when triggered |
| 3 | `Whispers` has both `Targets` (text) and `TargetPlayerIds` (uuid[]) — duplicate | Remove one, keep one |

### Agent Framework (4 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `GetOrCreate` race condition | Use `GetOrAdd` or lock |
| 2 | `HandleGMCall` tool call loop breaks on confirmation | Fix flow to handle waiting state |
| 3 | `HandleNPCCall`/`HandlePlayerCall` no game-scoped check | Add game scope validation |
| 4 | `GetProvider` caches `DecryptedApiKey` on shared EF entity | Decrypt per-call or cache separately |

### Resilience (5 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `GetCombinedPolicy` wraps circuitBreaker around retry — retry should be outer | Swap order |
| 2 | `ExecuteWithResilienceAsync` catches `Exception` broadly | Only catch expected exceptions |
| 3 | Swallows original exception — no stack trace | Log + rethrow or include in Result |
| 4 | Retry only handles `HttpRequestException` and `TimeoutRejectedException` | Add `TaskCanceledException`, `SocketException` |
| 5 | Circuit breaker per-instance, not per-provider | Add provider-keyed breaker |

### Dice Engine (2 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `ParseFormula` doesn't handle spaces in formula | Allow optional whitespace in regex |
| 2 | `ApplyOptions` applies KeepHighest THEN KeepLowest — incorrect when both set | Validate mutual exclusivity |

### Combat System (3 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `GetCombatWithParticipants` duplicates across multiple services | Extract shared method |
| 2 | `AddCombatEvent` repeated in every service — DRY violation | Extract to shared method |
| 3 | `SANService` stores SAN in `Notes` JSON — not a proper field | Add dedicated SAN fields to Character or CombatParticipant |

### Character System (4 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `CharacterDetail` has both `characterClass` and `class` — duplicate | Remove one |
| 2 | `handleSave` sends double-nested `attributes` | Fix to flat structure |
| 3 | `CharacterCreateWizard` sends `classId` not `class` | Use correct field name |
| 4 | No attribute range validation | Add min/max validation |

### PlotWeaver (3 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `ComputeRelevanceScores` loads ALL threads — no pagination | Add pagination |
| 2 | `GetActiveThreadsAsync` loads ALL threads despite name | Filter by status |
| 3 | Strategies created in constructor — not DI-injectable | Inject via DI |

### LLM Provider System (4 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `IsAvailableAsync()` always returns true — no-op | Implement actual connectivity test |
| 2 | `GetTokenUsage` parses response text as usage JSON — wrong for narrative responses | Parse from proper response field |
| 3 | New `HttpClient` per provider — no connection pooling | Use `IHttpClientFactory` |
| 4 | `CompleteStructuredAsync` throws on deserialization failure | Add retry with slightly different prompt |

### RAG/Embeddings (3 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `FindSimilarPlotThreadsAsync` raw SQL — bypasses EF value converters | Use EF Core with raw SQL or fix mapping |
| 2 | `GeneratePlotContextAsync` loads ALL NPCs — redundant queries | Single query with `.Take()` |
| 3 | `CheckPlotConsistencyAsync` `string.Contains` false positives | Improve pattern matching |

### GM Tool Calls (3 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `ConfirmToolCall` no game ownership check | Add game validation |
| 2 | `DeclinePlayerRoll` parses args as `Dictionary<string, object>` — key mismatch | Use consistent serialization |
| 3 | No tool call expiration | Add TTL to pending calls |

### Message System (2 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `PersistGameEventAsync` uses `Guid.NewGuid()` before save | Let EF generate ID |
| 2 | `EmbedMessageAsync` uses `FindAsync` — table scan | Use `FirstOrDefaultAsync` with predicate |

### LLM Presets (1 issue)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `UpdatePresetAsync` updates `UpdatedAt` even if no fields changed | Only update if changed |

### MediatR Event Bus (3 issues)
| # | Issue | Fix |
|---|-------|-----|
| 1 | `GameLifecycleHandler.Handle(GameCreated)` returns `Task.CompletedTask` — no-op handler | Implement or remove |
| 2 | `PlayerHandler.Handle(PlayerJoined)` logs `notification.PlayerId` as Role — format string bug | Fix format |
| 3 | `PlotWeaverHandler` `_messageCountSinceReview` shared across ALL games | Per-game counter or scoped handler |

---

## ⚡ Performance Fixes

| # | System | Issue | Fix |
|---|--------|-------|-----|
| 1 | Game Management | `GetSessionsAsync` correlated subquery for MessageCount — N+1 | Use group join or computed column |
| 2 | Game Management | `GetPlayersAsync` loads User and Character for every player | Projection to minimal fields |
| 3 | Game Management | `JoinByCodeAsync`/`JoinGameAsync` Include just to check Any() | Use `AnyAsync()` without Include |
| 4 | Game Management | `GenerateInviteCode` full table scan for collision | Datetime concat approach (user-specified) |
| 5 | Agent Framework | Polling every 1s when idle | Replace with MediatR notifications |
| 6 | Agent Framework | Auto-narrate loads ALL active plot threads | Add pagination |
| 7 | Agent Framework | Loads game + LLM preset on every call | Cache game entity |
| 8 | Agent Framework | Creates new scope for every call | Batch calls in single scope |
| 9 | SignalR Hub | `_playerConnections` never cleaned up — memory leak | Periodic cleanup or bounded cache |
| 10 | SignalR Hub | `OnConnectedAsync` loads Players without GameId | Add GameId filter |
| 11 | Combat | `GetCombatWithParticipants` duplicated across services | Extract shared method |
| 12 | PlotWeaver | `ComputeRelevanceScores` saves ALL threads after computing | Only update changed threads |
| 13 | PlotWeaver | `ReviewAndAdaptAsync` passes ALL threads to LLM | Limit to top N by relevance |
| 14 | RAG | `GeneratePlotContextAsync` loads ALL plot threads | Add pagination |
| 15 | RAG | `CheckPlotConsistencyAsync` loads ALL messages | Filter by SessionId directly |
| 16 | LLM Provider | No response caching | Add LRU cache for repeated prompts |
| 17 | LLM Provider | `GetAvailableModelsAsync` no caching | Cache model list per-provider |
| 18 | MediatR | `GameActionHandler` handler each does own `GetGMStatusAsync` + `GetPendingCallsAsync` | Batch queries in a single handler |
| 19 | MediatR | `PublishAsync` fire-and-forget with no timeout | Add cancellation timeout |
| 20 | Character | `CharacterSheetPage` loads full character on mount | Add optimistic UI |
| 21 | Character | `useCharacter` refetches on every update | Cache update |
| 22 | Frontend | `CharacterCreateWizard` loads all templates on every render | Memoize templates |

---

## 📦 Completeness Additions

| # | System | Issue | Fix |
|---|--------|-------|-----|
| 1 | Game Management | No game status transition validation on delete | Add status check |
| 2 | Game Management | No game name uniqueness or validation | Add validation |
| 3 | Game Management | `CloseSessionAsync` doesn't check if already closed | Add check |
| 4 | Game Management | `LeaveGameAsync` doesn't check if user is Creator | Handle creator leaving |
| 5 | Game Management | No hard cap on player count | Add max players |
| 6 | Game Management | `CreateGameAsync` doesn't auto-create Creator player record | Auto-create |
| 7 | Database | No soft delete anywhere | Add `IsDeleted` pattern |
| 8 | Database | `NPCs.PlotThreadId` FK no cascade — orphaned NPCs | Add cascade or check |
| 9 | Database | `SessionNotes.Content` non-nullable text — should allow empty | Make nullable |
| 10 | Agent Framework | No dead letter queue for failed calls | Add DLQ |
| 11 | Agent Framework | `SendCallAsync` doesn't validate required fields | Add validation |
| 12 | Agent Framework | `CompleteCallAsync`/`FailCallAsync` don't check ownership | Add ownership check |
| 13 | Agent Framework | `GetCallTreeAsync` loads ALL children — no limit | Add limit |
| 14 | Resilience | No health check for Redis/DistributedCache | Add check |
| 15 | Resilience | No health check for Hangfire | Add check |
| 16 | Resilience | No health check for pgvector | Add check |
| 17 | Resilience | `LlmProvidersHealthCheck` always returns Healthy | Implement real check |
| 18 | Resilience | No graceful degradation path | Add fallback strategy |
| 19 | Resilience | No max retry delay cap | Cap at 30s |
| 20 | Dice Engine | No dice formula length limit | Add max formula length |
| 21 | Dice Engine | No dice type validation | Validate type > 0 |
| 22 | Dice Engine | `FormatResult` doesn't handle empty finalRolls | Handle edge case |
| 23 | PlotWeaver | No thread deduplication | Check for duplicates |
| 24 | PlotWeaver | No thread merging | Add merge detection |
| 25 | PlotWeaver | No thread archiving — resolved/abandoned never cleaned | Add archival job |
| 26 | PlotWeaver | `GenerateDynamicAsync` doesn't check for existing similar threads | Check embeddings |
| 27 | PlotWeaver | `SpawnMilestonesAsync` doesn't check if milestone already exists | Check existence |
| 28 | PlotWeaver | `DetectOpportunitiesAsync` doesn't check for existing opportunities | Check existence |
| 29 | LLM Provider | No Anthropic provider | Add Claude support |
| 30 | LLM Provider | No streaming support | Add streaming |
| 31 | LLM Provider | No provider health monitoring | Implement real checks |
| 32 | LLM Provider | `GetAvailableModelsAsync` no caching | Add cache |
| 33 | RAG | No embedding cache | Add LRU cache |
| 34 | RAG | No embedding dimension validation | Add dimension check |
| 35 | GM Tool Calls | No tool call history | Add history endpoint |
| 36 | GM Tool Calls | No tool call validation | Validate against schema |
| 37 | Message System | No message archiving | Add retention policy |
| 38 | Message System | No message export | Add export endpoint |
| 39 | LLM Presets | No preset validation on creation | Validate provider/model |
| 40 | Game Templates | No template sharing between users | Add IsPublic + sharing |
| 41 | Session Notes | No note search | Add search |
| 42 | Dice Statistics | No historical trend analysis | Add trend endpoint |
| 43 | Prompt Templates | No template variable validation | Add {variable} validation |
| 44 | Prompt Templates | No template testing UI | Add test endpoint |
| 45 | NPC Management | No NPC stat block validation | Add validation |
| 46 | NPC Management | No NPC import/export | Add import/export |
| 47 | System Registry | No system version compatibility check | Add version check |
| 48 | Character | No character deletion | Add delete endpoint |
| 49 | Character | No character export/import | Add export/import |
| 50 | Character | No spell slot management UI | Wire up spell slots |
| 51 | Character | No equipment management | Wire up inventory |
| 52 | Character | No condition duration countdown | Add countdown UI |
| 53 | Frontend | No error boundary components | Add error boundaries |
| 54 | Frontend | No loading skeletons | Add skeletons |
| 55 | MediatR | No event versioning | Add version field |
| 56 | MediatR | No ordering guarantee | Add sequence numbers |
| 57 | MediatR | No event deduplication | Add idempotency key |
| 58 | MediatR | No dead letter for failed handlers | Add DLQ |
| 59 | MediatR | `WhisperSent` handler is no-op — doesn't persist | Add persistence |
| 60 | MediatR | `SessionCreated`/`SessionClosed` handlers are no-ops | Add side effects |
| 61 | MediatR | `PlayerLeft` handler doesn't update player status | Add status update |

---

## Subplans (Ordered by Dependency)

### Subplan 1: Critical Blockers
- Fix SignalR `_playerConnections` key mismatch
- Fix Agent Framework DbContext lifetime
- Fix Agent Framework recovery (StartAsync)
- Fix Resilience circuit breaker singleton
- Fix Dice Engine thread safety
- Fix MediatR handler per-game counter
- Fix Frontend skills tab + attribute loading

### Subplan 2: Core Correctness
- Auth transaction wrapping + LastLoginAt + CanAdminAsync
- SignalR connection validation + combat fixes
- Game Management: delete cascade → NULL, invite code rework, leave as player
- DB warning suppression fix + whispers one-to-one
- Agent Framework race condition + game-scoped checks
- Resilience policy ordering + exception handling
- Dice formula parsing + combat state service dedup
- Character system type fixes + create wizard fix

### Subplan 3: Performance
- Game Management N+1 queries + invite code datetime concat
- Agent Framework polling → MediatR + caching
- SignalR connection cleanup
- Combat shared methods
- PlotWeaver pagination + relevance score optimization
- RAG query optimization
- LLM Provider HttpClient factory + model caching
- MediatR query batching
- Frontend memoization + optimistic UI

### Subplan 4: Completeness
- Game Management: status validation, name validation, close check, creator leaving, player cap
- Soft-delete pattern across all entities
- Agent Framework: DLQ, validation, ownership, children limit
- Resilience: health checks, degradation, max delay cap
- Dice Engine: formula limits, type validation, format edge case
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

## Implementation Notes

- **Subplan 1** is blocking — nothing works correctly until these are fixed
- **Subplan 2** fixes the bulk of correctness issues
- **Subplan 3** optimizes what Subplans 1+2 changed
- **Subplan 4** adds missing features (no breaking changes)
- **Subplan 5** is final cleanup

Total estimated subplans: **5**
Total estimated issues to fix: **~100**
