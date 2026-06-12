# Chat Redesign — "Everything is a Message"

> **Date:** 2026-06-12
> **Status:** Design Approved
> **Scope:** GameRoomPage + ChatPanel component — full redesign of the main game chat interface

## Problem Statement

The current game room has:
- A sidebar-style approach where combat, dice history, and other panels compete with chat
- Message type filtering is missing or unclear
- No proper infinite scroll from newest to oldest
- Input area is fragmented with multiple panels instead of a single-line input

## Design Philosophy

**"Everything is a message."** Combat is narrative. The chat IS the game log. Anyone reading the chat later can understand exactly what happened.

## Layout

Three zones in a full-height column:

### Zone A: Collapsible Combat Panel

Appears only when combat is active. Sits at the top of the chat.

```
┌────────────────────────────────────────────────┐
│  ⚔️ Combat: "Goblin Ambush"        [Collapse ▼]│
├────────────────────────────────────────────────┤
│  Initiative Order:                              │
│  ┌────────────────────────────────────────────┐│
│  │ ⏩ Thorin  20  HP: 45/50  AC: 18           ││  ← Current turn (highlighted)
│  │    Conditions: [Frightened][1]              ││
│  ├────────────────────────────────────────────┤│
│  │ Elara     17  HP: 32/32  AC: 14           ││
│  │    Conditions: [None]                       ││
│  ├────────────────────────────────────────────┤│
│  │ Goblin #1 14  HP: 7/12   AC: 11           ││
│  │    Conditions: [Bleeding]                   ││
│  └────────────────────────────────────────────┘│
│  [View Combat Tab →]  [End Combat]             │
└────────────────────────────────────────────────┘
```

**Design decisions:**
- Always shows current turn first (highlighted with arrow `⏩`)
- Inline conditions as chips with duration countdown
- HP color-coded: green >75%, yellow >25%, red <25%
- Collapse button hides but doesn't remove from DOM
- "View Combat Tab" links to `/game/:id/combat` for full grid view
- Only visible when combat exists
- Auto-updates via SignalR on HP/conditions/initiative changes

### Zone B: Message Feed

The heart of the page. All messages rendered as styled cards.

**Scroll direction:** Newest at bottom, oldest at top. Auto-scrolls to bottom on new messages.

**Message type visual styles:**

| Category | Border | Background | Chip |
|----------|--------|------------|------|
| In-Game chat | Green | Subtle green | 🎮 In-Game |
| OOC chat | Blue | Subtle blue | 📢 OOC |
| GM/AI Narration | Amber | Subtle amber | 🎬 Narration |
| Combat events | Red | Subtle red | ⚔️ Combat |
| Dice rolls | Orange | Subtle orange | 🎲 Dice |
| Skill checks | Purple | Subtle purple | 📋 Check |
| Whispers | Yellow bg | Yellow tint | 🤫 Whisper |
| System events | Gray | Subtle gray | ⚙️ System |

**Message card structure (uniform):**
```
┌──────────────────────────────────────┐
│ ⚔️ Combat  Thorin  ·  3:42 PM      │  ← Chip + Sender + Time
│  "Goblin #1 takes 8 slashing damage" │  ← Content
│  HP: 7/12  ·  Conditions: Bleeding  │  ← Inline extras
└──────────────────────────────────────┘
```

**Infinite scroll:**
- Load newest page on mount (auto-scroll to bottom)
- "Load older messages" trigger on scroll-to-top
- Anchor-based pagination

**New message behavior:**
- If at bottom → auto-scroll to follow
- If scrolled up → no auto-scroll, show "new messages" indicator
- Collapsible combat panel updates in real-time via SignalR

### Zone C: Input Area

Single-line input at the bottom with two controls.

```
┌──────────────────────────────────────────────────┐
│  🎮 In-Game       🌐  All  [▼]   ┌──────────┐  │
│  ┌─────────────────────────────────────────┐   │
│  │ Speak in-character...                    │   │
│  └─────────────────────────────────────────┘   │
│                              [🎲]  [Send]       │
└──────────────────────────────────────────────────┘
```

**Two controls:**

1. **Type toggle** (left) — pill-style switch:
   - `🎮 In-Game` (green) — influences narrative
   - `📢 OOC` (blue) — never influences narrative

2. **Receiver dropdown** (right) — Select component:
   - `🌐 All (Public)` — visible to everyone
   - `🤫 To GM` — whisper to GM
   - `📩 To Player...` — GM/Creator only, shows player chips below input

**Input behavior:**
- Single line, expands to multiline on focus (max 4 rows)
- Enter to send, Shift+Enter for newline
- Placeholder changes based on type + receiver
- 🎲 dice icon for quick dice picker
- Player chips appear below input when "To Player" selected

## Data Flow

### Real-time updates
- **Combat panel** — updates on `CombatDamageDealt`, `CombatConditionApplied/Removed`, `TurnAdvanced`, `ParticipantAdded/Removed`
- **Message feed** — new messages via SignalR group `game:{gameId}`
- **Combat start/end** — triggers panel visibility + "Combat Started" message
- **Player join/leave** — system messages in feed

### Message flow
```
User types → SignalR hub method → Server publishes event → 
  MediatR handlers → DB write → Broadcast back → 
  All clients receive → Message added to feed
```

### Combat panel visibility
- Monitors `CombatStarted` / `CombatEnded` events
- Combat starts → panel expands
- Combat ends → panel collapses, shows "Combat Ended" in feed
- Manual collapse doesn't hide permanently (expands on next update)

### Scroll behavior
- On mount: load newest page, auto-scroll to bottom
- On new message: if at bottom → scroll down; if scrolled up → "new messages" pill
- Load older: button or scroll-to-top triggers pagination

## Combat Integration Philosophy

- Every combat event appears as a message in the feed
- Combat panel is a **quick reference**, not a separate tab
- Players can follow the entire game from chat alone
- Combat tab (`/game/:id/combat`) available for deep tactical work
- Combat panel **collapses** so it doesn't block narrative

## Components Affected

| Component | Change |
|-----------|--------|
| `GameChatPage.tsx` | Full redesign — 3-zone layout |
| `ChatPanel.tsx` | Simplified — remove top panel controls, delegate to GameChatPage |
| `MessageBubble.tsx` | Keep as-is (already supports all types) |
| `messageStyles.ts` | Keep as-is (already has all styles) |
| `useMessages.ts` | Keep as-is (already has infinite scroll) |
| `useMessagesPaginated.ts` | Keep as-is |
| `CombatTab.tsx` | No changes (separate page) |
| `ToolCallBanner.tsx` | No changes (overlay component) |

## Files to Create/Modify

### Modified
- `src/Adnd.Client/src/pages/GameChatPage.tsx` — complete rewrite
- `src/Adnd.Client/src/components/chat/ChatPanel.tsx` — simplify (remove top panel controls)

### No changes needed
- `MessageBubble.tsx` — already handles all message types
- `messageStyles.ts` — already has all styles
- `useMessages.ts` — already has infinite scroll hook
- All backend files — no new endpoints needed
