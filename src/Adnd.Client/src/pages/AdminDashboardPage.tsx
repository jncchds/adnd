import { useParams } from 'react-router-dom';
import { useGame } from '../api/hooks/useGameDetail';
import GameStatePage from './GameStatePage';
import { Box, Typography } from '@mui/material';

export default function AdminDashboardPage() {
  const { id } = useParams<{ id: string }>();
  const { game, isLoading } = useGame(id);

  if (isLoading) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading...</Typography></Box>;
  if (!game) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography variant="h5" color="error">Game not found</Typography></Box>;

  return <GameStatePage gameId={id!} />;
}
