import { useState } from 'react';
import { Box, Typography, Paper, IconButton, Collapse, Chip, Divider, Grid, Button } from '@mui/material';
import { ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon } from '@mui/icons-material';
import MarkdownRenderer from '../MarkdownRenderer';
import type { GameDetail } from '../../types/game.types';

interface OverviewTabProps {
  gameState: any;
  expandedSections: Record<string, boolean>;
  onToggleSection: (section: string) => void;
}

export default function OverviewTab({ gameState, expandedSections, onToggleSection }: OverviewTabProps) {
  if (!gameState?.Game) return <Typography>No game data available.</Typography>;
  const g = gameState!.Game;
  const ps = gameState!.PlayerStats;
  const ss = gameState!.SessionStats;
  const cs = gameState!.CombatStats;
  const pStats = gameState!.PlotStats;
  const aCalls = gameState!.AgentCalls;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {/* Game Overview */}
      <SectionCard title="Game Overview" icon={<DashboardIcon />} expanded={expandedSections.overview} onToggle={() => onToggleSection('overview')}>
        <Box sx={{ display: 'flex', gap: 2 }}>
          <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 1 }}>
            <StatCard label="Name" value={g.Name} />
            <StatCard label="System" value={`${g.SystemId} v${g.SystemVersion || 'unknown'}`} />
            <StatCard label="Language" value={g.Language} />
            <StatCard label="Status" value={
              <Chip label={g.Status} size="small" color={g.Status === 'Active' ? 'success' : g.Status === 'Draft' ? 'default' : 'warning'} />
            } />
            <StatCard label="GM Status" value={
              <Chip label={g.gmStatus} size="small"
                color={g.gmStatus === 'Running' ? 'success' : g.gmStatus === 'Paused' ? 'warning' : 'default'} />
            } />
          </Box>
          <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 1 }}>
            <StatCard label="Created" value={new Date(g.CreatedAt).toLocaleString()} />
            <StatCard label="Started" value={g.StartedAt ? new Date(g.StartedAt).toLocaleString() : '—'} />
            <StatCard label="Last GM Action" value={g.LastGMAction || '—'} />
            <StatCard label="Last Action At" value={g.LastGMActionAt ? new Date(g.LastGMActionAt).toLocaleString() : '—'} />
            <StatCard label="LLM Preset" value={g.llmPresetName || 'None'} />
            <StatCard label="Provider/Model" value={g.llmPresetProvider ? `${g.llmPresetProvider} / ${g.llmPresetModel}` : '—'} />
          </Box>
        </Box>
      </SectionCard>

      {/* Quick Stats */}
      <SectionCard title="Quick Stats" icon={<TrophyIcon />} expanded={expandedSections.quickStats} onToggle={() => onToggleSection('quickStats')}>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
          <Box sx={{ flex: '1 1 calc(16.666% - 8px)', minWidth: 120 }}>
            <StatCard label="Players" value={ps.Total} sub={`${ps.Active} active`} />
          </Box>
          <Box sx={{ flex: '1 1 calc(16.666% - 8px)', minWidth: 120 }}>
            <StatCard label="Disconnected" value={ps.Disconnected} sub="players" color={ps.Disconnected > 0 ? 'error' as const : 'success' as const} />
          </Box>
          <Box sx={{ flex: '1 1 calc(16.666% - 8px)', minWidth: 120 }}>
            <StatCard label="Sessions" value={ss.Total} sub={`${ss.Active} active`} />
          </Box>
          <Box sx={{ flex: '1 1 calc(16.666% - 8px)', minWidth: 120 }}>
            <StatCard label="Combats" value={cs.Active} sub={`${cs.Total} total`} color={cs.Active > 0 ? 'warning' as const : 'default' as const} />
          </Box>
          <Box sx={{ flex: '1 1 calc(16.666% - 8px)', minWidth: 120 }}>
            <StatCard label="Plot Threads" value={pStats.Active} sub={`${pStats.Resolved} resolved`} />
          </Box>
          <Box sx={{ flex: '1 1 calc(16.666% - 8px)', minWidth: 120 }}>
            <StatCard label="Pending Calls" value={aCalls.Pending} sub={`${aCalls.Running} running`} color={aCalls.Pending > 0 ? 'warning' as const : 'success' as const} />
          </Box>
        </Box>
      </SectionCard>

      {/* Players */}
      <SectionCard title="Players" icon={<PeopleIcon />} expanded={expandedSections.players} onToggle={() => onToggleSection('players')}>
        {gameState.Players.length === 0 ? (
          <Typography color="text.secondary">No players yet.</Typography>
        ) : (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            {gameState.Players.map((p: any) => (
              <Box key={p.id} sx={{ display: 'flex', alignItems: 'center', gap: 1, p: 1, bgcolor: 'background.default', borderRadius: 1 }}>
                <Chip label={getRoleIcon(p.role)} size="small" />
                <Typography variant="body2" fontWeight="bold">{p.characterName}</Typography>
                <Typography variant="caption" color="text.secondary">({p.userName || p.userEmail || 'anon'})</Typography>
                <Chip label={p.status} size="small"
                  color={p.status === 'Active' ? 'success' : p.status === 'Disconnected' ? 'warning' : 'default'}
                  variant={p.status === 'Active' ? 'filled' : 'outlined'} />
                <Typography variant="caption" color="text.secondary">joined {new Date(p.joinedAt).toLocaleString()}</Typography>
              </Box>
            ))}
          </Box>
        )}
      </SectionCard>

      {/* Sessions */}
      <SectionCard title="Sessions" icon={<EventIcon />} expanded={expandedSections.sessions} onToggle={() => onToggleSection('sessions')}>
        {gameState.Sessions.length === 0 ? (
          <Typography color="text.secondary">No sessions yet.</Typography>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Title</TableCell>
                  <TableCell>Started</TableCell>
                  <TableCell>Ended</TableCell>
                  <TableCell>Messages</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {gameState.Sessions.map((s: any) => (
                  <TableRow key={s.id}>
                    <TableCell>{s.title}</TableCell>
                    <TableCell>{new Date(s.startedAt).toLocaleString()}</TableCell>
                    <TableCell>{s.endedAt ? new Date(s.endedAt).toLocaleString() : '—'}</TableCell>
                    <TableCell>{s.messageCount}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </SectionCard>

      {/* Plot Threads */}
      <SectionCard title="Plot Threads" icon={<StoryIcon />} expanded={expandedSections.plot} onToggle={() => onToggleSection('plot')}>
        {gameState.PlotThreads.length === 0 ? (
          <Typography color="text.secondary">No active plot threads.</Typography>
        ) : (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            {gameState.PlotThreads.map((t: any) => (
              <PlotThreadCard key={t.id} thread={t} />
            ))}
          </Box>
        )}
        <Box sx={{ mt: 1, display: 'flex', gap: 1 }}>
          <Chip label={`${pStats.Active} active`} size="small" color="success" variant="outlined" />
          <Chip label={`${pStats.Resolved} resolved`} size="small" />
          <Chip label={`${pStats.Abandoned} abandoned`} size="small" color="error" />
          <Chip label={`${pStats.RecentReviews} recent reviews`} size="small" />
        </Box>
      </SectionCard>

      {/* NPCs */}
      <SectionCard title="NPCs" icon={<Avatar sx={{ bgcolor: 'secondary.main' }}>🧙</Avatar>} expanded={expandedSections.npcs} onToggle={() => onToggleSection('npcs')}>
        {gameState.NPCs?.Count === 0 ? (
          <Typography color="text.secondary">No NPCs yet.</Typography>
        ) : (
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {gameState.NPCs.List.map((npc: any) => (
              <Chip key={npc.id} label={npc.name} size="small"
                onClick={() => alert(`${npc.name}: ${npc.description || 'No description'}`)}
                sx={{ cursor: 'pointer' }} />
            ))}
          </Box>
        )}
      </SectionCard>

      {/* Characters */}
      <SectionCard title="Characters" icon={<Avatar sx={{ bgcolor: 'primary.main' }}>👤</Avatar>} expanded={expandedSections.characters} onToggle={() => onToggleSection('characters')}>
        {gameState.Characters?.Count === 0 ? (
          <Typography color="text.secondary">No characters yet.</Typography>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Name</TableCell>
                  <TableCell>Class</TableCell>
                  <TableCell>Level</TableCell>
                  <TableCell>HP</TableCell>
                  <TableCell>Conditions</TableCell>
                  <TableCell>Player</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {gameState.Characters.List.map((c: any) => {
                  const conditions = parseConditions(c.conditions);
                  return (
                    <TableRow key={c.id}>
                      <TableCell sx={{ fontWeight: 'bold' }}>{c.name}</TableCell>
                      <TableCell>{c.class}</TableCell>
                      <TableCell>{c.level}</TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                          <Box sx={{ flex: 1, width: 60 }}>
                            <LinearProgress variant="determinate" value={(c.currentHP / c.maxHP) * 100}
                              sx={{ height: 6, borderRadius: 3, bgcolor: 'background.paper',
                                '& .MuiLinearProgress-bar': { bgcolor: c.currentHP / c.maxHP < 0.3 ? 'error.main' : 'success.main' } }} />
                          </Box>
                          <Typography variant="caption">{c.currentHP}/{c.maxHP}</Typography>
                        </Box>
                      </TableCell>
                      <TableCell>
                        {conditions.length > 0 ? conditions.map((cond, i) => (
                          <Chip key={i} label={cond.name} size="small" color="warning" variant="outlined" sx={{ mr: 0.5, mb: 0.5 }} />
                        )) : <Typography variant="caption" color="text.secondary">None</Typography>}
                      </TableCell>
                      <TableCell>{c.playerName}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </SectionCard>
    </Box>
  );
}

