import { Box, Typography, Paper, Chip } from '@mui/material';
import { PlotThreadCard } from './GameStateCards';

interface GameStatePlotProps {
  gameState: any;
}

export default function GameStatePlot({ gameState }: GameStatePlotProps) {
  if (!gameState?.PlotThreads) return <Typography>No plot data available.</Typography>;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Plot Board</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Chip label={`${gameState.PlotStats.Active} active`} size="small" color="success" />
          <Chip label={`${gameState.PlotStats.Resolved} resolved`} size="small" />
          <Chip label={`${gameState.PlotStats.Abandoned} abandoned`} size="small" color="error" />
        </Box>
      </Box>

      {gameState.PlotThreads.length === 0 ? (
        <Typography color="text.secondary">No active plot threads.</Typography>
      ) : (
        gameState.PlotThreads.map((t: any) => <PlotThreadCard key={t.id} thread={t} />)
      )}

      {/* Plot Reviews */}
      {gameState.PlotStats?.RecentReviews > 0 && (
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2">Recent Plot Reviews</Typography>
          <Typography variant="caption" color="text.secondary">
            {gameState.PlotStats.RecentReviews} reviews in history
          </Typography>
        </Paper>
      )}
    </Box>
  );
}

// ==================== Agent Tab ====================
