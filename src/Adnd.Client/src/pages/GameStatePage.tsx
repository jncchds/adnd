import { useState, useEffect, useCallback } from 'react';
import { useParams } from 'react-router-dom';
import { useGameHub } from '../api/hubHook';
import { api } from '../api/client';
import { AgentType, AgentAction, MessageType } from '../types';
import {
  Box, Typography, Paper, Button, Chip,
  Divider, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Alert, LinearProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Collapse, CircularProgress,
} from '@mui/material';
import { AlertTitle, Avatar } from '@mui/material';
import {
  ExpandMore as ExpandMoreIcon,
  ExpandLess as ExpandLessIcon,
  Refresh as RefreshIcon,
  NotificationsActive as NotificationIcon,
  Event as EventIcon,
  People as PeopleIcon,
  SportsMartialArts as CombatIcon,
  EmojiEvents as TrophyIcon,
  Warning as WarningIcon,
  Lightbulb as LightbulbIcon,
  Speed as SpeedIcon,
  Dashboard as DashboardIcon,
  MenuBook as StoryIcon,
} from '@mui/icons-material';

// ==================== Main Component ====================

export default function GameStatePage() {
  const { id } = useParams<{ id: string }>();
  const [gameState, setGameState] = useState<GameStateData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({
    overview: true,
    combat: true,
    plot: true,
    agents: true,
    messages: true,
    llm: true,
    triggers: false,
  });
  const [triggerResult, setTriggerResult] = useState<any>(null);
  const [triggerLoading, setTriggerLoading] = useState<string | null>(null);
  const [reviewDialogOpen, setReviewDialogOpen] = useState(false);
  const [reviewContext, setReviewContext] = useState('');
  const [showAgentDialog, setShowAgentDialog] = useState(false);
  const [agentFrom, setAgentFrom] = useState(AgentType.GM);
  const [agentTo, setAgentTo] = useState(AgentType.LLM);
  const [agentAction, setAgentAction] = useState(AgentAction.Query);
  const [agentInput, setAgentInput] = useState('');
  useGameHub();

  const fetchGameState = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const data = await api.getGameState(id);
      setGameState(data as unknown as GameStateData);
    } catch (e: any) {
      setError(e instanceof Error ? e.message : 'Failed to load game state');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchGameState();
    // Auto-refresh every 15 seconds
    const interval = setInterval(fetchGameState, 15000);
    return () => clearInterval(interval);
  }, [fetchGameState]);

  const handleTrigger = async (_endpoint: string, label: string): Promise<void> => {
    if (!id) return;
    setTriggerLoading(label);
    setTriggerResult(null);
    try {
      let result: any;
      switch (label) {
        case 'Pause Game': result = await api.pauseGame(id); break;
        case 'Resume Game': result = await api.resumeGame(id); break;
        case 'Start Combat': result = await api.triggerCombatStart(id); break;
        case 'End Combat': result = await api.triggerCombatEnd(id); break;
        case 'Narrate': result = await api.triggerNarrate(id); break;
        case 'Suggest': result = await api.triggerSuggest(id); break;
        case 'Consistency Check': result = await api.triggerConsistency(id); break;
        case 'New Scene': result = await api.triggerNewScene(id); break;
        case 'GM Evaluate': result = await api.triggerGMEvaluate(id); break;
        case 'Plot Check': result = await api.triggerPlotCheck(id); break;
        case 'Detect Opportunities': result = await api.triggerDetectOpportunities(id); break;
        case 'Generate Threads': result = await api.triggerGenerateThreads(id); break;
        case 'Spawn Milestones': result = await api.triggerSpawnMilestones(id); break;
        case 'Session Summary': result = await api.triggerSessionSummary(id); break;
        default: result = null;
      }
      if (result) {
        setTriggerResult(result);
        setSuccess(`${label} triggered successfully`);
      }
    } catch (e: any) {
      setTriggerResult({ error: e.message });
    } finally {
      setTriggerLoading(null);
    }
  };


  const handleToggleSection = (section: string) => {
    setExpandedSections(prev => ({ ...prev, [section]: !prev[section] }));
  };

  const handleReview = async () => {
    if (!id) return;
    setTriggerLoading('FullReview');
    setTriggerResult(null);
    try {
      const result = await api.triggerFullReview(id, reviewContext);
      setTriggerResult(result);
      setSuccess('Full plot review triggered');
      setReviewDialogOpen(false);
      setReviewContext('');
      await fetchGameState();
    } catch (e: any) {
      setTriggerResult({ error: e.message });
    } finally {
      setTriggerLoading(null);
    }
  };

  const handleCreateAgentCall = async () => {
    if (!id) return;
    setTriggerLoading('AgentCall');
    setTriggerResult(null);
    try {
      const result = await api.createAgentCall(id, agentFrom, agentTo, agentAction, agentInput || undefined);
      setTriggerResult(result);
      setSuccess('Agent call created');
      setShowAgentDialog(false);
      setAgentInput('');
      await fetchGameState();
    } catch (e: any) {
      setTriggerResult({ error: e.message });
    } finally {
      setTriggerLoading(null);
    }
  };

  if (loading && !gameState) {
    return <Box sx={{ textAlign: 'center', mt: 8 }}><CircularProgress /><Typography sx={{ mt: 2 }}>Loading game state...</Typography></Box>;
  }

  return (
    <Box>
      {/* Top Bar */}
      <Box sx={{ mb: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <DashboardIcon color="primary" />
          <Typography variant="h5" fontWeight="bold">Game State Dashboard</Typography>
          {gameState?.Game && (
            <Chip label={gameState!.Game.gmStatus} size="small"
              color={gameState!.Game.gmStatus === 'Running' ? 'success' : gameState!.Game.gmStatus === 'Paused' ? 'warning' : 'default'}
              sx={{ ml: 1 }} />
          )}
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button size="small" variant="outlined" startIcon={<RefreshIcon />} onClick={fetchGameState} disabled={loading}>
            Refresh
          </Button>
          <Button size="small" variant="outlined" onClick={() => setExpandedSections(prev => ({ ...prev, ...Object.fromEntries(Object.keys(prev).map(k => [k, true])) }))}>
            Expand All
          </Button>
          <Button size="small" variant="outlined" onClick={() => setExpandedSections(prev => ({ ...prev, ...Object.fromEntries(Object.keys(prev).map(k => [k, false])) }))}>
            Collapse All
          </Button>
        </Box>
      </Box>

      {/* Alerts */}
      {error && <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>{error}</Alert>}
      {success && <Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2 }}>{success}</Alert>}
      {triggerResult && !triggerResult.error && (
        <Alert severity="info" sx={{ mb: 2 }}>
          <AlertTitle>Trigger Result</AlertTitle>
          <pre style={{ margin: 0, whiteSpace: 'pre-wrap', fontSize: 12 }}>{JSON.stringify(triggerResult, null, 2)}</pre>
        </Alert>
      )}
      {triggerResult?.error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          <AlertTitle>Trigger Error</AlertTitle>
          {triggerResult.error}
        </Alert>
      )}

      {/* Single Dashboard Layout */}
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <OverviewTab gameState={gameState} expandedSections={expandedSections} onToggleSection={handleToggleSection} />
        <CombatTab gameState={gameState} expandedSections={expandedSections} onToggleSection={handleToggleSection} />
        <PlotTab gameState={gameState} expandedSections={expandedSections} onToggleSection={handleToggleSection} />
        <AgentTab gameState={gameState} onOpenAgentDialog={() => setShowAgentDialog(true)} onRefresh={fetchGameState} />
        <MessagesTab gameState={gameState} />
        <LLMTab gameState={gameState} />
        <SectionCard title="Manual Triggers" icon={<LightbulbIcon />} expanded={expandedSections.triggers} onToggle={() => handleToggleSection('triggers')}>
          <TriggersTab
            gameState={gameState}
            onTrigger={handleTrigger}
            loading={triggerLoading}
            onOpenReview={() => setReviewDialogOpen(true)}
            onOpenAgent={() => setShowAgentDialog(true)}
          />
        </SectionCard>
      </Box>

      {/* Review Dialog */}
      <Dialog open={reviewDialogOpen} onClose={() => setReviewDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Full Plot Review</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            The LLM will review all active plot threads in light of recent game events and adapt them accordingly.
            This may generate new threads, spawn milestones, or adjust momentum.
          </Typography>
          <TextField
            fullWidth
            multiline
            rows={3}
            label="Additional Context (optional)"
            value={reviewContext}
            onChange={e => setReviewContext(e.target.value)}
            placeholder="e.g., Players just discovered a major betrayal by their ally..."
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReviewDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleReview} variant="contained" disabled={triggerLoading === 'FullReview'}>
            {triggerLoading === 'FullReview' ? 'Reviewing...' : 'Review'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Agent Call Dialog */}
      <Dialog open={showAgentDialog} onClose={() => setShowAgentDialog(false)} maxWidth="md" fullWidth>
        <DialogTitle>New Agent Call</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField fullWidth select size="small" label="From Agent" value={agentFrom}
              onChange={e => setAgentFrom(Number(e.target.value))}
              SelectProps={{ native: true }}>
              {Object.entries(AgentType).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
                <option key={name} value={val}>{getAgentLabel(Number(val))}</option>
              ))}
            </TextField>
            <TextField fullWidth select size="small" label="To Agent" value={agentTo}
              onChange={e => setAgentTo(Number(e.target.value))}
              SelectProps={{ native: true }}>
              {Object.entries(AgentType).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
                <option key={name} value={val}>{getAgentLabel(Number(val))}</option>
              ))}
            </TextField>
            <TextField fullWidth select size="small" label="Action" value={agentAction}
              onChange={e => setAgentAction(Number(e.target.value))}
              SelectProps={{ native: true }}>
              {Object.entries(AgentAction).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
                <option key={name} value={Number(val)}>{name}</option>
              ))}
            </TextField>
          </Box>
          <TextField
            fullWidth
            label="Input (JSON)"
            multiline
            rows={4}
            value={agentInput}
            onChange={e => setAgentInput(e.target.value)}
            placeholder='{"systemPrompt": "...", "userPrompt": "..."}'
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowAgentDialog(false)}>Cancel</Button>
          <Button onClick={handleCreateAgentCall} variant="contained" disabled={triggerLoading === 'AgentCall'}>
            {triggerLoading === 'AgentCall' ? 'Creating...' : 'Execute'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

// ==================== Overview Tab ====================

function OverviewTab({ gameState, expandedSections, onToggleSection }: any) {
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

// ==================== Combat Tab ====================

function CombatTab({ gameState }: any) {
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

function PlotTab({ gameState }: any) {
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

function AgentTab({ gameState, onOpenAgentDialog }: any) {
  if (!gameState?.AgentCalls) return <Typography>No agent data available.</Typography>;
  const a = gameState.AgentCalls;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Agent Calls</Typography>
        <Button size="small" variant="contained" onClick={onOpenAgentDialog}>New Agent Call</Button>
      </Box>

      {/* Pending Calls */}
      <SectionCard title={`Pending Calls (${a.Pending})`} icon={<NotificationIcon />} expanded={true} onToggle={() => {}}>
        {a.PendingList.length === 0 ? (
          <Typography color="text.secondary">No pending calls</Typography>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>ID</TableCell>
                  <TableCell>From</TableCell>
                  <TableCell>To</TableCell>
                  <TableCell>Action</TableCell>
                  <TableCell>Input Preview</TableCell>
                  <TableCell>Created</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {a.PendingList.map((c: any) => (
                  <TableRow key={c.id}>
                    <TableCell sx={{ fontFamily: 'monospace', fontSize: 11 }}>{c.id.substring(0, 8)}...</TableCell>
                    <TableCell>{getAgentLabel(c.fromAgent)}</TableCell>
                    <TableCell>{getAgentLabel(c.toAgent)}</TableCell>
                    <TableCell>{getActionLabel(c.action)}</TableCell>
                    <TableCell sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {c.input ? (c.input.length > 80 ? c.input.substring(0, 80) + '...' : c.input) : '—'}
                    </TableCell>
                    <TableCell>{new Date(c.createdAt).toLocaleTimeString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </SectionCard>

      {/* Running Calls */}
      <SectionCard title={`Running Calls (${a.Running})`} icon={<SpeedIcon />} expanded={true} onToggle={() => {}}>
        {a.RunningList.length === 0 ? (
          <Typography color="text.secondary">No running calls</Typography>
        ) : (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>ID</TableCell>
                  <TableCell>From</TableCell>
                  <TableCell>To</TableCell>
                  <TableCell>Action</TableCell>
                  <TableCell>Started</TableCell>
                  <TableCell>Duration</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {a.RunningList.map((c: any) => (
                  <TableRow key={c.id}>
                    <TableCell sx={{ fontFamily: 'monospace', fontSize: 11 }}>{c.id.substring(0, 8)}...</TableCell>
                    <TableCell>{getAgentLabel(c.fromAgent)}</TableCell>
                    <TableCell>{getAgentLabel(c.toAgent)}</TableCell>
                    <TableCell>{getActionLabel(c.action)}</TableCell>
                    <TableCell>{c.startedAt ? new Date(c.startedAt).toLocaleTimeString() : '—'}</TableCell>
                    <TableCell>{c.durationMs}ms</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </SectionCard>
    </Box>
  );
}

// ==================== Messages Tab ====================

function MessagesTab({ gameState }: any) {
  if (!gameState?.RecentMessages) return <Typography>No message data available.</Typography>;

  const getMessageIcon = (type: number) => {
    switch (type) {
      case 0: return '💬'; case 1: return '🤫'; case 2: return '📢'; case 3: return '🤫';
      case 5: return '🎲'; case 4: return '⚔️'; case 7: return '🧙';
      case 8: return '🤖'; case 9: return '📡'; case 86: return '📖'; case 93: return '🔧';
      case 20: return '⚔️'; case 21: return '🏁'; case 60: return '👤'; case 61: return '👋';
      case 62: return '🔌'; case 63: return '✅';
      default: return '📝';
    }
  };

  const getMessageColor = (type: number): 'primary' | 'info' | 'default' | 'warning' | 'error' | 'success' | 'secondary' => {
    switch (type) {
      case 0: return 'primary'; case 1: return 'info'; case 2: return 'default'; case 3: return 'default';
      case 5: return 'warning'; case 4: return 'error'; case 7: return 'success';
      case 8: return 'info'; case 9: return 'info'; case 86: return 'primary'; case 93: return 'secondary';
      default: return 'default';
    }
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
      <Typography variant="h6">Recent Messages ({gameState.RecentMessages.length})</Typography>
      {gameState.RecentMessages.length === 0 ? (
        <Typography color="text.secondary">No messages yet.</Typography>
      ) : (
        <Box sx={{ maxHeight: 500, overflow: 'auto', bgcolor: 'background.default', p: 1, borderRadius: 1 }}>
          {[...gameState.RecentMessages].reverse().map((m: any) => (
            <Box key={m.id} sx={{
              p: 1, mb: 0.5, borderRadius: 1,
              borderLeft: `3px solid`,
              borderColor: getMessageColor(m.type),
              bgcolor: m.isOOC ? 'action.hover' : 'background.paper',
            }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                <Typography variant="caption">{getMessageIcon(m.type)}</Typography>
                <Chip label={m.type} size="small" color={getMessageColor(m.type) as any} variant="outlined" />
                <Typography variant="caption" fontWeight="bold">{m.playerName}</Typography>
                {m.isOOC && <Chip label="OOC" size="small" color="default" variant="filled" />}
                <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
                  {new Date(m.createdAt).toLocaleString()}
                </Typography>
              </Box>
              <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: m.type === MessageType.Dice ? 'monospace' : 'inherit' }}>
                {m.content}
              </Typography>
            </Box>
          ))}
        </Box>
      )}
    </Box>
  );
}

// ==================== LLM Tab ====================

function LLMTab({ gameState }: any) {
  if (!gameState?.LLMStats) return <Typography>No LLM data available.</Typography>;
  const s = gameState.LLMStats;
  const successRate = s.TotalCalls > 0 ? (s.SuccessfulCalls / s.TotalCalls * 100) : 0;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography variant="h6">LLM Usage</Typography>

      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Total Calls" value={s.TotalCalls} />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Successful" value={s.SuccessfulCalls} sub={`${successRate.toFixed(1)}% success rate`} color="success" />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Failed" value={s.FailedCalls} sub={`${(100 - successRate).toFixed(1)}% failure rate`} color="error" />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Total Tokens" value={formatTokens(s.TotalTokens)} />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Prompt Tokens" value={formatTokens(s.TotalPromptTokens)} />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Completion Tokens" value={formatTokens(s.TotalCompletionTokens)} />
        </Box>
        <Box sx={{ flex: '1 1 calc(14.285% - 8px)', minWidth: 100 }}>
          <StatCard label="Avg Duration" value={`${Math.round(s.AvgDurationMs)}ms`} />
        </Box>
      </Box>

      {/* Provider Breakdown */}
      {gameState.LLMProviderBreakdown && gameState.LLMProviderBreakdown.length > 0 && (
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ mb: 1 }}>Provider Breakdown</Typography>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Provider</TableCell>
                  <TableCell>Model</TableCell>
                  <TableCell>Calls</TableCell>
                  <TableCell>Success Rate</TableCell>
                  <TableCell>Tokens</TableCell>
                  <TableCell>Avg Duration</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {gameState.LLMProviderBreakdown.map((p: any) => (
                  <TableRow key={p.providerType + p.model}>
                    <TableCell>{getProviderIcon(p.providerType)} {p.providerType}</TableCell>
                    <TableCell sx={{ fontFamily: 'monospace', fontSize: 11 }}>{p.model}</TableCell>
                    <TableCell>{p.totalCalls}</TableCell>
                    <TableCell>
                      <Chip label={`${(p.successRate * 100).toFixed(0)}%`} size="small"
                        color={p.successRate > 0.8 ? 'success' : p.successRate > 0.5 ? 'warning' : 'error'} />
                    </TableCell>
                    <TableCell>{formatTokens(p.totalTokens)}</TableCell>
                    <TableCell>{Math.round(p.avgDurationMs)}ms</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      )}
    </Box>
  );
}

// ==================== Triggers Tab ====================

function TriggersTab({ gameState, onTrigger, loading, onOpenReview, onOpenAgent }: any) {
  if (!gameState?.Game) return null;
  const isRunning = gameState!.Game.gmStatus === 'Running';

  const triggerGroups = [
    {
      title: '⚙️ System Simulation',
      description: 'Trigger core game lifecycle events manually',
      triggers: [
        { label: 'Pause Game', endpoint: 'pause', desc: 'Publish GamePaused event' },
        { label: 'Resume Game', endpoint: 'resume', desc: 'Publish GameResumed event' },
        { label: 'Start Combat', endpoint: 'combat-start', desc: 'Create dummy combat and publish CombatStarted' },
        { label: 'End Combat', endpoint: 'combat-end', desc: 'End latest active combat and publish CombatEnded' },
      ],
    },
    {
      title: '📖 Narrative',
      description: 'Trigger the GM agent to generate narrative content',
      triggers: [
        { label: 'Narrate', endpoint: 'narrate', desc: 'Generate a narrative continuation' },
        { label: 'New Scene', endpoint: 'new-scene', desc: 'Create a new scene' },
        { label: 'GM Evaluate', endpoint: 'gm-evaluate', desc: 'Evaluate current game state' },
      ],
    },
    {
      title: '🤖 Suggestions',
      description: 'Get AI suggestions and plot ideas',
      triggers: [
        { label: 'Suggest', endpoint: 'suggest', desc: 'Get plot continuation suggestions' },
        { label: 'Detect Opportunities', endpoint: 'detect-opportunities', desc: 'Find story opportunities' },
        { label: 'Plot Check', endpoint: 'plot-check', desc: 'Check for plot opportunities' },
      ],
    },
    {
      title: '📊 Plot Management',
      description: 'Manage plot threads and milestones',
      triggers: [
        { label: 'Generate Threads', endpoint: 'generate-threads', desc: 'Generate new plot threads' },
        { label: 'Spawn Milestones', endpoint: 'spawn-milestones', desc: 'Spawn milestones for high-momentum threads' },
        { label: 'Full Review', endpoint: '', desc: 'Review all plot threads', action: onOpenReview },
      ],
    },
    {
      title: '🔍 Consistency & RAG',
      description: 'Check consistency and generate summaries',
      triggers: [
        { label: 'Consistency Check', endpoint: 'consistency', desc: 'Check plot consistency' },
        { label: 'Session Summary', endpoint: 'session-summary', desc: 'Generate session summary' },
      ],
    },
    {
      title: '🤖 Agent Framework',
      description: 'Manually create agent calls',
      triggers: [
        { label: 'New Agent Call', endpoint: '', desc: 'Create a custom agent call', action: onOpenAgent },
      ],
    },
  ];

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Manual Event Triggers</Typography>
        <Chip label={isRunning ? '✅ GM Agent Running' : '⚠️ GM Agent Not Running'}
          size="small"
          color={isRunning ? 'success' : 'warning'}
          variant={isRunning ? 'filled' : 'outlined'} />
      </Box>

      {!isRunning && (
        <Alert severity="warning" icon={<WarningIcon />}>
          GM agent is not running. Most triggers require the GM agent to be in 'Running' state.
          Start the game or resume the GM agent first.
        </Alert>
      )}

      {triggerGroups.map((group, gi) => (
        <Paper key={gi} sx={{ p: 2 }}>
          <Typography variant="subtitle1" sx={{ mb: 0.5 }}>{group.title}</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>{group.description}</Typography>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {group.triggers.map((t: any, ti: number) => (
              <Button
                key={ti}
                size="small"
                variant="outlined"
                disabled={!isRunning && !t.action}
                onClick={() => t.action ? t.action() : onTrigger(t.endpoint, t.label)}
                startIcon={loading === t.label ? <CircularProgress size={16} /> : undefined}
                sx={{ textTransform: 'none' }}
              >
                {t.label}
                {t.desc && <Typography variant="caption" sx={{ ml: 0.5, opacity: 0.7 }}>{t.desc}</Typography>}
              </Button>
            ))}
          </Box>
        </Paper>
      ))}
    </Box>
  );
}

// ==================== Helpers ====================

function getAgentLabel(agent: number): string {
  switch (agent) {
    case AgentType.Creator: return '👤 Creator';
    case AgentType.GM: return '🤖 AI-GM';
    case AgentType.LLM: return '🤖 LLM';
    case AgentType.Dice: return '🎲 Dice';
    case AgentType.RAG: return '📚 RAG';
    case AgentType.NPC: return '🧙 NPC';
    case AgentType.Player: return '👤 Player';
    case AgentType.System: return '⚙️ System';
    default: return '❓ Unknown';
  }
}

function getActionLabel(action: number): string {
  switch (action) {
    case AgentAction.Query: return 'Query';
    case AgentAction.Generate: return 'Generate';
    case AgentAction.Roll: return 'Roll';
    case AgentAction.Check: return 'Check';
    case AgentAction.Narrate: return 'Narrate';
    case AgentAction.Suggest: return 'Suggest';
    case AgentAction.Execute: return 'Execute';
    case AgentAction.Notify: return 'Notify';
    case AgentAction.Recall: return 'Recall';
    case AgentAction.ManageState: return 'State';
    case AgentAction.Nudge: return 'Nudge';
    case AgentAction.CreateCharacter: return 'CreateChar';
    default: return 'Unknown';
  }
}

function getRoleIcon(role: string): string {
  switch (role) {
    case 'Creator': return '👑';
    case 'Player': return '👤';
    case 'Spectator': return '👁️';
    case 'Observer': return '🔍';
    default: return '❓';
  }
}

function getProviderIcon(provider: string): string {
  switch (provider) {
    case 'ollama': return '🦙';
    case 'lmstudio': return '🏠';
    case 'openai': return '🔵';
    case 'google': return '🟢';
    default: return '🤖';
  }
}

function formatTokens(tokens: number): string {
  if (!tokens) return '0';
  if (tokens >= 1000000) return `${(tokens / 1000000).toFixed(1)}M`;
  if (tokens >= 1000) return `${(tokens / 1000).toFixed(1)}k`;
  return tokens.toString();
}

function parseConditions(conditions: any): any[] {
  if (!conditions) return [];
  try {
    return typeof conditions === 'string' ? JSON.parse(conditions) : conditions;
  } catch {
    return [];
  }
}

// ==================== Sub-components ====================

interface SectionCardProps {
  title: string;
  icon?: React.ReactNode;
  expanded: boolean;
  onToggle: () => void;
  children: React.ReactNode;
}

function SectionCard({ title, icon, expanded, onToggle, children }: SectionCardProps) {
  return (
    <Paper>
      <Box sx={{
        p: 1.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        cursor: 'pointer', '&:hover': { bgcolor: 'action.hover' },
      }} onClick={onToggle}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          {icon}
          <Typography variant="subtitle1" fontWeight="bold">{title}</Typography>
        </Box>
        {expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
      </Box>
      <Collapse in={expanded}>
        <Divider />
        <Box sx={{ p: 2 }}>{children}</Box>
      </Collapse>
    </Paper>
  );
}

interface StatCardProps {
  label: string;
  value: React.ReactNode;
  sub?: string;
  color?: 'success' | 'error' | 'warning' | 'info' | 'default';
}

function StatCard({ label, value, sub }: StatCardProps) {
  return (
    <Paper sx={{ p: 1.5, bgcolor: 'background.default' }}>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>{label}</Typography>
      <Typography variant="h6" fontWeight="bold">{value}</Typography>
      {sub && <Typography variant="caption" color="text.secondary">{sub}</Typography>}
    </Paper>
  );
}

interface PlotThreadCardProps {
  thread: any;
}

function PlotThreadCard({ thread }: PlotThreadCardProps) {
  const getMomentumColor = (momentum: number) => {
    if (momentum >= 7) return '#f44336';
    if (momentum >= 4) return '#ff9800';
    if (momentum >= 1) return '#ffeb3b';
    if (momentum >= -2) return '#4caf50';
    return '#9e9e9e';
  };

  const getMomentumLabel = (momentum: number) => {
    if (momentum >= 7) return '🔥 Urgent';
    if (momentum >= 4) return '⚡ High';
    if (momentum >= 1) return '📈 Moderate';
    if (momentum >= -2) return '📉 Low';
    return '💤 Fading';
  };

  const categoryLabels: Record<number, string> = {
    0: 'General', 1: 'Faction', 2: 'Mystery', 3: 'Personal',
    4: 'Threat', 5: 'WorldEvent', 6: 'Relationship',
  };

  const categoryColors: Record<number, 'default' | 'primary' | 'secondary' | 'error' | 'warning' | 'success' | 'info'> = {
    0: 'default', 1: 'primary', 2: 'info', 3: 'success',
    4: 'error', 5: 'warning', 6: 'secondary',
  };

  return (
    <Paper sx={{ p: 2, borderLeft: `4px solid ${getMomentumColor(thread.momentum)}` }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="body1" fontWeight="bold">{thread.title}</Typography>
          <Chip label={categoryLabels[thread.category] || 'General'} size="small" color={categoryColors[thread.category] || 'default'} variant="outlined" />
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Chip label={thread.status} size="small"
            color={thread.status === 'Active' ? 'success' : thread.status === 'Resolved' ? 'default' : 'error'}
            variant={thread.status === 'Active' ? 'filled' : 'outlined'} />
        </Box>
      </Box>

      {/* Momentum Bar */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <Typography variant="caption" color={getMomentumColor(thread.momentum)} sx={{ minWidth: 70 }}>
          {getMomentumLabel(thread.momentum)}
        </Typography>
        <Box sx={{ flex: 1 }}>
          <LinearProgress variant="determinate" value={(thread.momentum + 10) / 20 * 100}
            sx={{ height: 8, borderRadius: 4, bgcolor: 'background.paper',
              '& .MuiLinearProgress-bar': { bgcolor: getMomentumColor(thread.momentum), borderRadius: 4 } }} />
        </Box>
        <Typography variant="caption" fontWeight="bold" color={getMomentumColor(thread.momentum)}>
          {thread.momentum >= 0 ? '+' : ''}{thread.momentum.toFixed(1)}
        </Typography>
        <Chip label={`Relevance: ${(thread.relevanceScore * 100).toFixed(0)}%`} size="small" />
      </Box>

      {/* Description */}
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{thread.description}</Typography>

      {/* Details */}
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
        {thread.nextMilestone && (
          <Chip label={`Next: ${thread.nextMilestone}`} size="small" color="info" variant="outlined" />
        )}
        {thread.foreshadowing && (
          <Chip label={`Foreshadowing: ${thread.foreshadowing}`} size="small" color="warning" variant="outlined" />
        )}
        {thread.milestoneEvents && thread.milestoneEvents.length > 0 && (
          <Chip label={`${thread.milestoneEvents.length} milestones`} size="small" />
        )}
      </Box>

      {/* Adaptation History */}
      {thread.adaptationHistory && thread.adaptationHistory.length > 0 && (
        <Box sx={{ mt: 1 }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
            Last adaptation:
          </Typography>
          <Typography variant="caption" sx={{ display: 'block', color: 'text.secondary', fontStyle: 'italic' }}>
            {thread.adaptationHistory[thread.adaptationHistory.length - 1]}
          </Typography>
        </Box>
      )}

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
        Updated {thread.updatedAt ? new Date(thread.updatedAt).toLocaleString() : '—'}
      </Typography>
    </Paper>
  );
}

// ==================== Types ====================

interface GameStateData {
  Game: {
    id: string;
    name: string;
    systemId: string;
    systemVersion?: string;
    status: string;
    gmStatus: string;
    lastGMAction?: string;
    lastGMActionAt?: string;
    createdAt: string;
    startedAt?: string;
    language: string;
    plotSeed?: string;
    gameState?: string;
    gameParameters?: string;
    llmPresetId?: string;
    llmPresetName?: string;
    llmPresetProvider?: string;
    llmPresetModel?: string;
  };
  Players: Array<{
    id: string;
    characterName: string;
    role: string;
    status: string;
    joinedAt: string;
    leftAt?: string;
    userName?: string;
    userEmail?: string;
  }>;
  PlayerStats: {
    total: number;
    active: number;
    disconnected: number;
    left: number;
  };
  Sessions: Array<{
    id: string;
    title: string;
    description?: string;
    startedAt: string;
    endedAt?: string;
    messageCount: number;
  }>;
  SessionStats: {
    total: number;
    active: number;
    closed: number;
  };
  RecentMessages: Array<{
    id: string;
    sessionId: string;
    playerId?: string;
    playerName: string;
    content: string;
    type: number;
    isOOC: boolean;
    createdAt: string;
  }>;
  ActiveCombats: Array<{
    id: string;
    name?: string;
    status: string;
    currentRound: number;
    currentTurnIndex: number;
    startedAt: string;
    endedAt?: string;
    participants: Array<{
      id: string;
      displayName: string;
      participantType: string;
      currentHP: number;
      maxHP: number;
      ac: number;
      initiative: number;
      actionsRemaining: number;
      bonusActionsRemaining: number;
      reactionsRemaining: number;
      movementsRemaining: number;
      conditions: any;
    }>;
    events: Array<{
      id: string;
      round: number;
      turnIndex: number;
      type: string;
      actorName: string;
      targetName: string;
      content: string;
      createdAt: string;
    }>;
  }>;
  CombatStats: {
    active: number;
    total: number;
  };
  PlotThreads: Array<{
    id: string;
    title: string;
    category: number;
    description: string;
    status: string;
    momentum: number;
    relevanceScore: number;
    nextMilestone?: string;
    foreshadowing?: string;
    adaptationHistory: string[];
    milestoneEvents: any[];
    createdAt: string;
    updatedAt?: string;
  }>;
  PlotStats: {
    active: number;
    resolved: number;
    abandoned: number;
    recentReviews: number;
  };
  AgentCalls: {
    pending: number;
    pendingList: Array<{
      id: string;
      fromAgent: number;
      toAgent: number;
      action: number;
      input?: string;
      createdAt: string;
    }>;
    running: number;
    runningList: Array<{
      id: string;
      fromAgent: number;
      toAgent: number;
      action: number;
      input?: string;
      startedAt?: string;
      durationMs: number;
    }>;
  };
  PendingToolCalls: Array<{
    id: string;
    toolName: string;
    status: number;
    outputMessage?: string;
    requiresConfirmation: boolean;
    createdAt: string;
    arguments?: string;
  }>;
  LLMStats: {
    totalCalls: number;
    successfulCalls: number;
    failedCalls: number;
    totalTokens: number;
    totalPromptTokens: number;
    totalCompletionTokens: number;
    avgDurationMs: number;
  };
  LLMProviderBreakdown?: Array<{
    providerType: string;
    model: string;
    totalCalls: number;
    successfulCalls: number;
    failedCalls: number;
    successRate: number;
    totalTokens: number;
    avgDurationMs: number;
  }>;
  NPCs: {
    count: number;
    list: Array<{
      id: string;
      name: string;
      description?: string;
      attributes: any;
      skills: any;
      createdAt: string;
    }>;
  };
  Characters: {
    count: number;
    list: Array<{
      id: string;
      name: string;
      class: string;
      level: number;
      currentHP: number;
      maxHP: number;
      conditions: any;
      spells: any;
      updatedAt: string;
      playerName: string;
    }>;
  };
  GameState?: string;
  PlotSeed?: string;
  GameParameters?: string;
}
