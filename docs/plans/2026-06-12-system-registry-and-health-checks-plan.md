# Implementation Plan: System Registry and Health Checks

**Design:** `docs/plans/2026-06-12-system-registry-and-health-checks-design.md`
**Status:** ✅ Completed

## Task List

### 1. Simplify SystemRegistry to sealed singleton

**File:** `src/Adnd.Server/Services/SystemRegistry.cs`

- Remove `ISystemRegistry` interface entirely
- Make `SystemRegistry` a `sealed` class (no interface)
- Keep static constructor that populates the 3 built-in systems
- Keep `GetBuiltIn(string id)` method (renamed from `GetSystem`)
- Remove `RegisterSystem()` method
- Remove `CreateDefaultCharacter()` — move to GameEngine
- Remove `ValidateCharacter()` — move to GameEngine
- Add static `SystemDefinition DeserializeCustom(string json)` method
- Keep `SystemDefinition` class as-is
- Remove `ILogger<SystemRegistry>` dependency (no longer needed)
- Remove `ISystemRulesFactory` dependency (never used — check if it's actually used)

### 2. Update GameEngine

**File:** `src/Adnd.Server/Services/GameEngine.cs`

- Change `ISystemRegistry systemRegistry` parameter → `SystemRegistry systemRegistry`
- Add helper method `GetSystemDefinition(string systemId, Game? game = null)` that:
  - If `game?.CustomSystemJson` is not null → `SystemDefinition.DeserializeCustom(game.CustomSystemJson)`
  - Else → `systemRegistry.GetBuiltIn(systemId)`
  - Returns `null` if neither found (caller throws)
- Move `CreateDefaultCharacter` logic from `SystemRegistry` into GameEngine as a method that takes `SystemDefinition`
- Move `ValidateCharacter` logic from `SystemRegistry` into GameEngine as a method that takes `SystemDefinition`
- Update `CreateDefaultCharacterAsync` to use `GetSystemDefinition` + new helper
- Update `ValidateCharacterAsync` to use `GetSystemDefinition` + new helper
- Update `GetDefaultCharacterTemplateAsync` to use `GetSystemDefinition`

### 3. Update AgentBus

**File:** `src/Adnd.Server/Services/AgentBus.cs`

- Change `ISystemRegistry systemRegistry` parameter → `SystemRegistry systemRegistry`
- Update `DispatchCall` to use `GetSystemDefinition` pattern (check CustomSystemJson first)
- The `SystemDispatchOptions` already has `SystemId` — look up using the same pattern

### 4. Remove dead dependency from GMToolRegistry

**File:** `src/Adnd.Server/Services/GMToolRegistry.cs`

- Remove `ISystemRegistry` constructor parameter
- Remove `_systemRegistry` field (it's injected but never used)

### 5. Fix LLM Health Check

**File:** `src/Adnd.Server/HealthChecks/HealthCheckClasses.cs`

- Change `LlmProvidersHealthCheck` to **report-only mode**:
  - Still checks all presets and logs status
  - Always returns `Healthy` (never `Unhealthy`)
  - Returns detailed status message listing each preset's health
- Alternatively: remove the check entirely and log provider status on startup

### 6. Update Program.cs DI registration

**File:** `src/Adnd.Server/Program.cs`

- Change `builder.Services.AddScoped<ISystemRegistry, SystemRegistry>()` → `builder.Services.AddSingleton<SystemRegistry>()`

## Verification

After all changes:
1. `dotnet build` — no compile errors
2. `dotnet build` — no warnings about unused dependencies
3. Verify `SystemRegistry` is only injected as `SystemRegistry`, not `ISystemRegistry`
4. Verify all callers use the new lookup pattern

## Order of Operations

1. SystemRegistry.cs (foundation — define the new shape)
2. GameEngine.cs (main consumer)
3. AgentBus.cs (secondary consumer)
4. GMToolRegistry.cs (dead dependency removal)
5. HealthCheckClasses.cs (health check fix)
6. Program.cs (DI registration)
