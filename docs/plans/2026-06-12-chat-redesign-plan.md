# Chat Redesign — Implementation Plan

> **Date:** 2026-06-12
> **Branch:** `chat-redesign`
> **Worktree:** `../adnd-chat-redesign`
> **Design doc:** `docs/plans/2026-06-12-chat-redesign-design.md`

## Overview

Redesign the game chat page with a 3-zone layout: collapsible combat panel → message feed → single-line input. Combat events appear both in the feed as narrative cards and in the quick-reference panel.

## Tasks

### Task 1: Simplify ChatPanel component

**File:** `src/Adnd.Client/src/components/chat/ChatPanel.tsx`

Remove the top panel controls (type toggle, receiver selector, quick actions, dice history). Keep only:
- Message feed area (scrollable)
- Input area (single-line)
- Message rendering via MessageBubble

The type toggle and receiver selector move to `GameChatPage` as the page controls the context.

**Changes:**
- Remove `inputType`, `inputTarget`, `whisperTargetPlayer`, `whisperInput` props
- Remove top panel JSX (type toggle, receiver selector, quick actions, dice history, player chips)
- Keep `messages`, `isLoadingMore`, `hasMore`, `loadMoreOldest` props
- Keep `onSend`, `onDiceRoll` callbacks
- Keep scroll detection, message rendering
- Simplify to ~100 lines from ~270

### Task 2: Redesign GameChatPage with 3-zone layout

**File:** `src/Adnd.Client/src/pages/GameChatPage.tsx`

Complete rewrite. Three zones:

**Zone A — Collapsible Combat Panel:**
- Show only when active combat exists
- Fetch combat state from existing API (`/admin/games/{gameId}/combat` or similar)
- Display initiative order with current turn highlighted
- Show HP (color-coded), AC, conditions per participant
- Collapse/expand button
- "View Combat Tab" link to `/game/:id/combat`
- Auto-update via SignalR on combat events

**Zone B — Message Feed:**
- Use existing `useMessagesInfiniteScroll` hook
- Load newest page on mount, auto-scroll to bottom
- Render messages via ChatPanel component
- Show "new messages" indicator when scrolled up
- Show "Load older messages" on scroll-to-top

**Zone C — Single-Line Input:**
- Type toggle (In-Game / OOC) — pill-style switch
- Receiver dropdown (All / To GM / To Player)
- Player chips for GM whisper target (below input)
- Single text input with Send button + 🎲 dice icon
- Placeholder text changes based on type + receiver

### Task 3: Add combat state tracking

**File:** `src/Adnd.Client/src/pages/GameChatPage.tsx`

- Add hook to fetch active combat state
- Monitor SignalR for combat events (`CombatStarted`, `CombatEnded`, `CombatDamageDealt`, etc.)
- Update combat panel reactively
- Handle combat start/end (expand/collapse panel, show feed message)

**Data source:** Check existing combat API endpoints — may need to add a simple `GET /admin/games/{gameId}/combat/active` endpoint that returns the active combat with participants.

### Task 4: Wire up SignalR for combat panel updates

**File:** `src/Adnd.Client/src/pages/GameChatPage.tsx`

Subscribe to combat-related SignalR events:
- `CombatDamageDealt` — update HP
- `CombatConditionApplied/Removed` — update conditions
- `TurnAdvanced` — update current turn
- `ParticipantAdded/Removed` — update participant list
- `CombatStarted` — expand combat panel
- `CombatEnded` — collapse combat panel

Use existing `useGameHub` hook for SignalR connection.

### Task 5: Test and verify

- [ ] Combat panel shows when combat is active
- [ ] Combat panel collapses/expands correctly
- [ ] Current turn highlighted with arrow
- [ ] HP color-coded correctly
- [ ] Conditions shown as chips with duration
- [ ] Message feed loads newest first
- [ ] Infinite scroll works (load older)
- [ ] Auto-scroll on new messages (when at bottom)
- [ ] "New messages" indicator when scrolled up
- [ ] Input type toggle works (In-Game / OOC)
- [ ] Receiver dropdown works (All / To GM / To Player)
- [ ] Player chips appear for GM whisper target
- [ ] Combat events appear in both feed and panel
- [ ] No regressions in existing pages

## Files Changed

| File | Action |
|------|--------|
| `src/Adnd.Client/src/pages/GameChatPage.tsx` | Complete rewrite |
| `src/Adnd.Client/src/components/chat/ChatPanel.tsx` | Simplify (remove top panel) |
| `src/Adnd.Server/Controllers/GamesController.cs` | May add `GET combat/active` endpoint |
| `src/Adnd.Server/Services/CombatService.cs` | May add `GetActiveCombatAsync` method |

## Dependencies

- Uses existing `useMessagesInfiniteScroll` hook (no changes needed)
- Uses existing `MessageBubble` component (no changes needed)
- Uses existing `messageStyles.ts` (no changes needed)
- Uses existing `useGameHub` hook for SignalR
- Uses existing combat API endpoints (may need one new endpoint)
