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
