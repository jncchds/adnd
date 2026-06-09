# Refactoring Summary: Factory, FactoryMethod & Strategy Patterns

## Overview

This refactoring applies **Strategy**, **Factory**, and **FactoryMethod** patterns to eliminate
hardcoded switch/if-else chains and extract complex controller logic into testable services.

---

## 1. Strategy Pattern: `ISystemRules` + `SystemRulesFactory`

**File:** `Services/ISystemRules.cs` (490 lines)

### Problem
System-specific rules (proficiency bonus, critical hits, HP calculation, XP progression) were
scattered across `GameEngine.cs`, `CombatService` partials, and `SystemRegistry.cs`.

### Solution
- **`ISystemRules`** interface — 14 methods covering all system-specific calculations
- **`DnD5eRules`**, **`PF2eRules`**, **`CoC7eRules`** — concrete strategies
- **`ISystemRulesFactory`** / **`SystemRulesFactory`** — registry-based lookup

### Impact
```
Before: Switch/if-else chains in GameEngine, CombatService, SystemRegistry
After:  Single factory call → _rulesFactory.GetRules(systemId).GetProficiencyBonus(level)
```

### Key Methods
| Method | Purpose |
|--------|---------|
| `IsCriticalHit()` | System-specific critical hit detection |
| `GetProficiencyBonus()` | D&D (2→6), PF2e (level/2+1), CoC (0) |
| `GetAttributeModifier()` | D&D ((val-10)/2), CoC ((val-50)/10) |
| `GetXPForLevel()` | Different XP curves per system |
| `GetInitialHP()` | System-specific HP at level 1 |
| `ProcessRestAsync()` | D&D (1d6/level short), CoC (1d4 SAN long) |
| `PerformSANCheckAsync()` | CoC-specific d100 under SAN checks |

---

## 2. Strategy Pattern: `ICharacterCreationStrategy` + `CharacterCreationFactory`

**File:** `Services/ICharacterCreation.cs` (256 lines)

### Problem
Character creation logic was embedded in `GameEngine.CreateCharacterAsync()` with hardcoded
system-specific defaults and validation.

### Solution
- **`ICharacterCreationStrategy`** interface — `CreateDefaultTemplate()`, `Validate()`, `InitializeAsync()`
- **`DnD5eCharacterCreation`**, **`PF2eCharacterCreation`**, **`CoC7eCharacterCreation`** — concrete strategies
- **`ICharacterCreationFactory`** / **`CharacterCreationFactory`** — factory pattern

### Benefits
- Each system's character template is self-contained
- Validation is system-specific and easily testable
- New systems added by registering a strategy (no controller changes)

---

## 3. Strategy Pattern: `IGameStartService` + `INarrativeGenerationStrategy`

**File:** `Services/IGameStart.cs` (310 lines)

### Problem
`AdminController.StartGame()` was 133 lines of monolithic logic:
1. Validate → 2. Update status → 3. Generate plot threads → 4. Generate narrative → 5. Create session → 6. Broadcast → 7. Publish event

### Solution
- **`INarrativeGenerationStrategy`** — strategy for opening narrative
  - `DefaultNarrativeGenerator` — uses LLM provider
  - `TemplateNarrativeGenerator` — fallback templates per system
- **`INarrativeGenerationFactory`** — selects strategy based on provider type
- **`IGameStartService`** / **`GameStartService`** — orchestrates the 6 steps

### Controller Before/After
```csharp
// Before (133 lines inline)
[HttpPost("games/{gameId}/start")]
public async Task<IActionResult> StartGame(Guid gameId) { ... }

// After (10 lines)
[HttpPost("games/{gameId}/start")]
public async Task<IActionResult> StartGame(Guid gameId)
{
    var userId = _userIdProvider.GetCurrentUserId();
    var result = await _gameStartService.StartGameAsync(gameId, userId);
    if (!result.Success) return /* error handling */;
    return Ok(new { result.GameId, result.Status, result.StartedAt, result.PlotThreadsGenerated });
}
```

---

## 4. Factory Pattern: `IGameJoinStrategy` + `GameJoinFactory`

**File:** `Services/IGameJoin.cs` (172 lines)

### Problem
`GamesController` had two join paths (`JoinGame` and `JoinByCode`) with nearly identical logic
but different validation rules.

### Solution
- **`IGameJoinStrategy`** interface — `Validate()`, `ProcessJoinAsync()`
- **`InviteCodeJoinStrategy`** — validates game is active
- **`DirectJoinStrategy`** — simpler validation
- **`IGameJoinFactory`** / **`GameJoinFactory`** — selects strategy by join type

### Benefits
- Each join method is independently testable
- New join methods (e.g., admin invite, Discord bot) added by registering a strategy
- Validates before committing to DB

---

## 5. File Structure Changes

### New Files (1,228 lines)
| File | Lines | Pattern |
|------|-------|---------|
| `ISystemRules.cs` | 490 | Strategy + Factory |
| `ICharacterCreation.cs` | 256 | Strategy + Factory |
| `IGameStart.cs` | 310 | Strategy + Service |
| `IGameJoin.cs` | 172 | Strategy + Factory |

### Modified Files
| File | Before | After | Change |
|------|--------|-------|--------|
| `AdminController.cs` | 68 | 70 | Added `_gameStartService` field + parameter |
| `Games.cs` | 153 | 50 | **Replaced 133-line StartGame with 10-line delegate** |
| `GameEngine.cs` | 285 | 361 | Added `ISystemRulesFactory` dependency |
| `SystemRegistry.cs` | 228 | 304 | Added `ISystemRulesFactory` dependency |
| `Program.cs` | 216 | 233 | Added 4 new DI registrations |

### Net Impact (Part 1)
- **AdminController.StartGame**: 133 lines → 10 lines (92% reduction)
- **New abstractions**: 4 interfaces + 9 concrete classes + 2 factories
- **Build**: ✅ Passes (3 pre-existing warnings, 0 new warnings)

### Net Impact (Part 2 - CombatService)
- **Deleted**: 20 partial files (Combat.cs, Actions.cs, HP.cs, Conditions.cs, Spells.cs, etc.)
- **Added**: 12 domain services + 1 facade + 1 action factory
- **SystemSpecific.cs**: Switch/if-else → `ISystemRulesFactory` (Strategy pattern)
- **Progression.cs**: Hardcoded system logic → `ISystemRules` delegation
- **Build**: ✅ Passes (3 pre-existing warnings, 0 new warnings)

### Net Impact (Part 3 - GamesController)
- **GamesController**: 531 lines → 280 lines (47% reduction)
- **Added**: 3 domain services (Game, Session, Player management)
- **Build**: ✅ Passes (3 pre-existing warnings, 0 new warnings)

### Total New Files Created
| File | Lines | Pattern |
|------|-------|---------|
| `ISystemRules.cs` | 490 | Strategy + Factory |
| `ICharacterCreation.cs` | 256 | Strategy + Factory |
| `IGameStart.cs` | 310 | Strategy + Service |
| `IGameJoin.cs` | 172 | Strategy + Factory |
| `ICombatActions.cs` | 400 | Factory |
| `ICombatState.cs` | 490 | Domain Service |
| `ICombatDomain.cs` | 499 | Domain Service × 4 |
| `ICombatData.cs` | 1,062 | Domain Service × 4 |
| `ICombatQuery.cs` | 293 | Domain Service |
| `IGameManagement.cs` | 160 | Domain Service |
| `IPlayerManagement.cs` | 240 | Domain Service |
| **Total** | **4,372** | |

### Deleted Files (Combat partials)
20 files: Combat.cs, Actions.cs, HP.cs, Conditions.cs, DeathSaves.cs, Spells.cs, Rest.cs,
Inventory.cs, Grid.cs, SAN.cs, Progression.cs, SystemSpecific.cs, Queries.cs, Helpers.cs,
Helpers2.cs, Initiative.cs, Turns.cs, Participants.cs, AICombat.cs

---

## 6. CombatService Refactoring (Round 2)

### Problem
`CombatService` had **20 partial files** totaling **2,374 lines** with duplicated helper methods
(`GetCombatWithParticipants`, `AddCombatEvent`, `GetNotes`, etc.) across every file.

### Solution
Extracted into **12 focused domain services** + **1 facade**:

| Service | Responsibility | Lines |
|---------|---------------|-------|
| `ICombatLifecycleService` | Start/End/Pause/Resume | ~70 |
| `ICombatParticipantService` | Add/Remove participants | ~70 |
| `ICombatInitiativeService` | Roll/reorder initiative | ~100 |
| `ICombatTurnService` | Advance/retreat turns | ~130 |
| `ICombatStateService` | HP, conditions, death saves, rest | ~490 |
| `ICombatSpellService` | Cast spell/AoE | ~250 |
| `ICombatInventoryService` | Add/remove/equip items | ~130 |
| `ICombatProgressionService` | XP, leveling | ~130 |
| `ICombatGridService` | Grid positioning | ~130 |
| `ICombatAIService` | AI suggestions, auto-resolve | ~350 |
| `ICombatQueryService` | Get combat, log, participants | ~140 |
| `ISANService` | SAN loss/recovery/check | ~140 |
| `ICombatActionFactory` | Attack/Save/Spell actions | ~400 |
| **`CombatService` (facade)** | Delegates to all above | ~380 |

**Deleted**: 20 partial files (Combat.cs, Actions.cs, HP.cs, Conditions.cs, etc.)

### Key Wins
- **`ApplySystemSpecificEffectsAsync`**: Switch/if-else → `_rulesFactory.GetRules(systemId)` (Strategy)
- **`LevelUpAsync`**: Hardcoded `if (systemId == "dnd5e")` → `_rulesFactory.GetRules(systemId).GetInitialHP()`
- **`CalculateXPForCombatAsync`**: Uses `rules.GetCombatXPReward()` instead of flat 100 XP
- All helpers consolidated into domain services (no more duplication across 20 files)

---

## 7. GamesController Split (Round 2)

### Problem
`GamesController` was **531 lines** handling game CRUD, sessions, and players all in one class.

### Solution
Split into **3 domain services**:

| Service | Responsibility |
|---------|---------------|
| `IGameManagementService` | GetGames, GetGame, CreateGame, DeleteGame, GenerateInvite, UpdateLanguage |
| `ISessionManagementService` | GetSessions, CreateSession, CloseSession |
| `IPlayerManagementService` | GetPlayers, PromotePlayer, JoinGame, LeaveGame, JoinByCode |

### Controller Before/After
```csharp
// Before (531 lines, all in one class)
public class GamesController : ControllerBase { ... }

// After (280 lines, delegates to services)
public class GamesController : ControllerBase
{
    private readonly IGameManagementService _gameService;
    private readonly ISessionManagementService _sessionService;
    private readonly IPlayerManagementService _playerService;
    // ... each endpoint is 5-15 lines
}
```

### Net Impact
- **GamesController**: 531 lines → 280 lines (47% reduction)
- **Each service**: Independently testable, focused on one domain
- **JoinByCode**: Extracted validation logic into `IGameJoinFactory` (from Part 1)

---

## 8. Remaining Opportunities

### Medium Priority
1. **`GameHub`** (19 partial files, 2,838 lines) — Already split by concern; good as-is
2. **`PlotWeaver.cs`** — Extract thread generation strategies
3. **`RAGService.cs`** — Extract embedding strategies
4. **`GMToolRegistry.cs`** (737 lines) — Extract tool registration strategies

### Done ✅
- **`LLMProvider.cs`** (1,143 lines → 4 files + slimmed core):
  - `LLMProvider.cs` → 300 lines (interfaces + `BaseLLMProvider` + registry)
  - `OllamaLLMProvider.cs` → 170 lines
  - `LmStudioLLMProvider.cs` → 180 lines
  - `OpenAILLMProvider.cs` → 180 lines
  - `GoogleAIStudioLLMProvider.cs` → 200 lines
  - `ProviderFromPresets.cs` → 431 lines (already separate)

- **`PlotWeaver.cs`** (899 lines → 5 files + slimmed core):
  - `IPlotWeaver.cs` → 80 lines (interface only)
  - `PlotWeaver.cs` → 280 lines (orchestrator)
  - `PlotThreadGenerationStrategy.cs` → 200 lines (initial + dynamic generation)
  - `PlotThreadAdaptation.cs` → 160 lines (review and adapt)
  - `PlotMilestoneSpawning.cs` → 120 lines (milestone spawning)
  - `PlotOpportunityDetection.cs` → 120 lines (opportunity detection)
  - `PlotSharedTypes.cs` → 30 lines (StoryOpportunity, OpportunityType)
  - DTOs moved to their respective strategy files

---

## 7. How to Add a New RPG System

```csharp
// 1. Create rules strategy
public class MySystemRules : ISystemRules
{
    public string SystemId => "mysystem";
    public int GetProficiencyBonus(int level) => /* custom logic */;
    // ... implement all 14 methods
}

// 2. Create character creation strategy
public class MySystemCharacterCreation : ICharacterCreationStrategy
{
    public string SystemId => "mysystem";
    public JsonDocument CreateDefaultTemplate() => /* custom template */;
    // ... implement all methods
}

// 3. Register in factories (via admin API or config)
services.AddScoped<ISystemRulesFactory, SystemRulesFactory>();
services.AddScoped<ICharacterCreationFactory, CharacterCreationFactory>();
```
