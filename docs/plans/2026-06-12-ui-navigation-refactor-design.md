# UI Navigation Refactor Design

> Date: 2026-06-12

## Problem

- `GameRoomPage.tsx` uses internal `<Tabs>` (Chat, Combat, Characters, Settings) — redundant with the SidePanel's own navigation
- `AdminPage.tsx` uses path-based view selection — a single-page tab-like pattern rather than distinct pages
- Both pages load all hooks for all sub-views regardless of which one is active
- Navigation is split between URL routing and internal tab state

## Goal

Remove all tab-based navigation. Each feature gets its own page file, navigated purely through the left sidebar.

## New File Structure

### Game Side (3 new pages)

| File | Route | Contents |
|------|-------|----------|
| `GameChatPage.tsx` | `/game/:id` | ChatPanel, message input, ToolCallBanner |
| `GameCombatPage.tsx` | `/game/:id/combat` | CombatTab component |
| `GameSettingsPage.tsx` | `/game/:id/settings` | GM status, player list, character creation wizard |

### Admin Side (7 new pages)

| File | Route | Contents |
|------|-------|----------|
| `AdminDashboardPage.tsx` | `/admin/:id` | GameStatePage content |
| `AdminPlotBoardPage.tsx` | `/admin/:id/plot-board` | PlotBoardAdminTab + plot hooks |
| `AdminNPCsPage.tsx` | `/admin/:id/npcs` | AdminNPCsTab + AdminNPCDialog + NPC hooks |
| `AdminCharactersPage.tsx` | `/admin/:id/characters` | AdminCharactersTab + character hooks |
| `AdminConsistencyPage.tsx` | `/admin/:id/consistency` | AdminConsistencyTab + consistency hooks |
| `AdminLLMLogsPage.tsx` | `/admin/:id/llm-logs` | AdminLLMLogsTab + LLM hooks + detail dialog |
| `AdminAgentCallsPage.tsx` | `/admin/:id/agent-calls` | AdminAgentCallsTab + agent call dialog |

### Removed Files

- `GameRoomPage.tsx` — replaced by the 3 game pages
- `AdminPage.tsx` — replaced by the 7 admin pages
- `GameStatePage.tsx` — merged into `AdminDashboardPage.tsx`

### Modified Files

- `App.tsx` — routes point to new page files
- `AppShell.tsx` — simplified state tracking (only `currentView` + `gameId`)
- `SidePanel.tsx` — uses `useLocation()` directly for highlighting, no `activeGameTab`/`activeAdminTab` props

## Navigation State

### AppShell
- `currentView`: `'dashboard' | 'llm-presets' | 'systems' | 'user-settings' | 'game' | 'admin' | 'welcome'`
- `gameId`: current game ID (derived from URL)
- Removed: `activeGameTab`, `activeAdminTab` props — SidePanel uses `useLocation()` directly

### SidePanel
- Uses `useLocation()` to determine which sub-item to highlight
- When `currentView === 'game'` — highlights Chat/Combat/Settings
- When `currentView === 'admin'` — highlights Dashboard/Plot Board/NPCs/Characters/Consistency/LLM Logs/Agent Calls

### Back Button Behavior

| From | Back goes to |
|------|-------------|
| Admin pages | `/game/:id` (game chat) |
| Game chat/combat | `/dashboard` (game list) |
| Game settings | `/dashboard` (game list) |

## Data Flow

Each page file loads only the hooks it needs:

- **GameChatPage**: `useGame`, `useMessagesInfiniteScroll`, `useToolCalls`
- **GameCombatPage**: `useGame` (minimal)
- **GameSettingsPage**: `useGame`, `usePlayers`, `useGMStatus`
- **AdminDashboardPage**: `useGame`
- **AdminNPCsPage**: `useNPCs`
- **AdminPlotBoardPage**: `usePlotWeaver`
- **AdminCharactersPage**: `useCharacters`
- **AdminConsistencyPage**: `useConsistency`
- **AdminLLMLogsPage**: LLM hooks + filter state
- **AdminAgentCallsPage**: Agent call hooks

Shared components (`CombatTab.tsx`, `ChatPanel.tsx`, `CharacterCreateWizard.tsx`, `ToolCallBanner.tsx`, tab components) stay in `components/` — no move needed.

## Benefits

1. **No tabs** — pure URL + sidebar navigation
2. **Lazy loading** — each page loads only its own hooks
3. **Cleaner components** — each page is focused, no conditional rendering branches
4. **Self-contained SidePanel** — uses `useLocation()` for highlighting, no prop drilling
