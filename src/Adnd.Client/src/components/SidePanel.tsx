import { useNavigate } from 'react-router-dom';
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
  Chip,
} from '@mui/material';
import {
  Menu as MenuIcon,
  Dashboard as DashboardIcon,
  AutoAwesome as LLMIcon,
  Settings as SystemsIcon,
  Logout as LogoutIcon,
  ChevronLeft as ChevronLeftIcon,
  ChevronRight as ChevronRightIcon,
  SportsEsports as GameIcon,
  Shield as ShieldIcon,
  ArrowBack as BackIcon,
  Chat as ChatIcon,
  DirectionsRun as CombatIcon,
  People as PeopleIcon,
  Article as SheetIcon,
  Build as ActionIcon,
  Mic as MicIcon,
  Settings as SettingsIcon,
  Book as BookIcon,
  Lightbulb as BulbIcon,
  History as HistoryIcon,
  AutoFixHigh as ConsistencyIcon,
  ChatBubble as ChatBubbleIcon,
  BarChart as BarChartIcon,
  Add as AddIcon,
  PlayArrow as PlayArrowIcon,
} from '@mui/icons-material';
import { useAuth } from '../api/authHook';
import type { GameListItem } from '../api/client';
import type { LLMPreset } from '../api/client';

const DRAWER_WIDTH = 260;
const DRAWER_COLLAPSED_WIDTH = 56;

export type AppView = 'welcome' | 'dashboard' | 'llm-presets' | 'systems' | 'user-settings' | 'game' | 'admin';

interface SidePanelProps {
  open: boolean;
  onToggle: () => void;
  currentView: AppView;
  onNavigate: (view: AppView) => void;
  gameId?: string;
  activeGameTab?: string;
  onNewGame?: () => void;
  onJoinGame?: () => void;
  onAddPreset?: () => void;
  onNewSystem?: () => void;
  games?: GameListItem[];
  presets?: LLMPreset[];
}

// Unified button style
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

export default function SidePanel({ open, onToggle, currentView, onNavigate, gameId, activeGameTab, onNewGame, onJoinGame, onAddPreset, onNewSystem, games, presets }: SidePanelProps) {
  const navigate = useNavigate();
  const theme = useTheme();
  const { user, isAuthenticated, logout } = useAuth();

  const drawerWidth = open ? DRAWER_WIDTH : DRAWER_COLLAPSED_WIDTH;

  const isInGame = gameId !== undefined;
  const activeGames = games?.filter(g => g.status !== 'Archived' && g.status !== 'Finished') || [];
  const resolvedPresets = presets ?? [];

  const handleLogout = async () => {
    await logout();
    onNavigate('welcome');
  };

  return (
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
      {/* Header: burger + ADnD */}
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
        onClick={onToggle}
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
          sx={{
            fontWeight: 700,
            letterSpacing: 1.5,
            color: 'primary.main',
            opacity: open ? 1 : 0,
            transition: 'opacity 0.2s',
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            userSelect: 'none',
          }}
        >
          ADnD
        </Typography>
      </Box>

      {/* Auth section */}
      <Box sx={{ px: 1.5, mb: 1 }}>
        {isAuthenticated ? (
          <>
            <ListItemButton
              sx={{ ...buttonBaseSx, justifyContent: 'flex-start', bgcolor: 'transparent' }}
            >
              <Avatar sx={{ width: 24, height: 24, bgcolor: 'primary.main', fontSize: 10, mr: 1 }}>
                {(user?.displayName || 'U')[0].toUpperCase()}
              </Avatar>
              {open && <ListItemText primary={user?.displayName || 'User'} primaryTypographyProps={{ fontWeight: 600, fontSize: 13 }} />}
            </ListItemButton>
            <ListItemButton onClick={handleLogout} sx={{ ...buttonBaseSx, color: 'error.light', '&:hover': { bgcolor: 'rgba(244,67,54,0.1)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><LogoutIcon fontSize="small" color="error" /></ListItemIcon>
              {open && <ListItemText primary="Log out" />}
            </ListItemButton>
          </>
        ) : (
          <>
            <ListItemButton onClick={() => navigate('/login')} sx={{ ...buttonBaseSx, bgcolor: 'rgba(145,71,255,0.15)', color: 'primary.light', '&:hover': { bgcolor: 'rgba(145,71,255,0.2)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><LogoutIcon fontSize="small" color="primary" /></ListItemIcon>
              {open && <ListItemText primary="Log in" />}
            </ListItemButton>
            <ListItemButton onClick={() => navigate('/register')} sx={{ ...buttonBaseSx, bgcolor: 'rgba(145,71,255,0.15)', color: 'primary.light', '&:hover': { bgcolor: 'rgba(145,71,255,0.2)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><LogoutIcon fontSize="small" color="primary" /></ListItemIcon>
              {open && <ListItemText primary="Register" />}
            </ListItemButton>
          </>
        )}
      </Box>

      {/* Scrollable content */}
      <Box sx={{ flexGrow: 1, overflow: 'auto', pb: 1 }}>
        {/* Always-visible nav (logged in) */}
        {isAuthenticated && (
          <>
            <ListItemButton onClick={() => onNavigate('dashboard')} sx={{ ...buttonBaseSx, bgcolor: currentView === 'dashboard' ? 'rgba(145,71,255,0.15)' : 'transparent', color: currentView === 'dashboard' ? 'primary.light' : 'text.primary', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><DashboardIcon color={currentView === 'dashboard' ? 'primary' : 'inherit'} /></ListItemIcon>
              {open && <ListItemText primary="Games" />}
            </ListItemButton>
            <ListItemButton onClick={() => onNavigate('llm-presets')} sx={{ ...buttonBaseSx, bgcolor: currentView === 'llm-presets' ? 'rgba(145,71,255,0.15)' : 'transparent', color: currentView === 'llm-presets' ? 'primary.light' : 'text.primary', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><LLMIcon color={currentView === 'llm-presets' ? 'primary' : 'inherit'} /></ListItemIcon>
              {open && <ListItemText primary="LLM Presets" />}
            </ListItemButton>
            <ListItemButton onClick={() => onNavigate('systems')} sx={{ ...buttonBaseSx, bgcolor: currentView === 'systems' ? 'rgba(145,71,255,0.15)' : 'transparent', color: currentView === 'systems' ? 'primary.light' : 'text.primary', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><SystemsIcon color={currentView === 'systems' ? 'primary' : 'inherit'} /></ListItemIcon>
              {open && <ListItemText primary="Systems" />}
            </ListItemButton>
            <ListItemButton onClick={() => onNavigate('user-settings')} sx={{ ...buttonBaseSx, bgcolor: currentView === 'user-settings' ? 'rgba(145,71,255,0.15)' : 'transparent', color: currentView === 'user-settings' ? 'primary.light' : 'text.primary', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><SettingsIcon color={currentView === 'user-settings' ? 'primary' : 'inherit'} /></ListItemIcon>
              {open && <ListItemText primary="User Settings" />}
            </ListItemButton>
            <Divider sx={{ my: 1, borderColor: 'rgba(255,255,255,0.08)' }} />
          </>
        )}

        {/* Dashboard: action buttons + game list */}
        {currentView === 'dashboard' && open && (
          <>
            {onNewGame && (
              <ListItemButton onClick={onNewGame} sx={{ ...buttonBaseSx, color: 'primary.light', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' }, mt: 0.5 }}>
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><AddIcon fontSize="small" color="primary" /></ListItemIcon>
                <ListItemText primary="New Game" />
              </ListItemButton>
            )}
            {onJoinGame && (
              <ListItemButton onClick={onJoinGame} sx={{ ...buttonBaseSx, color: 'success.light', '&:hover': { bgcolor: 'rgba(76,175,80,0.1)' } }}>
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><PlayArrowIcon fontSize="small" color="success" /></ListItemIcon>
                <ListItemText primary="Join by Code" />
              </ListItemButton>
            )}
            {activeGames.map(game => (
              <ListItemButton key={game.id} onClick={() => navigate(`/game/${game.id}`)} sx={{ ...buttonBaseSx, bgcolor: 'transparent', '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' } }}>
                <ListItemIcon sx={{ minWidth: 0, mr: 'auto', justifyContent: 'center' }}><GameIcon fontSize="small" color="action" /></ListItemIcon>
                <ListItemText primary={game.name} primaryTypographyProps={{ noWrap: true, fontSize: 13, fontWeight: 500 }} />
              </ListItemButton>
            ))}
          </>
        )}

        {/* LLM Presets view */}
        {currentView === 'llm-presets' && open && (
          <>
            {onAddPreset && (
              <ListItemButton onClick={onAddPreset} sx={{ ...buttonBaseSx, color: 'primary.light', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' }, mt: 0.5 }}>
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><AddIcon fontSize="small" color="primary" /></ListItemIcon>
                <ListItemText primary="Add Preset" />
              </ListItemButton>
            )}
            {resolvedPresets.length === 0 ? (
              <Typography variant="caption" sx={{ px: 2, color: 'text.secondary' }}>No presets yet</Typography>
            ) : (
              resolvedPresets.map(preset => (
                <ListItemButton key={preset.id} onClick={() => navigate(`/llm-presets/${preset.id}`)} sx={{ ...buttonBaseSx, bgcolor: preset.isDefault ? 'rgba(145,71,255,0.08)' : 'transparent', border: preset.isDefault ? '1px solid' : 'none', borderColor: preset.isDefault ? 'rgba(145,71,255,0.3)' : 'transparent', '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' } }}>
                  <ListItemIcon sx={{ minWidth: 0, mr: 'auto', justifyContent: 'center' }}><LLMIcon fontSize="small" color="action" /></ListItemIcon>
                  <ListItemText primary={preset.name} primaryTypographyProps={{ noWrap: true, fontSize: 13, fontWeight: 500 }} />
                </ListItemButton>
              ))
            )}
          </>
        )}

        {/* Systems view */}
        {currentView === 'systems' && open && (
          <>
            {onNewSystem && (
              <ListItemButton onClick={onNewSystem} sx={{ ...buttonBaseSx, color: 'primary.light', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' }, mt: 0.5 }}>
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><AddIcon fontSize="small" color="primary" /></ListItemIcon>
                <ListItemText primary="New System" />
              </ListItemButton>
            )}
          </>
        )}

        {/* Game view */}
        {currentView === 'game' && (
          <>
            {open && gameId && (
              <Chip
                label={games?.find(g => g.id === gameId)?.name || 'Game'}
                size="small"
                color="primary"
                variant="outlined"
                sx={{ mx: 1.5, mb: 1 }}
              />
            )}
            <ListItemButton onClick={() => onNavigate('dashboard')} sx={{ ...buttonBaseSx, color: 'text.secondary', '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><BackIcon fontSize="small" /></ListItemIcon>
              {open && <ListItemText primary="Back" />}
            </ListItemButton>
            {isInGame && gameId && (
              <ListItemButton onClick={() => navigate(`/admin/${gameId}`)} sx={{ ...buttonBaseSx, color: 'warning.light', '&:hover': { bgcolor: 'rgba(255,193,7,0.1)' } }}>
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><ShieldIcon fontSize="small" color="warning" /></ListItemIcon>
                {open && <ListItemText primary="Admin" />}
              </ListItemButton>
            )}
            {open && (
              <>
                <Divider sx={{ my: 1, borderColor: 'rgba(255,255,255,0.08)' }} />
                {[{ key: 'chat', label: 'Chat', icon: <ChatIcon fontSize="small" /> },
                  { key: 'combat', label: 'Combat', icon: <CombatIcon fontSize="small" /> },
                  { key: 'players', label: 'Players', icon: <PeopleIcon fontSize="small" /> },
                  { key: 'characters', label: 'Characters', icon: <SheetIcon fontSize="small" /> },
                  { key: 'actions', label: 'Actions', icon: <ActionIcon fontSize="small" /> },
                  { key: 'agent-calls', label: 'Agent Calls', icon: <MicIcon fontSize="small" /> },
                  { key: 'settings', label: 'Settings', icon: <SettingsIcon fontSize="small" /> }].map(tab => (
                  <ListItemButton key={tab.key} onClick={() => { window.location.hash = tab.key; }} sx={{ ...buttonBaseSx, bgcolor: activeGameTab === tab.key ? 'rgba(145,71,255,0.15)' : 'transparent', color: activeGameTab === tab.key ? 'primary.light' : 'text.primary', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' } }}>
                    <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>{tab.icon}</ListItemIcon>
                    <ListItemText primary={tab.label} />
                  </ListItemButton>
                ))}
              </>
            )}
          </>
        )}

        {/* Admin view */}
        {currentView === 'admin' && (
          <>
            <ListItemButton onClick={() => onNavigate('game')} sx={{ ...buttonBaseSx, color: 'text.secondary', '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' } }}>
              <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><BackIcon fontSize="small" /></ListItemIcon>
              {open && <ListItemText primary="Back" />}
            </ListItemButton>
            {open && (
              <>
                <Divider sx={{ my: 1, borderColor: 'rgba(255,255,255,0.08)' }} />
                {[{ key: 'npcs', label: 'NPCs', icon: <PeopleIcon fontSize="small" /> },
                  { key: 'plot-board', label: 'Plot Board', icon: <BulbIcon fontSize="small" /> },
                  { key: 'plot-threads', label: 'Plot Threads', icon: <BookIcon fontSize="small" /> },
                  { key: 'characters', label: 'Characters', icon: <SheetIcon fontSize="small" /> },
                  { key: 'consistency', label: 'Consistency', icon: <ConsistencyIcon fontSize="small" /> },
                  { key: 'agent-calls', label: 'Agent Calls', icon: <MicIcon fontSize="small" /> },
                  { key: 'llm-usage', label: 'LLM Usage', icon: <BarChartIcon fontSize="small" /> },
                  { key: 'llm-logs', label: 'LLM Logs', icon: <HistoryIcon fontSize="small" /> },
                  { key: 'whispers', label: 'Whispers', icon: <ChatBubbleIcon fontSize="small" /> },
                  { key: 'game-state', label: 'Game State', icon: <SettingsIcon fontSize="small" /> }].map(tab => (
                  <ListItemButton key={tab.key} onClick={() => { window.location.hash = tab.key; }} sx={{ ...buttonBaseSx, bgcolor: 'transparent', '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' } }}>
                    <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>{tab.icon}</ListItemIcon>
                    <ListItemText primary={tab.label} />
                  </ListItemButton>
                ))}
              </>
            )}
          </>
        )}
      </Box>

      {/* Collapse toggle */}
      <Box sx={{ flexGrow: 0, borderTop: '1px solid rgba(255,255,255,0.08)' }}>
        <ListItemButton onClick={onToggle} sx={{ justifyContent: 'center', py: 1.5, bgcolor: 'rgba(255,255,255,0.04)' }}>
          {open ? <ChevronLeftIcon /> : <ChevronRightIcon />}
        </ListItemButton>
      </Box>
    </Drawer>
  );
}
