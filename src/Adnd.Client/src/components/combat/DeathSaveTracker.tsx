import { Box, Typography } from '@mui/material';
import type { CombatParticipantSummary } from '../../types/combat.types';

export default function DeathSaveTracker({ participant }: { participant: CombatParticipantSummary }) {
  const deathState = (participant as any).deathSaveState as { successes?: number; failures?: number } | null;
  const successes = deathState?.successes ?? 0;
  const failures = deathState?.failures ?? 0;

  if (participant.currentHP > 0) return null;

  return (
    <Box sx={{ mt: 0.5 }}>
      <Typography variant="caption" color="text.secondary">Death Saves:</Typography>
      <Box sx={{ display: 'flex', gap: 0.5, mt: 0.25 }}>
        {[0, 1, 2].map(i => (
          <Box key={`s${i}`} sx={{
            width: 14, height: 14, borderRadius: '50%',
            bgcolor: i < successes ? 'success.main' : 'grey.500',
            border: `2px solid ${i < successes ? 'success.main' : 'grey.400'}`,
          }} />
        ))}
        <Typography variant="caption" color="text.secondary" sx={{ mx: 0.5 }}>|</Typography>
        {[0, 1, 2].map(i => (
          <Box key={`f${i}`} sx={{
            width: 14, height: 14, borderRadius: '50%',
            bgcolor: i < failures ? 'error.main' : 'grey.500',
            border: `2px solid ${i < failures ? 'error.main' : 'grey.400'}`,
          }} />
        ))}
      </Box>
    </Box>
  );
}

