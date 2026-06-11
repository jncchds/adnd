import { Box, Typography, Paper, Chip, Divider } from '@mui/material';
import { History as HistoryIcon } from '@mui/icons-material';
import { useRef, useEffect } from 'react';
import type { CombatLogEvent } from '../../types/combat.types';

export default function CombatLogPanel({ events, showCombatLog }: {
  events: CombatLogEvent[]; showCombatLog: boolean;
}) {
  const logRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (showCombatLog && logRef.current) {
      logRef.current.scrollTop = logRef.current.scrollHeight;
    }
  }, [events, showCombatLog]);

  const getEventIcon = (type: string) => {
    switch (type) {
      case 'CombatStart': return '🎯'; case 'CombatEnd': return '🏁'; case 'TurnChange': return '🔄';
      case 'Attack': return '⚔️'; case 'Damage': return '💥'; case 'Healing': return '💚';
      case 'Condition': return '🔮'; case 'SaveThrow': return '🛡️'; case 'Initiative': return '🎲';
      case 'Death': return '💀'; case 'Revival': return '✨'; case 'RoundStart': return '📢';
      case 'DeathSave': return '☠️';
      default: return '•';
    }
  };

  const getEventColor = (type: string) => {
    switch (type) {
      case 'Damage': return 'error.main'; case 'Healing': return 'success.main';
      case 'Death': return '#8B0000'; case 'Revival': return '#FFD700';
      case 'Condition': return 'primary.main'; case 'Attack': return 'warning.main';
      default: return 'text.secondary';
    }
  };

  const groupByRound = (evts: CombatLogEvent[]) => {
    const groups: Record<number, CombatLogEvent[]> = {};
    evts.forEach(e => {
      if (!groups[e.round]) groups[e.round] = [];
      groups[e.round].push(e);
    });
    return Object.entries(groups).sort(([a], [b]) => Number(a) - Number(b));
  };

  const grouped = groupByRound(events);

  return (
    <Paper ref={logRef} sx={{ maxHeight: '70vh', overflow: 'auto' }}>
      <Box sx={{ p: 1.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center', position: 'sticky', top: 0, bgcolor: 'background.paper', zIndex: 1, borderBottom: 1, borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <HistoryIcon fontSize="small" /> Combat Log
        </Typography>
        <Chip label={`${events.length} events`} size="small" variant="outlined" />
      </Box>
      <Divider />
      {events.length === 0 ? (
        <Box sx={{ p: 4, textAlign: 'center' }}>
          <Typography variant="body2" color="text.secondary">No combat events yet. Start a combat to begin tracking.</Typography>
        </Box>
      ) : (
        <Box>
          {grouped.map(([round, evts]) => (
            <Box key={round}>
              <Box sx={{ p: 0.5, px: 2, bgcolor: 'action.hover', position: 'sticky', top: 36, zIndex: 1 }}>
                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 'bold' }}>
                  Round {round}
                </Typography>
              </Box>
              {evts.map((evt) => (
                <Box key={evt.id} sx={{
                  p: 0.75, px: 2,
                  borderLeft: `3px solid ${getEventColor(evt.type)}`,
                  ml: 1, mr: 1, mb: 0.25,
                  bgcolor: evt.type === 'Death' ? 'rgba(139,0,0,0.05)' : evt.type === 'Healing' ? 'rgba(76,175,80,0.05)' : 'transparent',
                }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.25 }}>
                    <Typography variant="caption" color="text.secondary">
                      {getEventIcon(evt.type)}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      T{evt.turnIndex} · {new Date(evt.createdAt).toLocaleTimeString()}
                    </Typography>
                  </Box>
                  <Typography variant="body2">
                    <strong>{evt.actorName}</strong>
                    {evt.targetName && <span> → <em>{evt.targetName}</em></span>}
                    : {evt.content}
                  </Typography>
                </Box>
              ))}
            </Box>
          ))}
        </Box>
      )}
    </Paper>
  );
}

