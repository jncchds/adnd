# System Registry and Health Checks Design

**Date:** 2026-06-12
**Status:** Design Approved

## Problem

Three issues identified:

1. **SystemRegistry is unnecessarily a scoped DI service.** It holds immutable built-in system definitions (dnd5e, pf2e, coc7e) and provides a `RegisterSystem()` method for runtime registration. The runtime registration is never used — custom systems are stored per-game in `Game.CustomSystemJson`. The scoped lifetime is wasteful for an immutable lookup table.

2. **`ISystemRegistry` interface is unnecessary.** There's only one implementation. The interface exists but is only consumed by 4 services, 2 of which (GameEngine, AgentBus) actually use it, and 1 (GMToolRegistry) has it as a dead dependency.

3. **`LlmProvidersHealthCheck` can fail the app for unrelated reasons.** It queries all LLMPresets from the DB, creates a provider for each, calls `IsAvailableAsync()`, and returns **unhealthy if ALL fail**. This means:
   - One game with a bad API key can mark the whole app unhealthy
   - A temporarily down provider marks the app unhealthy even though other games work
   - It's a binary health check for something that should be per-game

## Design

### Part 1: SystemRegistry — singleton, immutable

`SystemRegistry` becomes a `sealed` singleton class:

- Private `Dictionary<string, SystemDefinition>` populated in a static constructor with the 3 built-in systems
- `GetBuiltIn(string id)` → returns `SystemDefinition?` from the dictionary
- `ISystemRegistry` interface → **removed entirely**
- `RegisterSystem()` → **removed**
- `CreateDefaultCharacter()` and `ValidateCharacter()` → moved to GameEngine as extension methods that take a `SystemDefinition` as input
- `SystemDefinition` class → stays as-is, just a data model

### Part 2: Custom systems — per-game, not global

`Game.CustomSystemJson` (already exists) is the source of truth for custom systems. Call sites:

```csharp
var system = game.CustomSystemJson != null
    ? SystemDefinition.DeserializeCustom(game.CustomSystemJson)
    : SystemRegistry.GetBuiltIn(game.SystemId);
```

`SystemDefinition` gets a static `DeserializeCustom(string json)` method. If the system ID isn't found in built-ins AND there's no custom system → `null` → caller throws `InvalidOperationException` (same behavior as now).

### Part 3: What gets removed

- `ISystemRegistry` interface — gone
- `RegisterSystem()` method — gone
- `GMToolRegistry`'s `ISystemRegistry` dependency — removed (dead code)

### Part 4: What stays the same

- `ISystemRulesFactory` — unchanged (singleton with built-in rules strategies, separate concern)
- `DnD5eRules`, `PF2eRules`, `CoC7eRules` — unchanged
- `SystemDefinition` data model — unchanged
- Game creation — still defaults to `dnd5e`
- Error handling — same: unknown system → `InvalidOperationException`

### Part 5: DI changes

```diff
- builder.Services.AddScoped<ISystemRegistry, SystemRegistry>();
+ builder.Services.AddSingleton<SystemRegistry>();
```

### Part 6: Call site changes

Files affected:

| File | Change |
|------|--------|
| `SystemRegistry.cs` | Remove interface, make sealed singleton, remove RegisterSystem, add DeserializeCustom |
| `GameEngine.cs` | Inject `SystemRegistry` (singleton), check `CustomSystemJson` first, move validation/default methods here |
| `AgentBus.cs` | Inject `SystemRegistry` (singleton), check `CustomSystemJson` first |
| `GMToolRegistry.cs` | Remove `ISystemRegistry` parameter (dead dependency) |
| `Program.cs` | `AddSingleton<SystemRegistry>()` |

### Part 7: LLM Health Check fix

`LlmProvidersHealthCheck` should not fail the app if individual providers are unavailable. Options:

**Option A: Remove the check entirely.** Provider availability is handled by the resilience policies (Polly retry + circuit breaker) already in place. A health check that fails for transient provider issues is noisy and misleading.

**Option B: Report status without failing.** Return Healthy with a detailed status message listing each preset's status. This is diagnostic-only — never Unhealthy.

**Option C: Only check the active game's preset.** If there are active sessions, only check that game's preset. Otherwise skip.

My recommendation is **Option A** — the resilience policies already handle provider failures gracefully. The health check adds noise without value. If you want diagnostics, log provider status on startup instead.

## Files to change

1. `src/Adnd.Server/Services/SystemRegistry.cs` — simplify to sealed singleton
2. `src/Adnd.Server/Services/GameEngine.cs` — check CustomSystemJson, move validation methods
3. `src/Adnd.Server/Services/AgentBus.cs` — check CustomSystemJson
4. `src/Adnd.Server/Services/GMToolRegistry.cs` — remove dead dependency
5. `src/Adnd.Server/HealthChecks/HealthCheckClasses.cs` — fix LLM health check
6. `src/Adnd.Server/Program.cs` — DI registration change

## Success criteria

- [ ] SystemRegistry is a singleton with no mutable state
- [ ] No `ISystemRegistry` interface — direct class usage
- [ ] Custom systems resolved from `Game.CustomSystemJson` at call site
- [ ] All callers compile and work the same
- [ ] LLM health check doesn't fail the app for individual provider issues
- [ ] No breaking changes to existing behavior
