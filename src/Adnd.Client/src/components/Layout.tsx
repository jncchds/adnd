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
  Chip,
  Paper,
  useTheme,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Alert,
  AlertTitle,
  Avatar,
} from '@mui/material';
import {
  Menu as MenuIcon,
  Dashboard as DashboardIcon,
  SportsEsports as GameIcon,
  AutoAwesome as LLMIcon,
  Add as AddIcon,
  PlayArrow as PlayArrowIcon,
  Logout as LogoutIcon,
  Login as LoginIcon,
  ChevronLeft as ChevronLeftIcon,
  ChevronRight as ChevronRightIcon,
  Shield as ShieldIcon,
} from '@mui/icons-material';
import { useGames, useLLMPresets } from '../api/gameHooks';
import { useAuth } from '../api/authHook';

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

  // Dialog state
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [joinDialogOpen, setJoinDialogOpen] = useState(false);
  const [gameName, setGameName] = useState('');
  const [systemId, setSystemId] = useState('dnd5e');
  const [llmPresetId, setLLMPresetId] = useState<string | null>(null);
  const [plotSeed, setPlotSeed] = useState('');
  const [gameParameters, setGameParameters] = useState('');
  const [joinCode, setJoinCode] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const currentGameId = location.pathname.match(/\/game\/([a-f0-9-]+)/)?.[1];
  const isInGame = !!currentGameId;
  const isDashboard = location.pathname === '/dashboard';
  const isLLMPresets = location.pathname === '/llm-presets';

  const activeGames = games?.filter(g => g.status !== 'Archived' && g.status !== 'Finished') || [];

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

  const handleJoinGame = async () => {
    setActionError(null);
    setActionSuccess(null);
    try {
      await joinGame(joinCode);
      setJoinDialogOpen(false);
      setJoinCode('');
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
            <>
              <Typography variant="caption" sx={{ px: 2, color: 'text.secondary', display: 'block', mb: 0.5 }}>
                ACTIONS
              </Typography>
              <ListItemButton
                onClick={() => setCreateDialogOpen(true)}
                sx={{
                  ...buttonBaseSx,
                  color: 'primary.light',
                  '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' },
                }}
              >
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
                  <AddIcon fontSize="small" color="primary" />
                </ListItemIcon>
                {drawerOpen && <ListItemText primary="New Game" />}
              </ListItemButton>
              <ListItemButton
                onClick={() => setJoinDialogOpen(true)}
                sx={{
                  ...buttonBaseSx,
                  color: 'success.light',
                  '&:hover': { bgcolor: 'rgba(76,175,80,0.1)' },
                }}
              >
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
                  <PlayArrowIcon fontSize="small" color="success" />
                </ListItemIcon>
                {drawerOpen && <ListItemText primary="Join by Code" />}
              </ListItemButton>
              <Divider sx={{ my: 1, borderColor: 'rgba(255,255,255,0.08)' }} />
              {drawerOpen && (
                <Typography variant="caption" sx={{ px: 2, color: 'text.secondary', display: 'block', mb: 0.5 }}>
                  YOUR GAMES
                </Typography>
              )}
              {isLoading ? (
                drawerOpen && (
                  <Typography variant="caption" sx={{ px: 2, color: 'text.secondary' }}>Loading...</Typography>
                )
              ) : activeGames.length === 0 ? (
                drawerOpen && (
                  <Typography variant="caption" sx={{ px: 2, color: 'text.secondary' }}>No games yet</Typography>
                )
              ) : (
                activeGames.map(game => (
                  <ListItemButton
                    key={game.id}
                    onClick={() => navigate(`/game/${game.id}`)}
                    sx={{
                      ...buttonBaseSx,
                      bgcolor: 'transparent',
                      '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
                    }}
                  >
                    <ListItemIcon sx={{ minWidth: 0, mr: 'auto', justifyContent: 'center' }}>
                      <GameIcon fontSize="small" color="action" />
                    </ListItemIcon>
                    {drawerOpen && (
                      <ListItemText
                        primary={game.name}
                        primaryTypographyProps={{ noWrap: true, fontSize: 13, fontWeight: 500 }}
                      />
                    )}
                  </ListItemButton>
                ))
              )}
            </>
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
                  <ListItemButton
                    key={preset.id}
                    onClick={() => navigate(`/llm-presets/${preset.id}`)}
                    sx={{
                      ...buttonBaseSx,
                      bgcolor: preset.isDefault ? 'rgba(145,71,255,0.08)' : 'transparent',
                      border: preset.isDefault ? '1px solid' : 'none',
                      borderColor: preset.isDefault ? 'rgba(145,71,255,0.3)' : 'transparent',
                      '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
                    }}
                  >
                    <ListItemIcon sx={{ minWidth: 0, mr: 'auto', justifyContent: 'center' }}>
                      <LLMIcon fontSize="small" color="action" />
                    </ListItemIcon>
                    {drawerOpen && (
                      <ListItemText
                        primary={preset.name}
                        primaryTypographyProps={{ noWrap: true, fontSize: 13, fontWeight: 500 }}
                      />
                    )}
                  </ListItemButton>
                ))
              )}
              {drawerOpen && (
                <ListItemButton
                  onClick={() => navigate('/llm-presets/new')}
                  sx={{
                    ...buttonBaseSx,
                    bgcolor: 'rgba(145,71,255,0.1)',
                    color: 'primary.light',
                    '&:hover': { bgcolor: 'rgba(145,71,255,0.15)' },
                    mt: 1,
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
                    <AddIcon fontSize="small" color="primary" />
                  </ListItemIcon>
                  <ListItemText primary="Add Preset" />
                </ListItemButton>
              )}
            </>
          )}

          {/* In-game: game info + back to games */}
          {isInGame && (
            <>
              {drawerOpen && (
                <Typography variant="caption" sx={{ px: 2, color: 'text.secondary', display: 'block', mb: 0.5 }}>
                  CURRENT GAME
                </Typography>
              )}
              <Paper
                elevation={1}
                sx={{
                  mx: 1,
                  p: 1.5,
                  bgcolor: 'rgba(145,71,255,0.08)',
                  borderRadius: 2,
                  mb: 1,
                }}
              >
                {drawerOpen && (
                  <>
                    <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5, color: 'primary.light' }}>
                      {games?.find(g => g.id === currentGameId)?.name || 'Game'}
                    </Typography>
                    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                      <Chip
                        label={games?.find(g => g.id === currentGameId)?.status || ''}
                        size="small"
                        sx={{ height: 18, fontSize: 10 }}
                        color={games?.find(g => g.id === currentGameId)?.status === 'Active' ? 'success' : 'default'}
                      />
                      {games?.find(g => g.id === currentGameId)?.llmPresetName && (
                        <Chip
                          label={games.find(g => g.id === currentGameId)!.llmPresetName!}
                          size="small"
                          sx={{ height: 18, fontSize: 10 }}
                          variant="outlined"
                        />
                      )}
                    </Box>
                  </>
                )}
              </Paper>
              <ListItemButton
                onClick={() => navigate('/dashboard')}
                sx={{
                  ...buttonBaseSx,
                  color: 'text.secondary',
                  '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
                }}
              >
                <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
                  <ChevronLeftIcon fontSize="small" />
                </ListItemIcon>
                {drawerOpen && <ListItemText primary="Back to Games" />}
              </ListItemButton>
              {/* Admin button for game creator */}
              {(() => {
                const game = games?.find(g => g.id === currentGameId);
                const isCreator = user?.id && game && game.creatorId === user.id;
                if (!isCreator) return null;
                return (
                  <ListItemButton
                    onClick={() => navigate(`/admin/${currentGameId}`)}
                    sx={{
                      ...buttonBaseSx,
                      color: 'warning.light',
                      '&:hover': { bgcolor: 'rgba(255,193,7,0.1)' },
                    }}
                  >
                    <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
                      <ShieldIcon fontSize="small" color="warning" />
                    </ListItemIcon>
                    {drawerOpen && <ListItemText primary="Admin Panel" />}
                  </ListItemButton>
                );
              })()}
            </>
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

      {/* Create Game Dialog */}
      <Dialog open={createDialogOpen} onClose={() => setCreateDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Create New Game</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            fullWidth
            label="Game Name"
            value={gameName}
            onChange={e => setGameName(e.target.value)}
            autoFocus
          />
          <TextField
            fullWidth
            select
            label="System"
            value={systemId}
            onChange={e => setSystemId(e.target.value)}
          >
            <option value="dnd5e">D&D 5th Edition</option>
            <option value="pf2e">Pathfinder 2nd Edition</option>
            <option value="coc7e">Call of Cthulhu 7th Edition</option>
          </TextField>
          <TextField
            fullWidth
            select
            label="LLM Preset (for AI-GM)"
            value={llmPresetId || ''}
            onChange={e => setLLMPresetId(e.target.value || null)}
          >
            <option value="">None</option>
            {presets.map(p => (
              <option key={p.id} value={p.id}>{p.name} ({p.providerType})</option>
            ))}
          </TextField>
          <TextField
            fullWidth
            multiline
            rows={3}
            label="Plot Seed (initial story premise)"
            value={plotSeed}
            onChange={e => setPlotSeed(e.target.value)}
            placeholder="Describe the initial story, setting, and tone..."
          />
          <TextField
            fullWidth
            multiline
            rows={2}
            label="Game Parameters (tone, difficulty, pacing)"
            value={gameParameters}
            onChange={e => setGameParameters(e.target.value)}
            placeholder="e.g., Dark tone, medium difficulty, fast-paced..."
          />
          {actionError && (
            <Alert severity="error" onClose={() => setActionError(null)}>
              <AlertTitle>Error</AlertTitle>
              {actionError}
            </Alert>
          )}
          {actionSuccess && (
            <Alert severity="success" onClose={() => setActionSuccess(null)}>
              {actionSuccess}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleCreateGame} variant="contained" disabled={!gameName.trim()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>

      {/* Join Game Dialog */}
      <Dialog open={joinDialogOpen} onClose={() => setJoinDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Join a Game</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <TextField
            fullWidth
            label="Invite Code"
            value={joinCode}
            onChange={e => setJoinCode(e.target.value)}
            placeholder="Enter the 8-character invite code"
            autoFocus
          />
          {actionError && (
            <Alert severity="error" onClose={() => setActionError(null)} sx={{ mt: 2 }}>
              <AlertTitle>Error</AlertTitle>
              {actionError}
            </Alert>
          )}
          {actionSuccess && (
            <Alert severity="success" onClose={() => setActionSuccess(null)} sx={{ mt: 2 }}>
              {actionSuccess}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setJoinDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleJoinGame} variant="contained" disabled={!joinCode.trim()}>
            Join
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
