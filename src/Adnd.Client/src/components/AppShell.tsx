import { useState, useEffect } from 'react';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { Box, Alert, AlertTitle, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Button } from '@mui/material';
import { useAuth } from '../api/authHook';
import { useGames, useLLMPresets } from '../api/gameHooks';
import SidePanel, { type AppView } from './SidePanel';
import WelcomeScreen from './WelcomeScreen';

export default function AppShell() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated } = useAuth();
  const { createGame, joinByCode } = useGames();

  const [drawerOpen, setDrawerOpen] = useState(true);
  const [currentView, setCurrentView] = useState<AppView>('welcome');
  const [currentGameId, setCurrentGameId] = useState<string | undefined>(undefined);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [joinDialogOpen, setJoinDialogOpen] = useState(false);
  const [gameName, setGameName] = useState('');
  const [systemId, setSystemId] = useState('dnd5e');
  const [llmPresetId, setLLMPresetId] = useState<string | null>(null);
  const [plotSeed, setPlotSeed] = useState('');
  const [gameParameters, setGameParameters] = useState('');
  const [joinCode, setJoinCode] = useState('');
  const { presets } = useLLMPresets();

  // Determine current view from URL
  useEffect(() => {
    if (!isAuthenticated) {
      setCurrentView('welcome');
      return;
    }

    const path = location.pathname;
    const gameId = path.match(/\/game\/([a-f0-9-]+)/)?.[1];
    const adminId = path.match(/\/admin\/([a-f0-9-]+)/)?.[1];

    if (path === '/dashboard' || path === '/') {
      setCurrentView('dashboard');
      setCurrentGameId(undefined);
    } else if (path === '/llm-presets' || path.startsWith('/llm-presets/')) {
      setCurrentView('llm-presets');
      setCurrentGameId(undefined);
    } else if (path === '/systems' || path.startsWith('/systems/')) {
      setCurrentView('systems');
      setCurrentGameId(undefined);
    } else if (path === '/login' || path === '/register') {
      setCurrentView(currentView);
    } else if (gameId) {
      setCurrentView('game');
      setCurrentGameId(gameId);
    } else if (adminId) {
      setCurrentView('admin');
      setCurrentGameId(adminId);
    }
  }, [location.pathname, isAuthenticated]);

  const handleNavigate = (view: AppView) => {
    switch (view) {
      case 'welcome':
        setCurrentView('welcome');
        break;
      case 'dashboard':
        setCurrentView('dashboard');
        setCurrentGameId(undefined);
        navigate('/dashboard');
        break;
      case 'llm-presets':
        setCurrentView('llm-presets');
        setCurrentGameId(undefined);
        navigate('/llm-presets');
        break;
      case 'systems':
        setCurrentView('systems');
        setCurrentGameId(undefined);
        navigate('/systems');
        break;
      case 'game':
        setCurrentView('game');
        if (currentGameId) navigate(`/game/${currentGameId}`);
        break;
      case 'admin':
        setCurrentView('admin');
        if (currentGameId) navigate(`/admin/${currentGameId}`);
        break;
    }
  };

  // Action handlers for sidebar buttons
  const handleNewGame = () => setCreateDialogOpen(true);
  const handleJoinGame = () => setJoinDialogOpen(true);
  const handleAddPreset = () => navigate('/llm-presets/new');
  const handleNewSystem = () => navigate('/systems/new');

  const handleCreateGame = async () => {
    if (!gameName.trim()) return;
    setActionError(null);
    setActionSuccess(null);
    try {
      await createGame(gameName, systemId, undefined, undefined, llmPresetId || undefined, plotSeed || undefined, gameParameters || undefined);
      setCreateDialogOpen(false);
      setGameName('');
      setSystemId('dnd5e');
      setLLMPresetId(null);
      setPlotSeed('');
      setGameParameters('');
      setActionSuccess('Game created!');
      setTimeout(() => setActionSuccess(null), 2000);
    } catch (e: any) {
      setActionError(e.message);
    }
  };

  const handleJoinGameAPI = async () => {
    setActionError(null);
    setActionSuccess(null);
    try {
      const result = await joinByCode(joinCode) as any;
      setJoinDialogOpen(false);
      setJoinCode('');
      setActionSuccess('Joined game!');
      setTimeout(() => navigate(`/game/${result.gameId}`), 500);
    } catch (e: any) {
      setActionError(e.message);
    }
  };





  // ==================== WELCOME VIEW ====================
  if (!isAuthenticated) {
    return (
      <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
        <SidePanel
          open={drawerOpen}
          onToggle={() => setDrawerOpen(!drawerOpen)}
          currentView="welcome"
          onNavigate={handleNavigate}
        />
        <Box component="main" sx={{
          flexGrow: 1,
          minWidth: 0,
          overflow: 'auto',
          bgcolor: 'background.default',
        }}>
          <WelcomeScreen />
        </Box>
      </Box>
    );
  }

  // ==================== LAYOUT WRAPPER ====================
  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <SidePanel
        open={drawerOpen}
        onToggle={() => setDrawerOpen(!drawerOpen)}
        currentView={currentView}
        onNavigate={handleNavigate}
        gameId={currentGameId}
        activeGameTab={window.location.hash.replace('#', '') || 'chat'}
        onNewGame={currentView === 'dashboard' ? handleNewGame : undefined}
        onJoinGame={currentView === 'dashboard' ? handleJoinGame : undefined}
        onAddPreset={currentView === 'llm-presets' ? handleAddPreset : undefined}
        onNewSystem={currentView === 'systems' ? handleNewSystem : undefined}
      />
      <Box component="main" sx={{
        flexGrow: 1,
        minWidth: 0,
        p: { xs: 2, md: 3 },
        overflow: 'auto',
        bgcolor: 'background.default',
      }}>
        {/* Success/Error messages */}
        {actionSuccess && (
          <Alert severity="success" onClose={() => setActionSuccess(null)} sx={{ mb: 2, alignItems: 'center' }}>
            {actionSuccess}
          </Alert>
        )}
        {actionError && (
          <Alert severity="error" onClose={() => setActionError(null)} sx={{ mb: 2 }}>
            <AlertTitle>Error</AlertTitle>
            {actionError}
          </Alert>
        )}
        <Outlet />

        {/* Create Game Dialog */}
        <Dialog open={createDialogOpen} onClose={() => setCreateDialogOpen(false)} maxWidth="sm" fullWidth>
          <DialogTitle>Create New Game</DialogTitle>
          <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField fullWidth label="Game Name" value={gameName} onChange={e => setGameName(e.target.value)} autoFocus />
            <TextField fullWidth select label="System" value={systemId} onChange={e => setSystemId(e.target.value)}>
              <option value="dnd5e">D&D 5th Edition</option>
              <option value="pf2e">Pathfinder 2nd Edition</option>
              <option value="coc7e">Call of Cthulhu 7th Edition</option>
            </TextField>
            <TextField fullWidth select label="LLM Preset (for AI-GM)" value={llmPresetId || ''} onChange={e => setLLMPresetId(e.target.value || null)}>
              <option value="">None</option>
              {presets?.map(p => (
                <option key={p.id} value={p.id}>{p.name} ({p.providerType})</option>
              ))}
            </TextField>
            <TextField fullWidth multiline rows={3} label="Plot Seed" value={plotSeed} onChange={e => setPlotSeed(e.target.value)} placeholder="Describe the initial story, setting, and tone..." />
            <TextField fullWidth multiline rows={2} label="Game Parameters" value={gameParameters} onChange={e => setGameParameters(e.target.value)} placeholder="e.g., Dark tone, medium difficulty, fast-paced..." />
            {actionError && <Alert severity="error" onClose={() => setActionError(null)}><AlertTitle>Error</AlertTitle>{actionError}</Alert>}
            {actionSuccess && <Alert severity="success" onClose={() => setActionSuccess(null)}>{actionSuccess}</Alert>}
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setCreateDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleCreateGame} variant="contained" disabled={!gameName.trim()}>Create</Button>
          </DialogActions>
        </Dialog>

        {/* Join Game Dialog */}
        <Dialog open={joinDialogOpen} onClose={() => setJoinDialogOpen(false)} maxWidth="sm" fullWidth>
          <DialogTitle>Join a Game</DialogTitle>
          <DialogContent sx={{ mt: 1 }}>
            <TextField fullWidth label="Invite Code" value={joinCode} onChange={e => setJoinCode(e.target.value)} placeholder="Enter the invite code" autoFocus />
            {actionError && <Alert severity="error" onClose={() => setActionError(null)} sx={{ mt: 2 }}><AlertTitle>Error</AlertTitle>{actionError}</Alert>}
            {actionSuccess && <Alert severity="success" onClose={() => setActionSuccess(null)} sx={{ mt: 2 }}>{actionSuccess}</Alert>}
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setJoinDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleJoinGameAPI} variant="contained" disabled={!joinCode.trim()}>Join</Button>
          </DialogActions>
        </Dialog>
      </Box>
    </Box>
  );
}
