import { ListItemButton, ListItemIcon, ListItemText } from '@mui/material';
import { SportsEsports as GameIcon } from '@mui/icons-material';
import type { GameDetail } from '../../types/game.types';

interface GameListItemProps {
  game: GameDetail;
  drawerOpen: boolean;
  onSelect: (id: string) => void;
}

export default function GameListItem({ game, drawerOpen, onSelect }: GameListItemProps) {
  const buttonBaseSx = {
    width: '100%', justifyContent: 'flex-start', pl: 2, borderRadius: 1, mb: 0.5,
    minHeight: 40, px: 1.5, bgcolor: 'transparent',
    '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
  };

  return (
    <ListItemButton
      onClick={() => onSelect(game.id)}
      sx={buttonBaseSx}
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
  );
}
