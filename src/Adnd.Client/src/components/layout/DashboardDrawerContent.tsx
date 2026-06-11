import { Typography, Divider, ListItemButton, ListItemIcon, ListItemText } from '@mui/material';
import { Add as AddIcon, PlayArrow as PlayArrowIcon } from '@mui/icons-material';

interface DashboardDrawerContentProps {
  drawerOpen: boolean;
  isLoading: boolean;
  activeGamesCount: number;
  onCreateGame: () => void;
  onJoinGame: () => void;
}

export default function DashboardDrawerContent({ drawerOpen, isLoading, activeGamesCount, onCreateGame, onJoinGame }: DashboardDrawerContentProps) {
  const buttonBaseSx = {
    width: '100%', justifyContent: 'flex-start', pl: 2, borderRadius: 1, mb: 0.5,
    minHeight: 40, px: 1.5, bgcolor: 'transparent',
    '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
  };

  return (
    <>
      <Typography variant="caption" sx={{ px: 2, color: 'text.secondary', display: 'block', mb: 0.5 }}>ACTIONS</Typography>
      <ListItemButton onClick={onCreateGame} sx={{ ...buttonBaseSx, color: 'primary.light', '&:hover': { bgcolor: 'rgba(145,71,255,0.1)' } }}>
        <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><AddIcon fontSize="small" color="primary" /></ListItemIcon>
        {drawerOpen && <ListItemText primary="New Game" />}
      </ListItemButton>
      <ListItemButton onClick={onJoinGame} sx={{ ...buttonBaseSx, color: 'success.light', '&:hover': { bgcolor: 'rgba(76,175,80,0.1)' } }}>
        <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><PlayArrowIcon fontSize="small" color="success" /></ListItemIcon>
        {drawerOpen && <ListItemText primary="Join by Code" />}
      </ListItemButton>
      <Divider sx={{ my: 1, borderColor: 'rgba(255,255,255,0.08)' }} />
      {drawerOpen && <Typography variant="caption" sx={{ px: 2, color: 'text.secondary', display: 'block', mb: 0.5 }}>YOUR GAMES</Typography>}
      {isLoading ? (
        drawerOpen && <Typography variant="caption" sx={{ px: 2, color: 'text.secondary' }}>Loading...</Typography>
      ) : activeGamesCount === 0 ? (
        drawerOpen && <Typography variant="caption" sx={{ px: 2, color: 'text.secondary' }}>No games yet</Typography>
      ) : null}
    </>
  );
}
