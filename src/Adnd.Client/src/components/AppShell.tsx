import { useState, useEffect } from 'react';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { Box, Alert, AlertTitle } from '@mui/material';
import { useAuth } from '../api/authHook';
import { useGames } from '../api/gameHooks';
import SidePanel, { type AppView } from './SidePanel';
import WelcomeScreen from './WelcomeScreen';

export default function AppShell() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated } = useAuth();
  useGames();

  const [drawerOpen, setDrawerOpen] = useState(true);
  const [currentView, setCurrentView] = useState<AppView>('welcome');
  const [currentGameId, setCurrentGameId] = useState<string | undefined>(undefined);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

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
        break;
      case 'admin':
        setCurrentView('admin');
        break;
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
      </Box>
    </Box>
  );
}
