import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useDiceHistory, useSessions } from '../api/gameHooks';
import { useGameHub } from '../api/hubHook';
import {
  Box, Typography, Paper, Chip, Table, TableBody, TableCell,
  TableHead, TableRow, IconButton, Collapse, Button, Grid,
  MenuItem, Select, FormControl, InputLabel,
} from '@mui/material';
import {
  FilterList as FilterIcon, Replay as RefreshIcon,
  ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon,
  SportsEsports as DiceIcon, Person as PlayerIcon,
  Schedule as TimeIcon,
} from '@mui/icons-material';

export default function DiceHistoryTab() {
  const { id: gameId } = useParams<{ id: string }>();
  const { rolls, isLoading, refetch } = useDiceHistory(gameId);
  const { sessions } = useSessions(gameId);
  const { isConnected, invoke } = useGameHub();

  const [showFilter, setShowFilter] = useState(false);
  const [filterSession, setFilterSession] = useState('');
  const [filterLimit, setFilterLimit] = useState(100);
  const [sortBy, setSortBy] = useState<'desc' | 'asc'>('desc');
  const [expandedRoll, setExpandedRoll] = useState<string | null>(null);

  useEffect(() => {
    refetch({ limit: filterLimit, sortBy });
  }, [gameId, filterSession, filterLimit, sortBy]);

  const handleQuickRoll = async (formula: string) => {
    if (!isConnected || !gameId) return;
    try {
      await invoke('RollDice', filterSession || '', formula);
    } catch (e) {
      console.error('Failed to roll dice:', e);
    }
  };

  const getRollColor = (total: number | null, diceType: number | null) => {
    if (total === null || diceType === null) return 'text.secondary';
    const pct = total / diceType;
    if (pct >= 0.9) return '#4caf50'; // natural 20+
    if (pct >= 0.7) return '#8bc34a';
    if (pct >= 0.4) return '#ff9800';
    return '#f44336';
  };

  const getRollIcon = (total: number | null, diceType: number | null) => {
    if (total === null || diceType === null) return '';
    if (total === diceType) return '🎯'; // natural 1
    if (total === diceType * (parseInt(diceType.toString()) || 20)) return '💥'; // natural max
    return '';
  };

  return (
    <Paper sx={{ height: '70vh', display: 'flex', flexDirection: 'column' }}>
      {/* Header */}
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: 1, borderColor: 'divider' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <DiceIcon color="primary" />
          <Typography variant="h6">Dice History ({rolls.length} rolls)</Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button size="small" variant="outlined" startIcon={<FilterIcon />} onClick={() => setShowFilter(!showFilter)}>
            Filter
          </Button>
          <Button size="small" variant="outlined" startIcon={<RefreshIcon />} onClick={() => refetch({ limit: filterLimit, sortBy })}>
            Refresh
          </Button>
        </Box>
      </Box>

      {/* Filter Panel */}
      <Collapse in={showFilter}>
        <Box sx={{ p: 2, display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center', borderBottom: 1, borderColor: 'divider', bgcolor: 'background.default' }}>
          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel>Session</InputLabel>
            <Select value={filterSession} label="Session" onChange={e => setFilterSession(e.target.value)}>
              <MenuItem value="">All Sessions</MenuItem>
              {sessions.map(s => (
                <MenuItem key={s.id} value={s.id}>{s.title}</MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 100 }}>
            <InputLabel>Limit</InputLabel>
            <Select value={filterLimit} label="Limit" onChange={e => setFilterLimit(Number(e.target.value))}>
              <MenuItem value={25}>25</MenuItem>
              <MenuItem value={50}>50</MenuItem>
              <MenuItem value={100}>100</MenuItem>
              <MenuItem value={200}>200</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 100 }}>
            <InputLabel>Sort</InputLabel>
            <Select value={sortBy} label="Sort" onChange={e => setSortBy(e.target.value as 'asc' | 'desc')}>
              <MenuItem value="desc">Newest First</MenuItem>
              <MenuItem value="asc">Oldest First</MenuItem>
            </Select>
          </FormControl>
        </Box>
      </Collapse>

      {/* Quick Roll Bar */}
      <Box sx={{ p: 1.5, display: 'flex', gap: 0.5, flexWrap: 'wrap', borderBottom: 1, borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary" sx={{ mr: 1, alignSelf: 'center' }}>Quick Roll:</Typography>
        {['1d4', '1d6', '1d8', '1d10', '1d12', '1d20', '2d6', '2d10', '3d6', '4d6kh3', '1d100'].map(d => (
          <Chip
            key={d}
            label={d}
            size="small"
            clickable
            onClick={() => handleQuickRoll(d)}
            sx={{ fontSize: 11 }}
          />
        ))}
      </Box>

      {/* Dice History Table */}
      <Box sx={{ flex: 1, overflow: 'auto', p: 0 }}>
        {isLoading ? (
          <Box sx={{ p: 4, textAlign: 'center' }}>
            <Typography color="text.secondary">Loading dice history...</Typography>
          </Box>
        ) : rolls.length === 0 ? (
          <Box sx={{ p: 4, textAlign: 'center' }}>
            <Typography variant="body2" color="text.secondary">No dice rolls yet. Start playing to see your roll history!</Typography>
          </Box>
        ) : (
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                <TableCell sx={{ minWidth: 60 }}>Time</TableCell>
                <TableCell sx={{ minWidth: 80 }}>Roller</TableCell>
                <TableCell sx={{ minWidth: 100 }}>Formula</TableCell>
                <TableCell sx={{ minWidth: 60, textAlign: 'right' }}>Total</TableCell>
                <TableCell sx={{ minWidth: 120 }}>Individual Rolls</TableCell>
                <TableCell sx={{ minWidth: 80 }}>Modifier</TableCell>
                <TableCell sx={{ minWidth: 60, textAlign: 'right' }}>Action</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rolls.map(roll => (
                <TableRow key={roll.id} sx={{ '&:hover': { bgcolor: 'action.hover' } }}>
                  <TableCell>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      <TimeIcon fontSize="small" color="action" />
                      <Typography variant="caption">{new Date(roll.createdAt).toLocaleTimeString()}</Typography>
                    </Box>
                    {roll.sessionTitle && (
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                        {roll.sessionTitle}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      <PlayerIcon fontSize="small" color="action" />
                      <Typography variant="body2">
                        {roll.characterName || (roll.diceType && roll.diceType === 100 ? 'System' : 'Player')}
                      </Typography>
                    </Box>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontFamily: 'monospace', fontWeight: 600 }}>
                      {roll.formula}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      d{roll.diceType} × {roll.diceCount}
                    </Typography>
                  </TableCell>
                  <TableCell sx={{ textAlign: 'right' }}>
                    <Typography
                      variant="h6"
                      sx={{
                        color: getRollColor(roll.total, roll.diceType),
                        fontWeight: 700,
                        fontSize: 18,
                      }}
                    >
                      {getRollIcon(roll.total, roll.diceType)}
                      {roll.total ?? '?'}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>
                      [{roll.rolls.join(', ')}]
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={roll.modifier >= 0 ? `+${roll.modifier}` : String(roll.modifier)}
                      size="small"
                      color={roll.modifier > 0 ? 'success' : roll.modifier < 0 ? 'error' : 'default'}
                      variant="outlined"
                    />
                  </TableCell>
                  <TableCell sx={{ textAlign: 'right' }}>
                    <IconButton size="small" onClick={() => setExpandedRoll(expandedRoll === roll.id ? null : roll.id)}>
                      {expandedRoll === roll.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Box>

      {/* Expanded Roll Details */}
      {expandedRoll && (() => {
        const roll = rolls.find(r => r.id === expandedRoll);
        if (!roll) return null;
        return (
          <Collapse in={!!expandedRoll}>
            <Box sx={{ p: 2, bgcolor: 'background.default', borderTop: 1, borderColor: 'divider' }}>
              <Grid container spacing={2}>
                <Grid size={4}>
                  <Typography variant="caption" color="text.secondary">Formula</Typography>
                  <Typography variant="body2" sx={{ fontFamily: 'monospace' }}>{roll.formula}</Typography>
                </Grid>
                <Grid size={4}>
                  <Typography variant="caption" color="text.secondary">Individual Rolls</Typography>
                  <Typography variant="body2">{roll.rolls.join(', ')}</Typography>
                </Grid>
                <Grid size={4}>
                  <Typography variant="caption" color="text.secondary">Breakdown</Typography>
                  <Typography variant="body2">
                    [{roll.rolls.join(' + ')}] + {roll.modifier} = {roll.total}
                  </Typography>
                </Grid>
              </Grid>
            </Box>
          </Collapse>
        );
      })()}
    </Paper>
  );
}
