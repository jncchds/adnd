import { useState, useEffect } from 'react';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { Alert, AlertTitle, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Button } from '@mui/material';
import { useAuth } from '../api/hooks/useAuth';
import { useGames } from '../api/hooks/useGame';
import SidePanel, { type AppView } from './SidePanel';
import WelcomeScreen from './WelcomeScreen';

const SIDEBAR_KEY = 'adnd-sidebar';
const isMobileQuery = () => window.innerWidth < 900;

export default function AppShell() {
  const { isAuthenticated } = useAuth();
  const [isMobile, setIsMobile] = useState(isMobileQuery());
  const [sidebarOpen, setSidebarOpen] = useState(() => {
    const stored = localStorage.getItem(SIDEBAR_KEY);
    if (stored !== null) return stored === 'true';
    return !isMobileQuery();
  });

  useEffect(() => {
    const handler = () => setIsMobile(isMobileQuery());
    window.addEventListener('resize', handler);
    return () => window.removeEventListener('resize', handler);
  }, []);

  const handleToggle = () => {
    setSidebarOpen(prev => {
      const next = !prev;
      localStorage.setItem(SIDEBAR_KEY, String(next));
      return next;
    });
  };

  // Not authenticated — no data hooks (avoids 401s)
  if (!isAuthenticated) {
    return (
      <div className="app-layout">
        <SidePanel
          open={sidebarOpen}
          onToggle={handleToggle}
          currentView="welcome"
          onNavigate={() => {}}
          games={[]}
          presets={[]}
          isMobile={isMobile}
        />
        <main className="app-main" style={{ padding: '1.5rem 2rem' }}>
          <WelcomeScreen />
        </main>
      </div>
    );
  }

  // Authenticated — load data hooks (only when needed)
  return <AuthenticatedShell isMobile={isMobile} />;
}

// ==================== Authenticated sub-component ====================
function AuthenticatedShell({ isMobile }: { isMobile: boolean }) {
  const navigate = useNavigate();
  const location = useLocation();
  const { joinByCode } = useGames();

  const [drawerOpen, setDrawerOpen] = useState(() => {
    const stored = localStorage.getItem(SIDEBAR_KEY);
    if (stored !== null) return stored === 'true';
    return !isMobile;
  });
  const [currentView, setCurrentView] = useState<AppView>('dashboard');
  const [currentGameId, setCurrentGameId] = useState<string | undefined>(undefined);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const [joinDialogOpen, setJoinDialogOpen] = useState(false);
  const [joinCode, setJoinCode] = useState('');

  // Persist sidebar state
  const handleToggle = () => {
    setDrawerOpen(prev => {
      const next = !prev;
      localStorage.setItem(SIDEBAR_KEY, String(next));
      return next;
    });
  };

  // Auto-close on mobile when navigating
  useEffect(() => {
    if (isMobile && drawerOpen) {
      setDrawerOpen(false);
      localStorage.setItem(SIDEBAR_KEY, 'false');
    }
  }, [location.pathname, isMobile]);

  // Determine current view from URL
  useEffect(() => {
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
    } else if (path === '/user-settings') {
      setCurrentView('user-settings');
      setCurrentGameId(undefined);
    } else if (gameId) {
      setCurrentView('game');
      setCurrentGameId(gameId);
    } else if (adminId) {
      setCurrentView('admin');
      setCurrentGameId(adminId);
    }
  }, [location.pathname]);

  const handleNavigate = (view: AppView) => {
    switch (view) {
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
      case 'user-settings':
        setCurrentView('user-settings');
        setCurrentGameId(undefined);
        navigate('/user-settings');
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
  const handleNewGame = () => navigate('/dashboard', { state: { openCreate: true } });
  const handleJoinGame = () => setJoinDialogOpen(true);
  const handleAddPreset = () => navigate('/llm-presets/new');
  const handleNewSystem = () => navigate('/systems/new');

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

  return (
    <div className="app-layout">
      <SidePanel
        open={drawerOpen}
        onToggle={handleToggle}
        currentView={currentView}
        onNavigate={handleNavigate}
        gameId={currentGameId}
        onNewGame={currentView === 'dashboard' ? handleNewGame : undefined}
        onJoinGame={currentView === 'dashboard' ? handleJoinGame : undefined}
        onAddPreset={currentView === 'llm-presets' ? handleAddPreset : undefined}
        onNewSystem={currentView === 'systems' ? handleNewSystem : undefined}
        isMobile={isMobile}
      />
      <main className="app-main" style={{ padding: isMobile ? '1rem' : '1.5rem 2rem' }}>
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
      </main>
    </div>
  );
}
