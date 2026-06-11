import { Paper, Typography, Chip, ListItemButton, ListItemIcon, ListItemText, Box } from '@mui/material';
import { ChevronLeft as ChevronLeftIcon, Shield as ShieldIcon } from '@mui/icons-material';
import type { GameDetail } from '../../types/game.types';

interface InGameDrawerContentProps {
  drawerOpen: boolean;
  game: GameDetail | undefined;
  isCreator: boolean;
  onBackToGames: () => void;
  onAdminPanel: () => void;
}

export default function InGameDrawerContent({ drawerOpen, game, isCreator, onBackToGames, onAdminPanel }: InGameDrawerContentProps) {
  const buttonBaseSx = {
    width: '100%', justifyContent: 'flex-start', pl: 2, borderRadius: 1, mb: 0.5,
    minHeight: 40, px: 1.5, bgcolor: 'transparent',
    '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
  };

  return (
    <>
      {drawerOpen && <Typography variant="caption" sx={{ px: 2, color: 'text.secondary', display: 'block', mb: 0.5 }}>CURRENT GAME</Typography>}
      <Paper elevation={1} sx={{ mx: 1, p: 1.5, bgcolor: 'rgba(145,71,255,0.08)', borderRadius: 2, mb: 1 }}>
        {drawerOpen && (
          <>
            <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5, color: 'primary.light' }}>
              {game?.name || 'Game'}
            </Typography>
            <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
              <Chip label={game?.status || ''} size="small" sx={{ height: 18, fontSize: 10 }} color={game?.status === 'Active' ? 'success' : 'default'} />
              {game?.llmPresetName && (
                <Chip label={game.llmPresetName} size="small" sx={{ height: 18, fontSize: 10 }} variant="outlined" />
              )}
            </Box>
          </>
        )}
      </Paper>
      <ListItemButton onClick={onBackToGames} sx={{ ...buttonBaseSx, color: 'text.secondary', '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' } }}>
        <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><ChevronLeftIcon fontSize="small" /></ListItemIcon>
        {drawerOpen && <ListItemText primary="Back to Games" />}
      </ListItemButton>
      {isCreator && (
        <ListItemButton onClick={onAdminPanel} sx={{ ...buttonBaseSx, color: 'warning.light', '&:hover': { bgcolor: 'rgba(255,193,7,0.1)' } }}>
          <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}><ShieldIcon fontSize="small" color="warning" /></ListItemIcon>
          {drawerOpen && <ListItemText primary="Admin Panel" />}
        </ListItemButton>
      )}
    </>
  );
}
