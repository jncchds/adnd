# UI Refactor Design — Split Huge Files Into Smaller Ones

**Date:** 2026-06-11
**Goals:** Readability (find code faster) + Maintainability (testable, reason-about-able components)
**Status:** ✅ Completed

## Current State

18,317 lines across 34 files. Top 8 files = 67% of codebase:

| File | Lines | Problem |
|------|------:|---------|
| `GameRoomPage.tsx` | 2,206 | Message styles config (100+ lines), markdown utils, main component, chat panel, message bubble, settings tab — 6 concerns |
| `client.ts` | 1,641 | APIClient class + ALL TypeScript types mashed together (75% types) |
| `GameStatePage.tsx` | 1,357 | 7 sub-tabs, 3 helper components, helper functions, types — all in one file |
| `CombatTab.tsx` | 1,523 | 5 sub-components + main component + types |
| `gameHooks.ts` | 921 | 17+ hooks for completely unrelated APIs |
| `hubHook.ts` | 703 | 20+ event interfaces + hub connection + 1 hook |
| `CharacterCreateWizard.tsx` | 1,021 | All wizard steps in one file |
| `CharacterSheetPage.tsx` | 905 | Stats, spells, equipment, conditions all together |

## Design

### 1. Type Layer (`types/`)

Split `types/index.ts` + `client.ts` types into domain-specific files:

```
src/types/
├── index.ts              # Barrel re-export (public API)
├── auth.types.ts         # AuthUser, AuthResponse, RefreshToken
├── game.types.ts         # Game, GameSession, Player, GameStatus, PlayerRole
├── combat.types.ts       # Combat, Initiative, Condition, SpellCast, etc.
├── plot.types.ts         # PlotThread, Milestone, Opportunity, PlotReview
├── llm.types.ts          # LLMPreset, LLMInteractionLog, ProviderModels
├── agent.types.ts        # AgentCall, AgentType, AgentAction
├── message.types.ts      # UnifiedMessage, MessageType, WhisperType
├── gm.types.ts           # GMTool, ToolCall, DiceHistory, SpellManagement
└── template.types.ts     # GameTemplate, Create/UpdateGameTemplateRequest
```

Each file: 20-80 lines. No duplication between `types/index.ts` and `client.ts`.

### 2. API Queries Layer (`api/queries/`)

Extract query functions from `client.ts` into domain-specific files:

```
src/api/
├── client.ts               # APIClient class only (~300 lines)
├── interceptors.ts         # Auth header injection, error handling
├── queries/
│   ├── authQueries.ts      # register, login, refresh, logout
│   ├── gameQueries.ts      # games CRUD, sessions, players
│   ├── combatQueries.ts    # combat, initiative, attacks
│   ├── plotQueries.ts      # plot threads, milestones, opportunities
│   ├── llmQueries.ts       # LLM presets, usage, interactions
│   ├── agentQueries.ts     # agent calls, pending calls
│   ├── gmQueries.ts        # GM tools, dice history, spells
│   └── templateQueries.ts  # game templates
└── hooks/
    ├── useAuth.ts          # auth context + hooks
    ├── useGame.ts          # useGame, useGames, useSessions, usePlayers
    ├── useCombat.ts        # useCombats, useCombat
    ├── usePlot.ts          # usePlotWeaver, usePlotThreads
    ├── useLLM.ts           # useLLMPresets, useProviderModels, useLLMUsage
    ├── useAgent.ts         # usePendingCalls
    ├── useGM.ts            # useGMTools, useSpells
    ├── useMessages.ts      # useMessagesPaginated, useMessagesInfiniteScroll, useMessageSearch
    ├── useDice.ts          # useDiceHistory, useDiceStats
    ├── useTemplates.ts     # useGameTemplates
    ├── useEntity.ts        # Generic entity CRUD
    ├── useHub.ts           # SignalR hub connection + useGameHub
    └── useGameState.ts     # useGameGameState
```

Each hook file: 40-120 lines. Grouped by domain, not by "when I wrote it."

### 3. Components Layer (`components/<domain>/`)

Extract sub-components from giant pages:

```
src/components/
├── layout/
│   ├── AppShell.tsx
│   └── Sidebar.tsx
├── chat/
│   ├── ChatPanel.tsx
│   ├── MessageBubble.tsx
│   ├── MessageInput.tsx
│   ├── messageStyles.ts
│   └── markdownUtils.ts
├── combat/
│   ├── CombatLogPanel.tsx
│   ├── DeathSaveTracker.tsx
│   ├── ActionEconomyTracker.tsx
│   ├── ConditionManager.tsx
│   └── CharacterSheetPopup.tsx
├── game-state/
│   ├── GameStateOverview.tsx
│   ├── GameStateCombat.tsx
│   ├── GameStatePlot.tsx
│   ├── GameStateAgent.tsx
│   ├── GameStateMessages.tsx
│   ├── GameStateLLM.tsx
│   ├── GameStateTriggers.tsx
│   └── GameStateCards.tsx
├── admin/
│   ├── AdminTabs.tsx
│   ├── NPCManagement.tsx
│   ├── CharacterManagement.tsx
│   ├── PlotBoard.tsx
│   └── SystemConfig.tsx
├── dashboard/
│   ├── GameCardList.tsx
│   ├── CreateGameForm.tsx
│   └── JoinGameForm.tsx
└── shared/
    ├── ToolCallBanner.tsx
    ├── PlayerRollDialog.tsx
    ├── WelcomeScreen.tsx
    ├── SidePanel.tsx
    └── MarkdownRenderer.tsx
```

### 4. Pages Layer (Thin Orchestrators)

Each page becomes a thin file that composes its sub-components:

| Page | Before | After | Key Extractions |
|------|------:|------:|----------------|
| `GameRoomPage.tsx` | 2,206 | ~150 | ChatPanel, MessageBubble, MessageInput, messageStyles, markdownUtils |
| `GameStatePage.tsx` | 1,357 | ~120 | 7 GameState* components + GameStateCards |
| `CombatTab.tsx` | 1,523 | ~80 | CombatLogPanel, DeathSaveTracker, ActionEconomyTracker, ConditionManager |
| `AdminPage.tsx` | 639 | ~100 | Admin tabs, NPCManagement, CharacterManagement |
| `DashboardPage.tsx` | 607 | ~100 | GameCardList, CreateGameForm, JoinGameForm |
| `CharacterSheetPage.tsx` | 905 | ~200 | StatsPanel, SpellManager, EquipmentList, ConditionPanel |
| `CharacterCreateWizard.tsx` | 1,021 | ~150 | RaceStep, ClassStep, AbilityStep, BackgroundStep, EquipmentStep |
| `LLMPresetsPage.tsx` | 607 | ~300 | Already reasonable, minor cleanup |
| `LLMUsagePanel.tsx` | 521 | ~300 | Already reasonable, minor cleanup |
| `PlotBoardTab.tsx` | 439 | ~250 | Already reasonable, minor cleanup |
| `PlotBoardAdminTab.tsx` | 292 | ~200 | Already reasonable, minor cleanup |
| `DiceHistoryTab.tsx` | 242 | ~150 | Already reasonable, minor cleanup |
| `SystemsPage.tsx` | 209 | ~150 | Already reasonable, minor cleanup |
| `UserSettingsPage.tsx` | 647 | ~300 | Minor cleanup |
| `CombatLogViewerPage.tsx` | 312 | ~200 | Minor cleanup |
| `AuthPage.tsx` | 122 | ~100 | Already fine |
| `HomePage.tsx` | 84 | ~80 | Already fine |

**After refactor: no file exceeds 400 lines.**

### 5. Migration Strategy (Bottom-Up, Phased)

Each phase is independently verifiable — build and test after each one.

| Phase | What | Risk | Effort |
|-------|------|------|--------|
| **1. Types** | Split `types/index.ts` + `client.ts` types into `types/*.types.ts` | None — no behavioral change | 30 min |
| **2. Queries** | Extract query functions from `client.ts` into `api/queries/` | Low — hooks still work | 1 hr |
| **3. Hooks** | Split `gameHooks.ts`, `hubHook.ts` into domain-grouped files | Low — just import reorganization | 1 hr |
| **4. Components** | Extract sub-components from giant pages into `components/<domain>/` | Medium — component interfaces must match | 3-4 hr |
| **5. Pages** | Thin out remaining pages, delete extracted code | Low — pages just import from new locations | 1 hr |

**Total estimated effort: 8-10 hours.**

## Key Principles

- **No tabs in routing** — existing convention preserved
- **No lazy loading** — not a goal (readability + maintainability only)
- **Backwards-compatible imports** — barrel re-exports in `types/index.ts` and `api/` allow gradual migration
- **Each extracted file has a single responsibility**
- **No file exceeds 400 lines after refactor**
