import { Box, Typography, Paper, Chip, Divider, Button, IconButton } from '@mui/material';
import { ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon } from '@mui/icons-material';
import MarkdownRenderer from '../MarkdownRenderer';

interface GameStateCombatProps {
  gameState: any;
}

export default function GameStateCombat({ gameState }: GameStateCombatProps) {
  if (!gameState?.ActiveCombats) return <Typography>No combat data available.</Typography>;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Active Combats ({gameState.ActiveCombats.length})</Typography>
        {gameState.CombatStats?.Active === 0 && (
          <Typography variant="body2" color="text.secondary">No active combats</Typography>
        )}
      </Box>

      {gameState.ActiveCombats.map((combat: any) => (
        <Paper key={combat.id} sx={{ p: 2 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <CombatIcon color="error" />
              <Typography variant="h6">{combat.name || 'Unnamed Combat'}</Typography>
              <Chip label={combat.status} size="small" color={combat.status === 'Active' ? 'success' : 'warning'} />
              <Chip label={`Round ${combat.currentRound}`} size="small" />
            </Box>
            <Typography variant="caption" color="text.secondary">
              Started {new Date(combat.startedAt).toLocaleString()}
            </Typography>
          </Box>

          {/* Participants */}
          <Typography variant="subtitle2" sx={{ mb: 1 }}>Participants ({combat.participants.length})</Typography>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, mb: 2 }}>
            {combat.participants.map((p: any) => (
              <Box key={p.id} sx={{
                p: 1, minWidth: 180, bgcolor: p.isCurrentTurn ? 'primary.lighter' : 'background.default',
                border: p.isCurrentTurn ? '2px solid' : '1px solid',
                borderColor: p.isCurrentTurn ? 'primary.main' : 'divider',
                borderRadius: 1,
              }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                  <Typography variant="body2" fontWeight="bold">{p.displayName}</Typography>
                  <Chip label={p.participantType} size="small" />
                </Box>
                {/* HP Bar */}
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
                  <Box sx={{ flex: 1 }}>
                    <LinearProgress variant="determinate" value={(p.currentHP / p.maxHP) * 100}
                      sx={{ height: 6, borderRadius: 3, bgcolor: 'background.paper',
                        '& .MuiLinearProgress-bar': { bgcolor: p.currentHP / p.maxHP < 0.3 ? 'error.main' : p.currentHP / p.maxHP < 0.6 ? 'warning.main' : 'success.main' } }} />
                  </Box>
                  <Typography variant="caption" color={p.currentHP / p.maxHP < 0.3 ? 'error' : 'text.secondary'}>
                    {p.currentHP}/{p.maxHP}
                  </Typography>
                </Box>
                <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                  <Chip label={`AC:${p.ac}`} size="small" />
                  <Chip label={`Init:${p.initiative}`} size="small" />
                  <Chip label={`HP:${p.currentHP}/${p.maxHP}`} size="small" />
                  <Chip label={`A:${p.actionsRemaining}`} size="small" />
                  <Chip label={`BA:${p.bonusActionsRemaining}`} size="small" />
                  <Chip label={`R:${p.reactionsRemaining}`} size="small" />
                  <Chip label={`M:${p.movementsRemaining}`} size="small" />
                </Box>
                {/* Conditions */}
                {p.conditions && p.conditions.length > 0 && (
                  <Box sx={{ mt: 0.5, display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                    {p.conditions.map((c: any, i: number) => (
                      <Chip key={i} label={c.name} size="small" color="error" variant="outlined" />
                    ))}
                  </Box>
                )}
              </Box>
            ))}
          </Box>

          {/* Recent Events */}
          <Typography variant="subtitle2" sx={{ mb: 1 }}>Recent Events ({combat.events.length})</Typography>
          <Box sx={{ maxHeight: 200, overflow: 'auto', bgcolor: 'background.default', p: 1, borderRadius: 1 }}>
            {combat.events.map((e: any) => (
              <Box key={e.id} sx={{ display: 'flex', gap: 1, mb: 0.5 }}>
                <Typography variant="caption" color="text.secondary" sx={{ minWidth: 55 }}>
                  R{e.round} {new Date(e.createdAt).toLocaleTimeString()}
                </Typography>
                <Typography variant="caption" fontWeight="bold">{e.actorName}</Typography>
                <Typography variant="caption" color="text.secondary">{e.content}</Typography>
              </Box>
            ))}
          </Box>
        </Paper>
      ))}
    </Box>
  );
}

// ==================== Plot Tab ====================
