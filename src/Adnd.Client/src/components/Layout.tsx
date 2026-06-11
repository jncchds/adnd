import { useState } from 'react';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import {
  Box,
  Drawer,
  Typography,
  IconButton,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Divider,
  useTheme,
  Avatar,
} from '@mui/material';
import {
  Menu as MenuIcon,
  Dashboard as DashboardIcon,
  AutoAwesome as LLMIcon,
  Logout as LogoutIcon,
  Login as LoginIcon,
  ChevronLeft as ChevronLeftIcon,
  ChevronRight as ChevronRightIcon,
} from '@mui/icons-material';
import { useGames } from '../api/hooks/useGame';
import { useLLMPresets } from '../api/hooks/useLLM';
import { useAuth } from '../api/hooks/useAuth';
import CreateGameDialog from './layout/CreateGameDialog';
import JoinGameDialog from './layout/JoinGameDialog';
import PresetListItem from './layout/PresetListItem';
import DashboardDrawerContent from './layout/DashboardDrawerContent';
import InGameDrawerContent from './layout/InGameDrawerContent';

const DRAWER_WIDTH = 260;
const DRAWER_COLLAPSED_WIDTH = 56;

// Unified button style for all left panel buttons
const buttonBaseSx = {
  width: '100%',
  justifyContent: 'flex-start',
  pl: 2,
  borderRadius: 1,
  mb: 0.5,
  minHeight: 40,
  px: 1.5,
  bgcolor: 'transparent',
  '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
};

export default function Layout() {
  const navigate = useNavigate();
  const location = useLocation();
  const theme = useTheme();
  const [drawerOpen, setDrawerOpen] = useState(true);
  const { user, isAuthenticated, logout } = useAuth();
  const { games, isLoading, createGame, joinGame } = useGames();
  const { presets } = useLLMPresets();

  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [joinDialogOpen, setJoinDialogOpen] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const currentGameId = location.pathname.match(/\/game\/([a-f0-9-]+)/)?.[1];
  const isInGame = !!currentGameId;
  const isDashboard = location.pathname === '/dashboard';
  const isLLMPresets = location.pathname === '/llm-presets';

  const activeGames = games?.filter(g => g.status !== 'Archived' && g.status !== 'Finished') || [];

  const handleCreateGameWithParams = async (name: string, systemId: string, llmPresetId: string | null, plotSeed: string, gameParameters: string) => {
    if (!name.trim()) return;
    setActionError(null);
    setActionSuccess(null);
    try {
      await createGame(name, systemId, undefined, undefined, llmPresetId || undefined, plotSeed || undefined, gameParameters || undefined);
      setCreateDialogOpen(false);
      setActionSuccess('Game created!');
      setTimeout(() => setActionSuccess(null), 2000);
    } catch (e: any) {
      setActionError(e.message);
    }
  };

  const handleJoinGameWithCode = async (code: string) => {
    setActionError(null);
    setActionSuccess(null);
    try {
      await joinGame(code);
      setJoinDialogOpen(false);
      setActionSuccess('Joined game!');
      setTimeout(() => setActionSuccess(null), 2000);
    } catch (e: any) {
      setActionError(e.message);
    }
  };

  const drawerWidth = drawerOpen ? DRAWER_WIDTH : DRAWER_COLLAPSED_WIDTH;

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      {/* Left Drawer - true flex sibling */}
      <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          whiteSpace: 'nowrap',
          boxSizing: 'border-box',
          transition: theme.transitions.create('width', {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.enteringScreen,
          }),
          '& .MuiDrawer-paper': {
            width: drawerWidth,
            transition: theme.transitions.create('width', {
              easing: theme.transitions.easing.sharp,
              duration: theme.transitions.duration.enteringScreen,
            }),
            overflowX: 'hidden',
            bgcolor: '#121212',
            borderRight: '1px solid rgba(255,255,255,0.08)',
            color: 'text.primary',
            display: 'flex',
            flexDirection: 'column',
            boxSizing: 'border-box',
          },
        }}
      >
        {/* Header: burger + ADnD as collapse toggle */}
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-start',
            gap: 1,
            p: 2,
            borderBottom: '1px solid rgba(255,255,255,0.08)',
            minHeight: 56,
            cursor: 'pointer',
          }}
          onClick={() => setDrawerOpen(!drawerOpen)}
        >
          <IconButton
            size="small"
            sx={{
              color: 'text.primary',
              bgcolor: 'rgba(255,255,255,0.08)',
              '&:hover': { bgcolor: 'rgba(255,255,255,0.15)' },
            }}
          >
            <MenuIcon />
          </IconButton>
          <Typography
            variant="h6"
            onClick={e => { e.stopPropagation(); navigate('/'); }}
            sx={{
              fontWeight: 700,
              letterSpacing: 1.5,
              color: 'primary.main',
              opacity: drawerOpen ? 1 : 0,
              transition: 'opacity 0.2s',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              cursor: 'pointer',
              userSelect: 'none',
            }}
          >
            ADnD
          </Typography>
        </Box>

        {/* Auth section - always visible */}
        <Box sx={{ px: 1.5, mb: 1 }}>
          {isAuthenticated ? (
            <>
              <ListItemButton
                onClick={() => {}}
                sx={{
                  ...buttonBaseSx,
                  justifyContent: 'flex-start',
                  bgcolor: 'transparent',
                }}
              >
                <Avatar sx={{ width: 24, height: 24, bgcolor: 'primary.main', fontSize: 10, mr: 1 }}>
                  {(user?.displayName || 'U')[0].toUpperCase()}
                </Avatar>
                {drawerOpen && (
                  <ListItemText
                    primary={user?.displayName || 'User'}
                    primaryTypographyProps={{ fontWeight: 600, fontSize: 13 }}
                  />
                )}
              </ListItemButton>
              <ListItemButton
                onClick={() => logout()}
                sx={{
                  ...buttonBaseSx,
                  color: 'error.light',
                  '&:hover': { bgcolor: 'rgba(244,67,54,0.1)' },
                }}
              >
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
                  <LogoutIcon fontSize="small" color="error" />
                </ListItemIcon>
                {drawerOpen && <ListItemText primary="Logout" />}
              </ListItemButton>
            </>
          ) : (
            <ListItemButton
              onClick={() => navigate('/login')}
              sx={{
                ...buttonBaseSx,
                bgcolor: 'rgba(145,71,255,0.15)',
                color: 'primary.light',
                '&:hover': { bgcolor: 'rgba(145,71,255,0.2)' },
              }}
            >
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
                <LoginIcon fontSize="small" color="primary" />
              </ListItemIcon>
              {drawerOpen && <ListItemText primary="Login" />}
            </ListItemButton>
          )}
        </Box>

        {/* Drawer Content - scrollable */}
        <Box sx={{ flexGrow: 1, overflow: 'auto', pb: 1 }}>
          {/* Always visible nav */}
          <ListItemButton
            onClick={() => navigate('/dashboard')}
            sx={{
              ...buttonBaseSx,
              bgcolor: isDashboard ? 'rgba(145,71,255,0.15)' : 'transparent',
              color: isDashboard ? 'primary.light' : 'text.primary',
              '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' },
            }}
          >
            <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
              <DashboardIcon color={isDashboard ? 'primary' : 'inherit'} />
            </ListItemIcon>
            {drawerOpen && <ListItemText primary="Games" />}
          </ListItemButton>

          <ListItemButton
            onClick={() => navigate('/llm-presets')}
            sx={{
              ...buttonBaseSx,
              bgcolor: isLLMPresets ? 'rgba(145,71,255,0.15)' : 'transparent',
              color: isLLMPresets ? 'primary.light' : 'text.primary',
              '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' },
            }}
          >
            <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
              <LLMIcon color={isLLMPresets ? 'primary' : 'inherit'} />
            </ListItemIcon>
            {drawerOpen && <ListItemText primary="LLM Presets" />}
          </ListItemButton>

          <Divider sx={{ my: 1, borderColor: 'rgba(255,255,255,0.08)' }} />

          {/* Dashboard: Action buttons */}
          {isDashboard && (
            <DashboardDrawerContent
              drawerOpen={drawerOpen}
              isLoading={isLoading}
              activeGamesCount={activeGames.length}
              onCreateGame={() => setCreateDialogOpen(true)}
              onJoinGame={() => setJoinDialogOpen(true)}
            />
          )}

          {/* LLM Presets page: presets list + Add Preset at bottom */}
          {isLLMPresets && (
            <>
              {drawerOpen && (
                <Typography variant="caption" sx={{ px: 2, color: 'text.secondary', display: 'block', mb: 0.5 }}>
                  YOUR PRESETS
                </Typography>
              )}
              {presets.length === 0 ? (
                drawerOpen && (
                  <Typography variant="caption" sx={{ px: 2, color: 'text.secondary' }}>No presets yet</Typography>
                )
              ) : (
                presets.map(preset => (
                  <PresetListItem
                    key={preset.id}
                    preset={preset}
                    drawerOpen={drawerOpen}
                    onSelect={(id) => navigate(`/llm-presets/${id}`)}
                    onAdd={() => navigate('/llm-presets/new')}
                  />
                ))
              )}
            </>
          )}

          {/* In-game: game info + back to games */}
          {isInGame && (
            <InGameDrawerContent
              drawerOpen={drawerOpen}
              game={games?.find(g => g.id === currentGameId)}
              isCreator={!!user?.id && (() => { const g = games?.find(g => g.id === currentGameId); return g && g.creatorId === user.id; })() || false}
              onBackToGames={() => navigate('/dashboard')}
              onAdminPanel={() => navigate(`/admin/${currentGameId}`)}
            />
          )}
        </Box>

        {/* Bottom spacer + collapse toggle */}
        <Box sx={{ flexGrow: 0, borderTop: '1px solid rgba(255,255,255,0.08)' }}>
          <ListItemButton
            onClick={() => setDrawerOpen(!drawerOpen)}
            sx={{
              justifyContent: 'center',
              py: 1.5,
              bgcolor: 'rgba(255,255,255,0.04)',
            }}
          >
            {drawerOpen ? <ChevronLeftIcon /> : <ChevronRightIcon />}
          </ListItemButton>
        </Box>
      </Drawer>

      {/* Main Content Area - fills remaining space, no overlap */}
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          minWidth: 0,
          p: { xs: 2, md: 3 },
          overflow: 'auto',
        }}
      >
        <Outlet />
      </Box>

      <CreateGameDialog
        open={createDialogOpen}
        onClose={() => setCreateDialogOpen(false)}
        onSubmit={(name, systemId, llmPresetId, plotSeed, gameParameters) => {
          handleCreateGameWithParams(name, systemId, llmPresetId, plotSeed, gameParameters);
        }}
        presets={presets}
        error={actionError}
        success={actionSuccess}
      />
      <JoinGameDialog
        open={joinDialogOpen}
        onClose={() => setJoinDialogOpen(false)}
        onSubmit={(code) => handleJoinGameWithCode(code)}
        error={actionError}
        success={actionSuccess}
      />
    </Box>
  );
}
