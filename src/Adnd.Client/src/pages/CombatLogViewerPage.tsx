import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useCombats, useCombat } from '../api/gameHooks';
import {
  Box, Typography, Paper, Chip, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Button, IconButton, Collapse, Grid,
} from '@mui/material';
import {
  History as HistoryIcon, Replay as RefreshIcon,
  ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon,
  People as PeopleIcon, Event as EventIcon,
  Timeline as TimelineIcon,
  AccessTime as TimeIcon, FilterList as FilterIcon,
} from '@mui/icons-material';

export default function CombatLogViewerPage() {
  const { id: gameId } = useParams<{ id: string }>();
  const { combats, isLoading, refetch } = useCombats(gameId);
  const [selectedCombat, setSelectedCombat] = useState<string | null>(null);
  const { combat, refetch: refetchCombat } = useCombat(selectedCombat || undefined, gameId);
  const [showFilter, setShowFilter] = useState(false);
  const [filterStatus, setFilterStatus] = useState('');

  useEffect(() => {
    refetch();
  }, [gameId]);

  useEffect(() => {
    if (selectedCombat) refetchCombat();
  }, [selectedCombat, gameId]);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Active': return 'success';
      case 'Paused': return 'warning';
      case 'Finished': return 'default';
      default: return 'default';
    }
  };

  const getEventColor = (type: string) => {
    switch (type) {
      case 'Damage': return 'error.main'; case 'Healing': return 'success.main';
      case 'Death': return '#8B0000'; case 'Revival': return '#FFD700';
      case 'Condition': return 'primary.main'; case 'Attack': return 'warning.main';
      case 'CombatStart': return 'success.main'; case 'CombatEnd': return 'info.main';
      default: return 'text.secondary';
    }
  };

  const getEventIcon = (type: string) => {
    switch (type) {
      case 'CombatStart': return '🎯'; case 'CombatEnd': return '🏁'; case 'TurnChange': return '🔄';
      case 'Attack': return '⚔️'; case 'Damage': return '💥'; case 'Healing': return '💚';
      case 'Condition': return '🔮'; case 'SaveThrow': return '🛡️'; case 'Initiative': return '🎲';
      case 'Death': return '💀'; case 'Revival': return '✨'; case 'RoundStart': return '📢';
      case 'DeathSave': return '☠️'; case 'RoundEnd': return '⏰';
      default: return '•';
    }
  };

  const filteredCombats = filterStatus
    ? combats.filter(c => c.status === filterStatus)
    : combats;

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <HistoryIcon color="primary" fontSize="large" />
          <Typography variant="h5">Combat Log Viewer</Typography>
          <Chip label={`${filteredCombats.length} combats`} size="small" variant="outlined" />
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button size="small" variant="outlined" startIcon={<FilterIcon />} onClick={() => setShowFilter(!showFilter)}>
            Filter
          </Button>
          <Button size="small" variant="outlined" startIcon={<RefreshIcon />} onClick={() => { refetch(); if (selectedCombat) refetchCombat(); }}>
            Refresh
          </Button>
        </Box>
      </Box>

      {/* Filter Panel */}
      <Collapse in={showFilter}>
        <Box sx={{ p: 2, mb: 2, display: 'flex', gap: 2, alignItems: 'center', bgcolor: 'background.default', borderRadius: 1 }}>
          <Typography variant="subtitle2">Filter by status:</Typography>
          <Chip label="All" size="small" clickable={!filterStatus} onClick={() => setFilterStatus('')} color={!filterStatus ? 'primary' : 'default'} variant={!filterStatus ? 'filled' : 'outlined'} />
          <Chip label="Active" size="small" clickable={filterStatus === 'Active'} onClick={() => setFilterStatus('Active')} color={filterStatus === 'Active' ? 'success' : 'default'} variant={filterStatus === 'Active' ? 'filled' : 'outlined'} />
          <Chip label="Paused" size="small" clickable={filterStatus === 'Paused'} onClick={() => setFilterStatus('Paused')} color={filterStatus === 'Paused' ? 'warning' : 'default'} variant={filterStatus === 'Paused' ? 'filled' : 'outlined'} />
          <Chip label="Finished" size="small" clickable={filterStatus === 'Finished'} onClick={() => setFilterStatus('Finished')} color={filterStatus === 'Finished' ? 'default' : 'default'} variant={filterStatus === 'Finished' ? 'filled' : 'outlined'} />
        </Box>
      </Collapse>

      <Grid container spacing={2}>
        {/* Combat List */}
        <Grid size={{ xs: 12, lg: 4 }}>
          <Paper sx={{ maxHeight: '75vh', overflow: 'auto' }}>
            <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Typography variant="subtitle2" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                <TimelineIcon fontSize="small" /> Combats
              </Typography>
              <Chip label={`${filteredCombats.length}`} size="small" variant="outlined" />
            </Box>
            {isLoading ? (
              <Box sx={{ p: 2, textAlign: 'center' }}>
                <Typography color="text.secondary">Loading...</Typography>
              </Box>
            ) : filteredCombats.length === 0 ? (
              <Box sx={{ p: 4, textAlign: 'center' }}>
                <Typography variant="body2" color="text.secondary">No combats found.</Typography>
              </Box>
            ) : (
              filteredCombats.map(combat => (
                <Box
                  key={combat.id}
                  sx={{
                    p: 1.5,
                    borderBottom: 1,
                    borderColor: 'divider',
                    cursor: 'pointer',
                    bgcolor: selectedCombat === combat.id ? 'action.selected' : 'inherit',
                    '&:hover': { bgcolor: 'action.hover' },
                  }}
                  onClick={() => setSelectedCombat(combat.id)}
                >
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Typography variant="body2" sx={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {combat.name}
                    </Typography>
                    <Chip label={combat.status} size="small" color={getStatusColor(combat.status) as any} variant="outlined" />
                  </Box>
                  <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                    <Chip icon={<PeopleIcon fontSize="small" />} label={`${combat.participantCount} participants`} size="small" variant="filled" color="default" sx={{ height: 18, fontSize: 10 }} />
                    <Chip icon={<EventIcon fontSize="small" />} label={`${combat.eventCount} events`} size="small" variant="filled" color="default" sx={{ height: 18, fontSize: 10 }} />
                    <Chip icon={<TimeIcon fontSize="small" />} label={new Date(combat.startedAt).toLocaleDateString()} size="small" variant="filled" color="default" sx={{ height: 18, fontSize: 10 }} />
                  </Box>
                  {combat.endedAt && (
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                      Ended: {new Date(combat.endedAt).toLocaleString()}
                    </Typography>
                  )}
                  {combat.sessionTitle && (
                    <Typography variant="caption" color="text.secondary">
                      Session: {combat.sessionTitle}
                    </Typography>
                  )}
                </Box>
              ))
            )}
          </Paper>
        </Grid>

        {/* Combat Detail */}
        <Grid size={{ xs: 12, lg: 8 }}>
          {selectedCombat && combat ? (
            <CombatDetail combat={combat} getEventColor={getEventColor} getEventIcon={getEventIcon} />
          ) : (
            <Paper sx={{ p: 4, textAlign: 'center', height: '70vh', display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'center' }}>
              <HistoryIcon sx={{ fontSize: 64, color: 'text.disabled', mb: 2 }} />
              <Typography variant="h6" color="text.secondary">Select a combat to view its log</Typography>
              <Typography variant="body2" color="text.secondary">Choose a combat from the list on the left</Typography>
            </Paper>
          )}
        </Grid>
      </Grid>
    </Box>
  );
}

// ==================== Combat Detail Component ====================

function CombatDetail({ combat, getEventColor, getEventIcon }: {
  combat: any;
  getEventColor: (type: string) => string;
  getEventIcon: (type: string) => string;
}) {
  const [showParticipants, setShowParticipants] = useState(true);
  const [showLog, setShowLog] = useState(true);
  const [expandedEvent, setExpandedEvent] = useState<string | null>(null);

  const groupByRound = (evts: any[]) => {
    const groups: Record<number, any[]> = {};
    evts.forEach(e => {
      if (!groups[e.round]) groups[e.round] = [];
      groups[e.round].push(e);
    });
    return Object.entries(groups).sort(([a], [b]) => Number(a) - Number(b));
  };

  const groupedEvents = groupByRound(combat.events);

  return (
    <Paper sx={{ maxHeight: '75vh', overflow: 'auto' }}>
      {/* Combat Header */}
      <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider', bgcolor: 'background.default' }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
          <Typography variant="h6">{combat.name}</Typography>
          <Chip label={combat.status} size="small" color={combat.status === 'Active' ? 'success' : 'default'} />
        </Box>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          <Chip icon={<PeopleIcon fontSize="small" />} label={`${combat.participants.length} participants`} size="small" variant="outlined" />
          <Chip icon={<EventIcon fontSize="small" />} label={`${combat.events.length} events`} size="small" variant="outlined" />
          <Chip icon={<TimeIcon fontSize="small" />} label={`Started: ${new Date(combat.startedAt).toLocaleString()}`} size="small" variant="outlined" />
          {combat.endedAt && <Chip icon={<TimeIcon fontSize="small" />} label={`Ended: ${new Date(combat.endedAt).toLocaleString()}`} size="small" variant="outlined" />}
          <Chip label={`Round ${combat.currentRound}`} size="small" variant="outlined" />
        </Box>
      </Box>

      {/* Participants Toggle */}
      <Button size="small" startIcon={<PeopleIcon />} onClick={() => setShowParticipants(!showParticipants)} sx={{ m: 1 }}>
        {showParticipants ? 'Hide' : 'Show'} Participants
        {showParticipants ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
      </Button>

      <Collapse in={showParticipants}>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Participant</TableCell>
                <TableCell>HP</TableCell>
                <TableCell>AC</TableCell>
                <TableCell>Initiative</TableCell>
                <TableCell>Conditions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {combat.participants.map((p: any) => (
                <TableRow key={p.id}>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {p.displayName}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">{p.participantType}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ color: p.currentHP < p.maxHP * 0.3 ? 'error.main' : 'inherit' }}>
                      {p.currentHP}/{p.maxHP}
                    </Typography>
                  </TableCell>
                  <TableCell>{p.ac}</TableCell>
                  <TableCell>{p.initiative}</TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', gap: 0.25, flexWrap: 'wrap' }}>
                      {Array.isArray(p.conditions) && p.conditions.map((c: any, i: number) => (
                        <Chip key={i} label={c.name} size="small" variant="outlined" color="warning" sx={{ height: 18, fontSize: 10 }} />
                      ))}
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Collapse>

      {/* Combat Log Toggle */}
      <Button size="small" startIcon={<HistoryIcon />} onClick={() => setShowLog(!showLog)} sx={{ m: 1 }}>
        {showLog ? 'Hide' : 'Show'} Combat Log ({combat.events.length} events)
        {showLog ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
      </Button>

      <Collapse in={showLog}>
        <Box>
          {groupedEvents.map(([round, evts]) => (
            <Box key={round}>
              <Box sx={{ p: 0.5, px: 2, bgcolor: 'action.hover', position: 'sticky', top: 0, zIndex: 1 }}>
                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 'bold' }}>
                  Round {round} ({evts.length} events)
                </Typography>
              </Box>
              {evts.map(evt => (
                <Box key={evt.id} sx={{
                  p: 0.75, px: 2,
                  borderLeft: `3px solid ${getEventColor(evt.type)}`,
                  ml: 1, mr: 1, mb: 0.25,
                  bgcolor: evt.type === 'Death' ? 'rgba(139,0,0,0.05)' : evt.type === 'Healing' ? 'rgba(76,175,80,0.05)' : 'transparent',
                }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.25 }}>
                    <Typography variant="caption" color="text.secondary">{getEventIcon(evt.type)}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      T{evt.turnIndex} · {new Date(evt.createdAt).toLocaleTimeString()}
                    </Typography>
                    <Chip label={evt.type} size="small" variant="outlined" sx={{ height: 16, fontSize: 9 }} />
                    <IconButton size="small" onClick={() => setExpandedEvent(expandedEvent === evt.id ? null : evt.id)}>
                      {expandedEvent === evt.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                    </IconButton>
                  </Box>
                  <Typography variant="body2">
                    <strong>{evt.actorName}</strong>
                    {evt.targetName && <span> → <em>{evt.targetName}</em></span>}
                    : {evt.content}
                  </Typography>
                  <Collapse in={expandedEvent === evt.id}>
                    <Box sx={{ mt: 1, p: 1, bgcolor: 'background.default', borderRadius: 1 }}>
                      <Typography variant="caption" color="text.secondary">Metadata:</Typography>
                      <Typography variant="caption" sx={{ fontFamily: 'monospace', wordBreak: 'break-all' }}>
                        {evt.metadata ? JSON.stringify(evt.metadata, null, 2) : 'None'}
                      </Typography>
                    </Box>
                  </Collapse>
                </Box>
              ))}
            </Box>
          ))}
        </Box>
      </Collapse>
    </Paper>
  );
}
