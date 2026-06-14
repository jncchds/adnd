# UI Refactor Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Split the monolithic frontend files into domain-organized smaller files so no file exceeds 400 lines and code is easier to find and maintain.

**Status:** ✅ Completed

**Architecture:** Bottom-up extraction — types → queries → hooks → components → pages. Each phase is independently buildable. Barrel re-exports maintain backward compatibility during migration.

**Tech Stack:** React 19, TypeScript, MUI, Vite, ES modules

---

## Phase 1: Type Layer Split

### Task 1.1: Create `types/auth.types.ts`

**Files:**
- Create: `src/types/auth.types.ts`

**Step 1: Extract auth types**

Copy these types from `types/index.ts` (lines 800-816 in original, but this file is being split so find them in the current `types/index.ts`):

```typescript
export interface AuthUser {
  id: string;
  email: string;
  role: string;
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  user: AuthUser;
}
```

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s` (no errors)

**Step 3: Commit**

```bash
git add src/types/auth.types.ts
git commit -m "refactor(types): extract auth types into auth.types.ts"
```

### Task 1.2: Create `types/game.types.ts`

**Files:**
- Create: `src/types/game.types.ts`

**Step 1: Extract game types**

From `types/index.ts`, extract:
- Enums: `MessageType`, `GameStatus`, `GMStatus`, `PlayerRole`, `PlayerStatus`
- Interfaces: `GameListItem`, `GameDetail`, `InviteResponse`, `GMStatusResponse`, `SwayResponse`, `GameSessionListItem`, `GameSessionDetail`, `PlayerListItem`

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/types/game.types.ts
git commit -m "refactor(types): extract game types into game.types.ts"
```

### Task 1.3: Create `types/combat.types.ts`

**Files:**
- Create: `src/types/combat.types.ts`

**Step 1: Extract combat types**

From `types/index.ts`, extract:
- Enums: `CombatStatus`, `CombatEventType`, `CombatParticipantType`
- Interfaces: `CombatLog`, `CombatParticipantSummary`, `ConditionEntry`, `CombatLogEvent`, `CombatAttackResult`, `CombatSaveThrowResult`, `CombatDeathSaveResult`, `CombatSummary`, `InitiativeRoll`, `SpellCastResult`, `LevelUpResult`, `RestStatus`, `GridPosition`, `AISuggestions`, `AITacticalAction`, `AINPCAction`, `AICombatWarning`, `SANCheckResult`

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/types/combat.types.ts
git commit -m "refactor(types): extract combat types into combat.types.ts"
```

### Task 1.4: Create `types/plot.types.ts`

**Files:**
- Create: `src/types/plot.types.ts`

**Step 1: Extract plot types**

From `types/index.ts`, extract:
- Enums: `PlotThreadCategory`, `MilestoneStatus`, `OpportunityType`
- Interfaces: `PlotThreadResponse`, `ThreadUpdate`, `PlotReviewResponse`, `AdjustMomentumRequest`

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/types/plot.types.ts
git commit -m "refactor(types): extract plot types into plot.types.ts"
```

### Task 1.5: Create `types/llm.types.ts`

**Files:**
- Create: `src/types/llm.types.ts`

**Step 1: Extract LLM types**

From `types/index.ts`, extract:
- Enum: `LLMProviderType`
- Interfaces: `LLMPreset`, `LLMInteractionLog`, `PresetUsageSummary`, `GameProviderUsageSummary`, `ProviderStatus`, `ConsistencyReport`

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/types/llm.types.ts
git commit -m "refactor(types): extract LLM types into llm.types.ts"
```

### Task 1.6: Create `types/agent.types.ts`

**Files:**
- Create: `src/types/agent.types.ts`

**Step 1: Extract agent types**

From `types/index.ts`, extract:
- Enums: `AgentType`, `AgentAction`, `AgentCallStatus`

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/types/agent.types.ts
git commit -m "refactor(types): extract agent types into agent.types.ts"
```

### Task 1.7: Create `types/message.types.ts`

**Files:**
- Create: `src/types/message.types.ts`

**Step 1: Extract message types**

From `types/index.ts`, extract:
- Enum: `WhisperType`

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/types/message.types.ts
git commit -m "refactor(types): extract message types into message.types.ts"
```

### Task 1.8: Create `types/gm.types.ts`

**Files:**
- Create: `src/types/gm.types.ts`

**Step 1: Extract GM types**

From `types/index.ts`, extract:
- Enums: `ToolCallStatus`, `ToolCategory`
- Interfaces: `ToolCallInfo`, `ToolCallConfirmationResponse`, `PlayerRollConfirmationResponse`, `ToolCallNotification`, `GMToolDefinition`, `GMToolResponse`, `ExecuteToolRequest`, `ExecuteToolResponse`, `DiceHistoryEntry`, `DiceHistoryResponse`, `CombatSummaryEntry`, `CombatLogResponse`, `CombatParticipantEntry`, `CombatEventEntry`, `SpellEntry`, `SpellSlotInfo`, `SpellManagementResponse`, `SpellUpdateRequest`, `SessionNote`

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/types/gm.types.ts
git commit -m "refactor(types): extract GM types into gm.types.ts"
```

### Task 1.9: Create `types/template.types.ts`

**Files:**
- Create: `src/types/template.types.ts`

**Step 1: Extract template types**

From `types/index.ts`, extract:
- Interface: `GameTemplate`
- Interfaces: `CreateGameTemplateRequest`, `UpdateGameTemplateRequest`

**Step 2: Update `types/index.ts`**

Replace the entire file with a barrel re-export:

```typescript
export * from './auth.types';
export * from './game.types';
export * from './combat.types';
export * from './plot.types';
export * from './llm.types';
export * from './agent.types';
export * from './message.types';
export * from './gm.types';
export * from './template.types';
```

**Step 3: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 4: Commit**

```bash
git add src/types/template.types.ts src/types/index.ts
git commit -m "refactor(types): complete type layer split with barrel re-exports"
```

---

## Phase 2: API Client Cleanup

### Task 2.1: Remove types from `client.ts`

**Files:**
- Modify: `src/api/client.ts`

**Step 1: Delete all type definitions from `client.ts`**

Everything from the first `export interface` / `export enum` to just before `export const api = new APIClient();` — that's roughly lines 800-1540. Delete ALL of it.

Keep only:
- The `APIClient` class definition
- The `export const api = new APIClient();` line
- Any utility functions that are purely HTTP helpers

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/client.ts
git commit -m "refactor(api): remove types from client.ts, types live in types/"
```

---

## Phase 3: Hooks Organization

### Task 3.1: Create `api/hooks/useGame.ts`

**Files:**
- Create: `src/api/hooks/useGame.ts`

**Step 1: Extract game-scoped hooks from `gameHooks.ts`**

Copy these functions from `api/gameHooks.ts`:
- `useGames()` (line 5)
- `useGame(id)` (line 156)
- `useSessions(gameId)` (line 182)
- `usePlayers(gameId)` (line 206)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useGame.ts
git commit -m "refactor(hooks): extract game hooks into useGame.ts"
```

### Task 3.2: Create `api/hooks/useCombat.ts`

**Files:**
- Create: `src/api/hooks/useCombat.ts`

**Step 1: Extract combat hooks from `gameHooks.ts`**

Copy these from `api/gameHooks.ts`:
- `useCombats(gameId)` (line 692)
- `useCombat(combatId, gameId)` (line 719)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useCombat.ts
git commit -m "refactor(hooks): extract combat hooks into useCombat.ts"
```

### Task 3.3: Create `api/hooks/usePlot.ts`

**Files:**
- Create: `src/api/hooks/usePlot.ts`

**Step 1: Extract plot hooks from `gameHooks.ts`**

Copy these from `api/gameHooks.ts`:
- `usePlotWeaver(gameId)` (line 562)
- `usePlotThreads(gameId)` (line 257)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/usePlot.ts
git commit -m "refactor(hooks): extract plot hooks into usePlot.ts"
```

### Task 3.4: Create `api/hooks/useLLM.ts`

**Files:**
- Create: `src/api/hooks/useLLM.ts`

**Step 1: Extract LLM hooks from `gameHooks.ts`**

Copy these from `api/gameHooks.ts`:
- `useLLMPresets()` (line 353)
- `useProviderModels()` (line 384)
- `useUserLLMUsage()` (line 408)
- `useLLMInteractions(gameId)` (line 436)
- `useGameProviderUsage(gameId)` (line 529)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useLLM.ts
git commit -m "refactor(hooks): extract LLM hooks into useLLM.ts"
```

### Task 3.5: Create `api/hooks/useAgent.ts`

**Files:**
- Create: `src/api/hooks/useAgent.ts`

**Step 1: Extract agent hooks from `gameHooks.ts`**

Copy from `api/gameHooks.ts`:
- `usePendingCalls(gameId)` (line 496)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useAgent.ts
git commit -m "refactor(hooks): extract agent hooks into useAgent.ts"
```

### Task 3.6: Create `api/hooks/useGM.ts`

**Files:**
- Create: `src/api/hooks/useGM.ts`

**Step 1: Extract GM hooks from `gameHooks.ts`**

Copy from `api/gameHooks.ts`:
- `useGMTools(gameId)` (line 748)
- `useSpells(characterId)` (line 796)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useGM.ts
git commit -m "refactor(hooks): extract GM hooks into useGM.ts"
```

### Task 3.7: Create `api/hooks/useMessages.ts`

**Files:**
- Create: `src/api/hooks/useMessages.ts`

**Step 1: Extract message hooks from `gameToolsHook.ts`**

Copy from `api/gameToolsHook.ts`:
- `useMessagesPaginated(gameId, sessionId)` (line 267)
- `useMessagesInfiniteScroll(gameId, sessionId)` (line 311)
- `useMessageSearch(gameId, sessionId)` (line 524)
- `UnifiedMessage`, `UnifiedMessageType` types
- `useSessionNotes` (line 158)
- `useDiceStats` (line 210)
- `usePlayerDiceStats` (line 237)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useMessages.ts
git commit -m "refactor(hooks): extract message hooks into useMessages.ts"
```

### Task 3.8: Create `api/hooks/useDice.ts`

**Files:**
- Create: `src/api/hooks/useDice.ts`

**Step 1: Extract dice hooks from `gameHooks.ts`**

Copy from `api/gameHooks.ts`:
- `useDiceHistory(gameId)` (line 658)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useDice.ts
git commit -m "refactor(hooks): extract dice hooks into useDice.ts"
```

### Task 3.9: Create `api/hooks/useTemplates.ts`

**Files:**
- Create: `src/api/hooks/useTemplates.ts`

**Step 1: Extract template hooks from `gameHooks.ts`**

Copy from `api/gameHooks.ts`:
- `useGameTemplates()` (line 873)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useTemplates.ts
git commit -m "refactor(hooks): extract template hooks into useTemplates.ts"
```

### Task 3.10: Create `api/hooks/useHub.ts`

**Files:**
- Create: `src/api/hooks/useHub.ts`

**Step 1: Extract SignalR types and hook from `hubHook.ts`**

Copy from `api/hubHook.ts`:
- All event interfaces (lines 14-280)
- `createHubConnection()` function (line 281)
- `useGameHub()` hook (line 370)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useHub.ts
git commit -m "refactor(hooks): extract SignalR hub into useHub.ts"
```

### Task 3.11: Create `api/hooks/useGameState.ts`

**Files:**
- Create: `src/api/hooks/useGameState.ts`

**Step 1: Extract game state hook from `gameStateHook.ts`**

Copy from `api/gameStateHook.ts`:
- `useGameGameState(gameId)` function

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/api/hooks/useGameState.ts
git commit -m "refactor(hooks): extract game state hook into useGameState.ts"
```

### Task 3.12: Keep `api/hooks/useAuth.ts` and `api/hooks/useEntity.ts`

**Files:**
- Create: `src/api/hooks/useAuth.ts` (rename from `authHook.tsx`)
- Keep: `src/api/hooks/useEntity.ts` (rename from `useEntity.ts` — already fine)

**Step 1: Rename authHook.tsx to useAuth.ts**

```bash
git mv src/api/authHook.tsx src/api/hooks/useAuth.ts
```

**Step 2: Move useEntity.ts to hooks**

```bash
git mv src/api/useEntity.ts src/api/hooks/useEntity.ts
```

**Step 3: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 4: Commit**

```bash
git add src/api/hooks/useAuth.ts src/api/hooks/useEntity.ts
git commit -m "refactor(hooks): organize remaining hooks into hooks/ directory"
```

### Task 3.13: Delete old hook files

**Files:**
- Delete: `src/api/gameHooks.ts`
- Delete: `src/api/hubHook.ts`
- Delete: `src/api/gameStateHook.ts`
- Delete: `src/api/gameToolsHook.ts`
- Delete: `src/api/authHook.tsx`

**Step 1: Remove old files**

```bash
rm src/api/gameHooks.ts src/api/hubHook.ts src/api/gameStateHook.ts src/api/gameToolsHook.ts src/api/authHook.tsx
```

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git rm src/api/gameHooks.ts src/api/hubHook.ts src/api/gameStateHook.ts src/api/gameToolsHook.ts src/api/authHook.tsx
git commit -m "refactor(hooks): remove old hook files after migration to hooks/"
```

---

## Phase 4: Component Extraction

### Task 4.1: Create `components/chat/messageStyles.ts`

**Files:**
- Create: `src/components/chat/messageStyles.ts`

**Step 1: Extract MESSAGE_STYLES from `GameRoomPage.tsx`**

Copy the `MESSAGE_STYLES` constant (lines ~50-137 in GameRoomPage.tsx) and export it:

```typescript
export const MESSAGE_STYLES: Record<UnifiedMessageType, {
  bg: string;
  border: string;
  chipColor: 'primary' | 'info' | 'warning' | 'success' | 'error' | 'default';
  chipLabel: string;
  chipIcon: string;
}> = { ... };
```

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/chat/messageStyles.ts
git commit -m "refactor(chat): extract message styles config into messageStyles.ts"
```

### Task 4.2: Create `components/chat/markdownUtils.ts`

**Files:**
- Create: `src/components/chat/markdownUtils.ts`

**Step 1: Extract markdown utilities from `GameRoomPage.tsx`**

Copy from GameRoomPage.tsx:
- `MARKDOWN_TYPES` constant
- `hasMarkdownSyntax()` function

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/chat/markdownUtils.ts
git commit -m "refactor(chat): extract markdown utilities into markdownUtils.ts"
```

### Task 4.3: Create `components/chat/MessageBubble.tsx`

**Files:**
- Create: `src/components/chat/MessageBubble.tsx`

**Step 1: Extract MessageBubble from `GameRoomPage.tsx`**

Copy the `MessageBubble` component (lines ~1845-2019 in GameRoomPage.tsx). It takes `{ msg }: { msg: UnifiedMessage }` as props.

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/chat/MessageBubble.tsx
git commit -m "refactor(chat): extract MessageBubble component"
```

### Task 4.4: Create `components/chat/ChatPanel.tsx`

**Files:**
- Create: `src/components/chat/ChatPanel.tsx`

**Step 1: Extract UnifiedChatPanel from `GameRoomPage.tsx`**

Copy the `UnifiedChatPanel` component (lines ~1457-1842 in GameRoomPage.tsx). It takes the full set of props that GameRoomPage currently passes to it.

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/chat/ChatPanel.tsx
git commit -m "refactor(chat): extract ChatPanel component"
```

### Task 4.5: Create `components/combat/CombatLogPanel.tsx`

**Files:**
- Create: `src/components/combat/CombatLogPanel.tsx`

**Step 1: Extract CombatLogPanel from `CombatTab.tsx`**

Copy the `CombatLogPanel` component (lines ~39-132 in CombatTab.tsx).

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/combat/CombatLogPanel.tsx
git commit -m "refactor(combat): extract CombatLogPanel component"
```

### Task 4.6: Create `components/combat/DeathSaveTracker.tsx`

**Files:**
- Create: `src/components/combat/DeathSaveTracker.tsx`

**Step 1: Extract DeathSaveTracker from `CombatTab.tsx`**

Copy the `DeathSaveTracker` component (lines ~133-163 in CombatTab.tsx).

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/combat/DeathSaveTracker.tsx
git commit -m "refactor(combat): extract DeathSaveTracker component"
```

### Task 4.7: Create `components/combat/ActionEconomyTracker.tsx`

**Files:**
- Create: `src/components/combat/ActionEconomyTracker.tsx`

**Step 1: Extract ActionEconomyTracker from `CombatTab.tsx`**

Copy the `ActionEconomyTracker` component (lines ~166-232 in CombatTab.tsx).

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/combat/ActionEconomyTracker.tsx
git commit -m "refactor(combat): extract ActionEconomyTracker component"
```

### Task 4.8: Create `components/combat/ConditionManager.tsx`

**Files:**
- Create: `src/components/combat/ConditionManager.tsx`

**Step 1: Extract ConditionManager from `CombatTab.tsx`**

Copy the `ConditionManager` component (lines ~235-433 in CombatTab.tsx).

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/combat/ConditionManager.tsx
git commit -m "refactor(combat): extract ConditionManager component"
```

### Task 4.9: Create `components/combat/CharacterSheetPopup.tsx`

**Files:**
- Create: `src/components/combat/CharacterSheetPopup.tsx`

**Step 1: Extract CharacterSheetPopup from `CombatTab.tsx`**

Copy the `CharacterSheetPopup` component (lines ~434-537 in CombatTab.tsx).

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/combat/CharacterSheetPopup.tsx
git commit -m "refactor(combat): extract CharacterSheetPopup component"
```

### Task 4.10: Create `components/game-state/GameStateCards.tsx`

**Files:**
- Create: `src/components/game-state/GameStateCards.tsx`

**Step 1: Extract shared cards from `GameStatePage.tsx`**

Copy from GameStatePage.tsx:
- `SectionCard` component (lines ~1025-1045)
- `StatCard` component (lines ~1053-1062)
- `PlotThreadCard` component (lines ~1067-1158)

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/game-state/GameStateCards.tsx
git commit -m "refactor(game-state): extract shared card components"
```

### Task 4.11: Extract remaining game-state tabs

**Files:**
- Create: `src/components/game-state/GameStateOverview.tsx`
- Create: `src/components/game-state/GameStateCombat.tsx`
- Create: `src/components/game-state/GameStatePlot.tsx`
- Create: `src/components/game-state/GameStateAgent.tsx`
- Create: `src/components/game-state/GameStateMessages.tsx`
- Create: `src/components/game-state/GameStateLLM.tsx`
- Create: `src/components/game-state/GameStateTriggers.tsx`

**Step 1: Extract each tab from `GameStatePage.tsx`**

For each tab, copy the component function from GameStatePage.tsx:
- `OverviewTab` → GameStateOverview.tsx
- `CombatTab` → GameStateCombat.tsx
- `PlotTab` → GameStatePlot.tsx
- `AgentTab` → GameStateAgent.tsx
- `MessagesTab` → GameStateMessages.tsx
- `LLMTab` → GameStateLLM.tsx
- `TriggersTab` → GameStateTriggers.tsx

**Step 2: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Commit**

```bash
git add src/components/game-state/
git commit -m "refactor(game-state): extract all tab components"
```

---

## Phase 5: Page Thinning

### Task 5.1: Thin `GameRoomPage.tsx`

**Files:**
- Modify: `src/pages/GameRoomPage.tsx`

**Step 1: Replace imports**

Change imports to use extracted components:

```typescript
// Remove these imports:
import { UnifiedMessage, UnifiedMessageType } from '../api/gameToolsHook';
// Add:
import { UnifiedMessage, UnifiedMessageType } from '../api/hooks/useMessages';
import { MESSAGE_STYLES } from '../components/chat/messageStyles';
import { hasMarkdownSyntax, MARKDOWN_TYPES } from '../components/chat/markdownUtils';
import MessageBubble from '../components/chat/MessageBubble';
import ChatPanel from '../components/chat/ChatPanel';
```

**Step 2: Delete extracted code from GameRoomPage.tsx**

Remove from GameRoomPage.tsx:
- `MARKDOWN_TYPES` constant
- `hasMarkdownSyntax()` function
- `MESSAGE_STYLES` constant
- `UnifiedChatPanel` component
- `MessageBubble` component

Keep:
- `GameRoomPage` default export (the main orchestrator)
- `SettingsTab` (or extract it separately)
- `getAgentLabel`, `getActionLabel` helpers (or move to shared)
- `MessageInputType`, `MessageTarget` types

**Step 3: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 4: Check line count**

Run: `wc -l src/pages/GameRoomPage.tsx`
Expected: ~150-200 lines

**Step 5: Commit**

```bash
git add src/pages/GameRoomPage.tsx
git commit -m "refactor(pages): thin GameRoomPage, extract chat components"
```

### Task 5.2: Thin `GameStatePage.tsx`

**Files:**
- Modify: `src/pages/GameStatePage.tsx`

**Step 1: Replace imports**

```typescript
import { SectionCard, StatCard, PlotThreadCard } from '../components/game-state/GameStateCards';
import GameStateOverview from '../components/game-state/GameStateOverview';
import GameStateCombat from '../components/game-state/GameStateCombat';
import GameStatePlot from '../components/game-state/GameStatePlot';
import GameStateAgent from '../components/game-state/GameStateAgent';
import GameStateMessages from '../components/game-state/GameStateMessages';
import GameStateLLM from '../components/game-state/GameStateLLM';
import GameStateTriggers from '../components/game-state/GameStateTriggers';
```

**Step 2: Delete extracted code**

Remove all tab components, helper functions, and card components from GameStatePage.tsx.

**Step 3: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 4: Check line count**

Run: `wc -l src/pages/GameStatePage.tsx`
Expected: ~100-120 lines

**Step 5: Commit**

```bash
git add src/pages/GameStatePage.tsx
git commit -m "refactor(pages): thin GameStatePage, extract tab components"
```

### Task 5.3: Thin `CombatTab.tsx`

**Files:**
- Modify: `src/pages/CombatTab.tsx`

**Step 1: Replace imports**

```typescript
import CombatLogPanel from '../components/combat/CombatLogPanel';
import DeathSaveTracker from '../components/combat/DeathSaveTracker';
import ActionEconomyTracker from '../components/combat/ActionEconomyTracker';
import ConditionManager from '../components/combat/ConditionManager';
import CharacterSheetPopup from '../components/combat/CharacterSheetPopup';
```

**Step 2: Delete extracted code**

Remove all sub-components from CombatTab.tsx.

**Step 3: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 4: Check line count**

Run: `wc -l src/pages/CombatTab.tsx`
Expected: ~60-80 lines

**Step 5: Commit**

```bash
git add src/pages/CombatTab.tsx
git commit -m "refactor(pages): thin CombatTab, extract combat components"
```

### Task 5.4: Thin remaining pages

**Files to modify:**
- `src/pages/AdminPage.tsx` → ~100 lines
- `src/pages/DashboardPage.tsx` → ~100 lines
- `src/pages/CharacterSheetPage.tsx` → ~200 lines
- `src/pages/CharacterCreateWizard.tsx` → ~150 lines
- `src/pages/LLMPresetsPage.tsx` → ~300 lines (minor cleanup)
- `src/pages/LLMUsagePanel.tsx` → ~300 lines (minor cleanup)
- `src/pages/UserSettingsPage.tsx` → ~300 lines (minor cleanup)

**Step 1: Extract sub-components from each page**

For each page, identify extractable sub-components and create them in `components/<domain>/`.

**Step 2: Verify build after each page**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 3: Check all line counts**

Run: `wc -l src/pages/*.tsx src/api/hooks/*.ts src/api/client.ts src/components/chat/*.tsx src/components/chat/*.ts src/components/combat/*.tsx src/components/game-state/*.tsx`
Expected: No file exceeds 400 lines

**Step 4: Final commit**

```bash
git add src/pages/ src/components/
git commit -m "refactor(pages): thin all remaining pages, finalize component extraction"
```

---

## Phase 6: Final Cleanup

### Task 6.1: Update all import paths across the codebase

**Step 1: Find remaining old imports**

```bash
grep -r "from.*gameHooks\|from.*hubHook\|from.*gameStateHook\|from.*gameToolsHook\|from.*authHook" src/ --include="*.tsx" --include="*.ts" | grep -v "node_modules"
```

**Step 2: Update each import**

Replace old import paths with new ones:
- `from '../api/gameHooks'` → `from '../api/hooks/useGame'` (or specific hook)
- `from '../api/hubHook'` → `from '../api/hooks/useHub'`
- `from '../api/gameStateHook'` → `from '../api/hooks/useGameState'`
- `from '../api/gameToolsHook'` → `from '../api/hooks/useMessages'`
- `from '../api/authHook'` → `from '../api/hooks/useAuth'`

**Step 3: Verify build**

Run: `cd src/Adnd.Client && npm run build 2>&1 | tail -5`
Expected: `✓ built in ~22s`

**Step 4: Commit**

```bash
git add src/
git commit -m "refactor: update all imports to new file structure"
```

### Task 6.2: Verify no file exceeds 400 lines

**Step 1: Check all file sizes**

```bash
find src/ -name "*.tsx" -o -name "*.ts" | xargs wc -l | sort -n | tail -20
```

**Step 2: If any file exceeds 400 lines, extract further**

**Step 3: Final build verification**

Run: `cd src/Adnd.Client && npm run build 2>&1`
Expected: Clean build, no errors

**Step 4: Final commit**

```bash
git add src/
git commit -m "refactor: finalize UI file structure, verify no file exceeds 400 lines"
```

---

## Verification Checklist

After all phases complete:

- [ ] `npm run build` passes with no errors
- [ ] No file exceeds 400 lines
- [ ] `GameRoomPage.tsx` < 200 lines
- [ ] `GameStatePage.tsx` < 150 lines
- [ ] `CombatTab.tsx` < 100 lines
- [ ] `client.ts` < 400 lines
- [ ] All `types/*.ts` files < 100 lines
- [ ] All `api/hooks/*.ts` files < 200 lines
- [ ] All `api/queries/*.ts` files exist (if created)
- [ ] All old hook files deleted
- [ ] All imports updated to new paths
