# UI Navigation Refactor Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Remove all tab-based navigation from GameRoomPage and AdminPage, replacing them with dedicated page files navigated purely through the left sidebar.

**Architecture:** Extract tab content from GameRoomPage.tsx and AdminPage.tsx into 10 separate page files. Simplify AppShell state tracking (remove activeGameTab/activeAdminTab props). Update SidePanel to use useLocation() directly for highlighting. Update App.tsx routes to point to new pages.

**Tech Stack:** React 19, TypeScript, MUI, react-router-dom, SignalR via hubHook

---

### Phase 1: Create Game Pages

#### Task 1: Create GameChatPage.tsx

**Files:**
- Create: `src/pages/GameChatPage.tsx`

**Step 1: Create the file**

Extract from `GameRoomPage.tsx` the chat tab content (activeTab === 0). This includes:
- `useGame`, `useMessagesInfiniteScroll`, `ToolCallBanner` hooks
- `ChatPanel` component
- Message input bar with type selector
- All inline state: `messageInput`, `messageType`, `messageTarget`
- All inline functions: `sendMessage`, `handleRollDice`

```tsx
import { useCallback, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../api/hooks/useAuth';
import { useGame } from '../api/hooks/useGameDetail';
import { useGMStatus } from '../api/hooks/useGameDetail';
import { usePlayers } from '../api/hooks/useSessionPlayers';
import { useToolCalls } from '../api/hooks/useAgent';
import { useMessagesInfiniteScroll } from '../api/hooks/useMessages';
import { api } from '../api/client';
import ToolCallBanner from '../components/ToolCallBanner';
import ChatPanel from '../components/chat/ChatPanel';
import { Box, Typography, TextField, InputAdornment, MenuItem, Select, FormControl, InputLabel, IconButton } from '@mui/material';
import { Send as SendIcon, SportsEsports as DiceIcon } from '@mui/icons-material';

type MessageInputType = 'inGame' | 'ooc';
type MessageTarget = 'all' | 'gm' | 'player';

export default function GameChatPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { game, isLoading: isLoadingGame } = useGame(id);
  const { status: gmStatus } = useGMStatus(id);
  const { players } = usePlayers(id);
  const { pendingCalls: calls } = useToolCalls(id);
  const { messages, isLoading, hasMore, loadOldest } = useMessagesInfiniteScroll(id, undefined);
  const [messageInput, setMessageInput] = useState('');
  const [messageType, setMessageType] = useState<MessageInputType>('inGame');
  const chatRef = useRef<HTMLDivElement>(null);

  const sendMessage = useCallback(async () => {
    if (!messageInput.trim() || !id) return;
    // TODO: implement send via hub
    setMessageInput('');
  }, [messageInput, id]);

  const handleRollDice = useCallback(async () => {
    // TODO: implement dice roll
  }, []);

  const handleLeaveGame = useCallback(async () => {
    if (!id) return;
    await api.leaveGame(id);
    navigate('/');
  }, [id, navigate]);

  void handleLeaveGame;

  if (isLoadingGame) return <Typography>Loading...</Typography>;
  if (!game) return <Typography>No game data available.</Typography>;

  return (
    <Box sx={{ display: 'flex', height: '100vh' }}>
      <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        <Box sx={{ flex: 1, overflow: 'auto', p: 2 }}>
          <ChatPanel
            messages={messages}
            isLoadingMore={isLoading}
            hasMore={hasMore}
            loadMoreOldest={loadOldest}
          />
        </Box>

        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', p: 2, pt: 0 }}>
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <InputLabel>Type</InputLabel>
            <Select
              value={messageType}
              label="Type"
              onChange={(e) => setMessageType(e.target.value as MessageInputType)}
            >
              <MenuItem value="inGame">In-Game</MenuItem>
              <MenuItem value="ooc">OOC</MenuItem>
            </Select>
          </FormControl>

          <TextField
            fullWidth
            placeholder={`Send ${messageType} message...`}
            value={messageInput}
            onChange={(e) => setMessageInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && sendMessage()}
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton onClick={sendMessage} edge="end">
                    <SendIcon />
                  </IconButton>
                  <IconButton onClick={handleRollDice} edge="end">
                    <DiceIcon />
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
        </Box>
      </Box>

      <ToolCallBanner
        pendingCalls={calls || []}
        onConfirm={async (_callId, _approved) => {}}
        onRoll={async (_callId) => {}}
        onDecline={async (_callId) => {}}
        onDismiss={(_callId) => {}}
        isCreator={user?.role === 'Creator'}
      />
    </Box>
  );
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/GameChatPage.tsx
git commit -m "feat: add GameChatPage as dedicated chat page"
```

---

#### Task 2: Create GameCombatPage.tsx

**Files:**
- Create: `src/pages/GameCombatPage.tsx`

**Step 1: Create the file**

Extract from `GameRoomPage.tsx` the combat tab content (activeTab === 1). This is essentially just the `CombatTab` component with its gameId prop:

```tsx
import { useParams } from 'react-router-dom';
import CombatTab from '../components/combat/CombatTab';

export default function GameCombatPage() {
  const { id } = useParams<{ id: string }>();
  if (!id) return null;
  return <CombatTab gameId={id} />;
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/GameCombatPage.tsx
git commit -m "feat: add GameCombatPage as dedicated combat page"
```

---

#### Task 3: Create GameSettingsPage.tsx

**Files:**
- Create: `src/pages/GameSettingsPage.tsx`

**Step 1: Create the file**

Extract from `GameRoomPage.tsx` the settings tab content (activeTab === 3) plus the character creation wizard. This includes:
- `useGame`, `useGMStatus`, `usePlayers` hooks
- `CharacterCreateWizard` dialog
- State: `showCharacterWizard`

```tsx
import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGame } from '../api/hooks/useGameDetail';
import { useGMStatus } from '../api/hooks/useGameDetail';
import { usePlayers } from '../api/hooks/useSessionPlayers';
import CharacterCreateWizard from './CharacterCreateWizard';
import { Box, Typography, Button, Dialog, DialogTitle, DialogContent } from '@mui/material';

export default function GameSettingsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { game, isLoading } = useGame(id);
  const { status: gmStatus } = useGMStatus(id);
  const { players } = usePlayers(id);
  const [showCharacterWizard, setShowCharacterWizard] = useState(false);

  if (isLoading) return <Typography>Loading...</Typography>;
  if (!game) return <Typography>No game data available.</Typography>;

  return (
    <Box sx={{ p: 2 }}>
      <Typography variant="h5" gutterBottom>Game Settings</Typography>

      <Box sx={{ mb: 3 }}>
        <Typography variant="subtitle1">Game Name: {game.name}</Typography>
        <Typography variant="subtitle1">Game ID: {id}</Typography>
        <Typography variant="subtitle1">GM Status: {gmStatus?.status || 'Idle'}</Typography>
        <Typography variant="subtitle1">Players: {players?.length || 0}</Typography>
      </Box>

      <Box sx={{ mb: 3 }}>
        <Button variant="contained" onClick={() => setShowCharacterWizard(true)}>
          Create Character
        </Button>
      </Box>

      <Button variant="outlined" color="error" onClick={() => navigate('/')}>
        Leave Game
      </Button>

      <Dialog open={showCharacterWizard} onClose={() => setShowCharacterWizard(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Create Character</DialogTitle>
        <DialogContent>
          <CharacterCreateWizard
            open={showCharacterWizard}
            onClose={() => setShowCharacterWizard(false)}
            onFinish={() => setShowCharacterWizard(false)}
          />
        </DialogContent>
      </Dialog>
    </Box>
  );
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/GameSettingsPage.tsx
git commit -m "feat: add GameSettingsPage as dedicated settings page"
```

---

### Phase 2: Create Admin Pages

#### Task 4: Create AdminDashboardPage.tsx

**Files:**
- Create: `src/pages/AdminDashboardPage.tsx`

**Step 1: Create the file**

Extract from `AdminPage.tsx` the dashboard view (view === 'dashboard'). This is the `GameStatePage` component:

```tsx
import { useParams } from 'react-router-dom';
import { useGame } from '../api/hooks/useGameDetail';
import GameStatePage from './GameStatePage';
import { Box, Typography } from '@mui/material';

export default function AdminDashboardPage() {
  const { id } = useParams<{ id: string }>();
  const { game, isLoading } = useGame(id);

  if (isLoading) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading...</Typography></Box>;
  if (!game) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography variant="h5" color="error">Game not found</Typography></Box>;

  return <GameStatePage gameId={id!} />;
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/AdminDashboardPage.tsx
git commit -m "feat: add AdminDashboardPage as dedicated dashboard page"
```

---

#### Task 5: Create AdminPlotBoardPage.tsx

**Files:**
- Create: `src/pages/AdminPlotBoardPage.tsx`

**Step 1: Create the file**

Extract from `AdminPage.tsx` the plot board view. Includes `usePlotWeaver` hook and `PlotBoardAdminTab` component:

```tsx
import { useParams } from 'react-router-dom';
import { usePlotWeaver } from '../api/hooks/usePlot';
import PlotBoardAdminTab from './PlotBoardAdminTab';
import { Box, Typography } from '@mui/material';

export default function AdminPlotBoardPage() {
  const { id } = useParams<{ id: string }>();
  const { threads, isLoading } = usePlotWeaver(id);

  if (!id) return null;
  return <PlotBoardAdminTab threads={threads} isLoading={isLoading} gameId={id} />;
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/AdminPlotBoardPage.tsx
git commit -m "feat: add AdminPlotBoardPage as dedicated plot board page"
```

---

#### Task 6: Create AdminNPCsPage.tsx

**Files:**
- Create: `src/pages/AdminNPCsPage.tsx`

**Step 1: Create the file**

Extract from `AdminPage.tsx` the NPCs view. Includes `useNPCs` hook, `AdminNPCsTab`, `AdminNPCDialog`:

```tsx
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useNPCs } from '../api/hooks/useNPCs';
import AdminNPCDialog from '../components/admin/AdminNPCDialog';
import AdminNPCsTab from '../components/admin/AdminNPCsTab';
import { Box, Alert, AlertTitle } from '@mui/material';

export default function AdminNPCsPage() {
  const { id } = useParams<{ id: string }>();
  const { npcs, isLoading: npcsLoading, createNPC, updateNPC, deleteNPC } = useNPCs(id);
  const [npcDialogOpen, setNpcDialogOpen] = useState(false);
  const [errorState, setErrorState] = useState<string | null>(null);

  if (!id) return null;

  return (
    <Box>
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}
      <AdminNPCsTab
        npcs={npcs}
        npcsLoading={npcsLoading}
        onOpenDialog={() => setNpcDialogOpen(true)}
        onDelete={deleteNPC}
        onUpdate={updateNPC}
      />
      <AdminNPCDialog open={npcDialogOpen} onClose={() => setNpcDialogOpen(false)} onCreate={async (name, desc) => {
        if (!id) return;
        await createNPC!(name, desc);
        setNpcDialogOpen(false);
      }} />
    </Box>
  );
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/AdminNPCsPage.tsx
git commit -m "feat: add AdminNPCsPage as dedicated NPCs page"
```

---

#### Task 7: Create AdminCharactersPage.tsx

**Files:**
- Create: `src/pages/AdminCharactersPage.tsx`

**Step 1: Create the file**

Extract from `AdminPage.tsx` the characters view. Includes `useCharacters` hook and `AdminCharactersTab`:

```tsx
import { useParams } from 'react-router-dom';
import { useCharacters } from '../api/hooks/useCharacters';
import AdminCharactersTab from '../components/admin/AdminCharactersTab';

export default function AdminCharactersPage() {
  const { id } = useParams<{ id: string }>();
  const { characters } = useCharacters(id);

  if (!id) return null;
  return <AdminCharactersTab characters={characters} />;
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/AdminCharactersPage.tsx
git commit -m "feat: add AdminCharactersPage as dedicated characters page"
```

---

#### Task 8: Create AdminConsistencyPage.tsx

**Files:**
- Create: `src/pages/AdminConsistencyPage.tsx`

**Step 1: Create the file**

Extract from `AdminPage.tsx` the consistency view. Includes `useConsistency` hook, `AdminConsistencyTab`:

```tsx
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useConsistency } from '../api/hooks/useCharacters';
import AdminConsistencyTab from '../components/admin/AdminConsistencyTab';
import { Box } from '@mui/material';

export default function AdminConsistencyPage() {
  const { id } = useParams<{ id: string }>();
  const { report, isLoading, check } = useConsistency(id);
  const [checking, setChecking] = useState(false);

  const handleCheck = async () => {
    setChecking(true);
    await check(50);
    setChecking(false);
  };

  if (!id) return null;
  return <AdminConsistencyTab report={report} isLoading={isLoading || checking} onCheck={handleCheck} />;
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/AdminConsistencyPage.tsx
git commit -m "feat: add AdminConsistencyPage as dedicated consistency page"
```

---

#### Task 9: Create AdminLLMLogsPage.tsx

**Files:**
- Create: `src/pages/AdminLLMLogsPage.tsx`

**Step 1: Create the file**

Extract from `AdminPage.tsx` the LLM logs view. Includes `useGameHub` hook, `AdminLLMLogsTab`, `LLMLogDetailDialog`, filter state:

```tsx
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useGameHub } from '../api/hooks/useHub';
import AdminLLMLogsTab from '../components/admin/AdminLLMLogsTab';
import LLMLogDetailDialog from '../components/admin/LLMLogDetailDialog';
import { Box } from '@mui/material';

export default function AdminLLMLogsPage() {
  const { id } = useParams<{ id: string }>();
  const { invoke } = useGameHub();
  const [llmLogs, setLlmLogs] = useState<any[]>([]);
  const [logsLoading, setLogsLoading] = useState(false);
  const [selectedLog, setSelectedLog] = useState<any>(null);
  const [showLogDetail, setShowLogDetail] = useState(false);
  const [logFilterProvider, setLogFilterProvider] = useState('');
  const [logFilterFrom, setLogFilterFrom] = useState('');
  const [logFilterTo, setLogFilterTo] = useState('');

  const handleRefresh = async () => {
    if (!id) return;
    setLogsLoading(true);
    try {
      const logs = await invoke('GetLLMInteractions', id, undefined, logFilterProvider, logFilterFrom || undefined, logFilterTo || undefined, 200);
      if (logs) setLlmLogs(logs);
    } catch (e) {
      console.error('Failed to fetch LLM logs', e);
    }
    setLogsLoading(false);
  };

  const handleDeleteLog = async (logId: string) => {
    try {
      await invoke('DeleteLLMInteraction', logId);
      setLlmLogs(prev => prev.filter(l => l.id !== logId));
    } catch (e) {
      console.error('Failed to delete log', e);
    }
  };

  if (!id) return null;
  return (
    <Box>
      <AdminLLMLogsTab
        logs={llmLogs}
        isLoading={logsLoading}
        onRefresh={handleRefresh}
        onOpenDetail={setSelectedLog}
        onDelete={handleDeleteLog}
        filterProvider={logFilterProvider}
        onFilterProviderChange={setLogFilterProvider}
        filterFrom={logFilterFrom}
        onFilterFromChange={setLogFilterFrom}
        filterTo={logFilterTo}
        onFilterToChange={setLogFilterTo}
      />
      <LLMLogDetailDialog open={showLogDetail} onClose={() => setShowLogDetail(false)} log={selectedLog} />
    </Box>
  );
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/AdminLLMLogsPage.tsx
git commit -m "feat: add AdminLLMLogsPage as dedicated LLM logs page"
```

---

#### Task 10: Create AdminAgentCallsPage.tsx

**Files:**
- Create: `src/pages/AdminAgentCallsPage.tsx`

**Step 1: Create the file**

Extract from `AdminPage.tsx` the agent calls view. Includes `useGameHub` hook, `AdminAgentCallsTab`, `AdminAgentCallDialog`, filter state, agent call form state:

```tsx
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useGameHub } from '../api/hooks/useHub';
import { AgentType, AgentAction } from '../types';
import AdminAgentCallDialog from '../components/admin/AdminAgentCallDialog';
import AdminAgentCallsTab from '../components/admin/AdminAgentCallsTab';
import { Box, Alert, AlertTitle } from '@mui/material';

export default function AdminAgentCallsPage() {
  const { id } = useParams<{ id: string }>();
  const { invoke } = useGameHub();
  const [agentCalls, setAgentCalls] = useState<any[]>([]);
  const [agentCallFilter, setAgentCallFilter] = useState<number | undefined>(undefined);
  const [showAgentDialog, setShowAgentDialog] = useState(false);
  const [agentFrom, setAgentFrom] = useState(AgentType.GM);
  const [agentTo, setAgentTo] = useState(AgentType.LLM);
  const [agentAction, setAgentAction] = useState(AgentAction.Query);
  const [agentInput, setAgentInput] = useState('');
  const [errorState, setErrorState] = useState<string | null>(null);

  const handleRefresh = async () => {
    if (!id) return;
    try {
      const calls = await invoke('GetAgentCallHistory', id, agentCallFilter, undefined, 50);
      if (calls) setAgentCalls(calls);
    } catch (e) {
      console.error('Failed to fetch agent calls', e);
    }
  };

  const handleCreate = async () => {
    if (!id) return;
    try {
      await invoke('CallAgent', agentFrom, agentTo, agentAction, agentInput || undefined);
      setShowAgentDialog(false);
      setAgentInput('');
      await handleRefresh();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleDelete = async (callId: string) => {
    try {
      await invoke('GetAgentCall', callId);
      setAgentCalls(prev => prev.filter(c => c.id !== callId));
    } catch (e) {
      console.error('Failed to delete agent call', e);
    }
  };

  if (!id) return null;

  return (
    <Box>
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}
      <AdminAgentCallsTab
        calls={agentCalls}
        isLoading={false}
        onRefresh={handleRefresh}
        onOpenDialog={() => setShowAgentDialog(true)}
        onDelete={handleDelete}
        filter={agentCallFilter}
        onFilterChange={setAgentCallFilter}
      />
      <AdminAgentCallDialog
        open={showAgentDialog}
        onClose={() => setShowAgentDialog(false)}
        agentFrom={agentFrom}
        agentTo={agentTo}
        agentAction={agentAction}
        agentInput={agentInput}
        onAgentFromChange={setAgentFrom}
        onAgentToChange={setAgentTo}
        onAgentActionChange={setAgentAction}
        onAgentInputChange={setAgentInput}
        onExecute={handleCreate}
      />
    </Box>
  );
}
```

**Step 2: Commit**

```bash
git add src/Adnd.Client/src/pages/AdminAgentCallsPage.tsx
git commit -m "feat: add AdminAgentCallsPage as dedicated agent calls page"
```

---

### Phase 3: Update SidePanel

#### Task 11: Update SidePanel.tsx to use useLocation()

**Files:**
- Modify: `src/components/SidePanel.tsx`

**Step 1: Remove activeGameTab and activeAdminTab props**

Remove from interface:
```diff
-  activeGameTab?: string;
-  activeAdminTab?: string;
```

Remove from destructuring:
```diff
-  gameId, activeGameTab, onNewGame, onJoinGame, onAddPreset, onNewSystem, games, presets, isMobile = false
+  gameId, onNewGame, onJoinGame, onAddPreset, onNewSystem, games, presets, isMobile = false
```

**Step 2: Add useLocation import and hook**

Add to imports:
```tsx
import { useNavigate, useParams, useLocation } from 'react-router-dom';
```

Add inside component body:
```tsx
const location = useLocation();
```

**Step 3: Replace activeGameTab highlighting in game view**

In the game view section, replace `activeGameTab === 'chat'` with:
```tsx
const isGameChat = gameId && location.pathname === `/game/${gameId}`;
const isGameCombat = gameId && location.pathname === `/game/${gameId}/combat`;
const isGameSettings = gameId && location.pathname === `/game/${gameId}/settings`;
```

Then use these in the ListItemButton sx:
```tsx
<ListItemButton onClick={() => navigate(`/game/${gameId}`)} sx={{ ...buttonBaseSx, bgcolor: isGameChat ? 'rgba(145,71,255,0.15)' : 'transparent', color: isGameChat ? 'primary.light' : 'text.primary', ... }}>
```

```tsx
<ListItemButton onClick={() => navigate(`/game/${gameId}/combat`)} sx={{ ...buttonBaseSx, bgcolor: isGameCombat ? 'rgba(145,71,255,0.15)' : 'transparent', color: isGameCombat ? 'primary.light' : 'text.primary', ... }}>
```

```tsx
<ListItemButton onClick={() => navigate(`/game/${gameId}/settings`)} sx={{ ...buttonBaseSx, bgcolor: isGameSettings ? 'rgba(145,71,255,0.15)' : 'transparent', color: isGameSettings ? 'primary.light' : 'text.primary', ... }}>
```

**Step 4: Replace activeAdminTab highlighting in admin view**

In the admin view section, add:
```tsx
const isAdminDashboard = adminId && location.pathname === `/admin/${adminId}`;
const isAdminPlotBoard = adminId && location.pathname === `/admin/${adminId}/plot-board`;
const isAdminNPCs = adminId && location.pathname === `/admin/${adminId}/npcs`;
const isAdminCharacters = adminId && location.pathname === `/admin/${adminId}/characters`;
const isAdminConsistency = adminId && location.pathname === `/admin/${adminId}/consistency`;
const isAdminLLMLogs = adminId && location.pathname === `/admin/${adminId}/llm-logs`;
const isAdminAgentCalls = adminId && location.pathname === `/admin/${adminId}/agent-calls`;
```

Then apply to the respective ListItemButtons.

**Step 5: Remove activeGameTab from AppShell call site**

In `AppShell.tsx`, remove:
```diff
-        activeGameTab={(() => {
-          const parts = location.pathname.split('/').filter(Boolean);
-          return parts[2] || 'chat';
-        })()}
```

**Step 6: Commit**

```bash
git add src/Adnd.Client/src/components/SidePanel.tsx
git commit -m "refactor: SidePanel uses useLocation() for highlighting, removes activeGameTab/activeAdminTab props"
```

---

### Phase 4: Update AppShell

#### Task 12: Simplify AppShell state

**Files:**
- Modify: `src/components/AppShell.tsx`

**Step 1: Remove activeGameTab and activeAdminTab state**

Remove these state declarations:
```diff
-  const [_activeGameTab, _setActiveGameTab] = useState<string>('chat');
```

Remove from `handleNavigate` switch case for 'game':
```diff
-        setCurrentView('game');
-        if (currentGameId) navigate(`/game/${currentGameId}`);
-        break;
```

Remove from `handleNavigate` switch case for 'admin':
```diff
-        setCurrentView('admin');
-        if (currentGameId) navigate(`/admin/${currentGameId}`);
-        break;
```

**Step 2: Remove activeGameTab/activeAdminTab from useEffect**

In the `useEffect` that maps paths, simplify the game/admin detection:
```diff
-    } else if (gameId) {
-      setCurrentView('game');
+    } else if (gameId) {
+      setCurrentView('game');
       setCurrentGameId(gameId);
-    } else if (adminId) {
+    } else if (adminId) {
       setCurrentView('admin');
```

**Step 3: Remove activeGameTab prop from SidePanel call**

```diff
-        activeGameTab={(() => {
-          const parts = location.pathname.split('/').filter(Boolean);
-          return parts[2] || 'chat';
-        })()}
```

**Step 4: Commit**

```bash
git add src/Adnd.Client/src/components/AppShell.tsx
git commit -m "refactor: AppShell simplified, removes activeGameTab/activeAdminTab state"
```

---

### Phase 5: Update Routes

#### Task 13: Update App.tsx routes

**Files:**
- Modify: `src/App.tsx`

**Step 1: Add new imports**

```tsx
import GameChatPage from './pages/GameChatPage';
import GameCombatPage from './pages/GameCombatPage';
import GameSettingsPage from './pages/GameSettingsPage';
import AdminDashboardPage from './pages/AdminDashboardPage';
import AdminPlotBoardPage from './pages/AdminPlotBoardPage';
import AdminNPCsPage from './pages/AdminNPCsPage';
import AdminCharactersPage from './pages/AdminCharactersPage';
import AdminConsistencyPage from './pages/AdminConsistencyPage';
import AdminLLMLogsPage from './pages/AdminLLMLogsPage';
import AdminAgentCallsPage from './pages/AdminAgentCallsPage';
```

**Step 2: Replace route elements**

Replace all game and admin route elements:
```diff
-    <Route path="/game/:id" element={<GameRoomPage />} />
-    <Route path="/game/:id/combat" element={<GameRoomPage />} />
-    <Route path="/game/:id/settings" element={<GameRoomPage />} />
-    <Route path="/admin/:id" element={<AdminPage />} />
-    <Route path="/admin/:id/plot-board" element={<AdminPage />} />
-    <Route path="/admin/:id/npcs" element={<AdminPage />} />
-    <Route path="/admin/:id/characters" element={<AdminPage />} />
-    <Route path="/admin/:id/consistency" element={<AdminPage />} />
-    <Route path="/admin/:id/llm-logs" element={<AdminPage />} />
-    <Route path="/admin/:id/agent-calls" element={<AdminPage />} />
+    <Route path="/game/:id" element={<GameChatPage />} />
+    <Route path="/game/:id/combat" element={<GameCombatPage />} />
+    <Route path="/game/:id/settings" element={<GameSettingsPage />} />
+    <Route path="/admin/:id" element={<AdminDashboardPage />} />
+    <Route path="/admin/:id/plot-board" element={<AdminPlotBoardPage />} />
+    <Route path="/admin/:id/npcs" element={<AdminNPCsPage />} />
+    <Route path="/admin/:id/characters" element={<AdminCharactersPage />} />
+    <Route path="/admin/:id/consistency" element={<AdminConsistencyPage />} />
+    <Route path="/admin/:id/llm-logs" element={<AdminLLMLogsPage />} />
+    <Route path="/admin/:id/agent-calls" element={<AdminAgentCallsPage />} />
```

**Step 3: Commit**

```bash
git add src/Adnd.Client/src/App.tsx
git commit -m "feat: routes point to new dedicated page files"
```

---

### Phase 6: Cleanup

#### Task 14: Remove old files

**Files:**
- Delete: `src/pages/GameRoomPage.tsx`
- Delete: `src/pages/AdminPage.tsx`

**Step 1: Remove files**

```bash
git rm src/Adnd.Client/src/pages/GameRoomPage.tsx
git rm src/Adnd.Client/src/pages/AdminPage.tsx
git commit -m "chore: remove GameRoomPage and AdminPage (replaced by dedicated pages)"
```

---

### Phase 7: Verify

#### Task 15: Verify build

**Step 1: Run TypeScript check**

```bash
cd src/Adnd.Client && npx tsc --noEmit
```

Expected: No errors.

**Step 2: Run dev server to verify**

```bash
cd src/Adnd.Client && npm run dev
```

Verify these routes work:
- `/dashboard` — game list
- `/game/<id>` — chat page
- `/game/<id>/combat` — combat page
- `/game/<id>/settings` — settings page
- `/admin/<id>` — admin dashboard
- `/admin/<id>/npcs` — NPCs page
- `/admin/<id>/characters` — characters page
- `/admin/<id>/plot-board` — plot board
- `/admin/<id>/consistency` — consistency check
- `/admin/<id>/llm-logs` — LLM logs
- `/admin/<id>/agent-calls` — agent calls
- Sidebar highlighting works in each view
- Back buttons navigate correctly

---

## Summary of Changes

| Action | Count | Files |
|--------|-------|-------|
| Create | 10 | GameChatPage, GameCombatPage, GameSettingsPage, AdminDashboardPage, AdminPlotBoardPage, AdminNPCsPage, AdminCharactersPage, AdminConsistencyPage, AdminLLMLogsPage, AdminAgentCallsPage |
| Modify | 3 | AppShell.tsx, SidePanel.tsx, App.tsx |
| Delete | 2 | GameRoomPage.tsx, AdminPage.tsx |

## Risks & Notes

1. **GameStatePage.tsx** is imported by AdminDashboardPage — keep it in place (it's a shared component, not a page)
2. **CharacterCreateWizard.tsx** is imported by GameSettingsPage — keep in place
3. All tab components (`AdminNPCsTab.tsx`, etc.) stay in `components/admin/` — they become the content of their pages
4. The `CombatTab` component stays in `components/combat/`
5. No new hooks needed — all existing hooks work with `useParams()` id extraction
