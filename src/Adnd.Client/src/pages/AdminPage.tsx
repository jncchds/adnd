import { useState } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { useGames, useGame, useNPCs, useCharacters, useConsistency } from '../api/hooks/useGame';
import { usePlotWeaver } from '../api/hooks/usePlot';
import { useGameHub } from '../api/hooks/useHub';
import { AgentType, AgentAction, AgentCallStatus } from '../types';
import CharacterCreateWizard from './CharacterCreateWizard';
import PlotBoardAdminTab from './PlotBoardAdminTab';
import GameStatePage from './GameStatePage';
import {
  Box, Typography, Paper,
  List, ListItem, ListItemText, ListItemAvatar, Avatar,
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Button, IconButton, Chip, Alert, AlertTitle,
  Divider, Card, CardContent, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Select, MenuItem
} from '@mui/material';
import { Delete as DeleteIcon, Add as AddIcon,
  CheckCircle as CheckCircleIcon,
  History as HistoryIcon,
  Article as SheetIcon,
  ChevronLeft as ChevronLeftIcon } from '@mui/icons-material';

export default function AdminPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { game, isLoading } = useGame(id);
  const { startGame: _startGame } = useGames();
  const { npcs, isLoading: npcsLoading, createNPC, updateNPC, deleteNPC } = useNPCs(id);
  const { characters, refetch: refetchCharacters } = useCharacters(id);
  const { report, isLoading: consistencyLoading, check } = useConsistency(id);
  const { threads: plotThreads, isLoading: plotThreadsLoading } = usePlotWeaver(id);
  const location = useLocation();

  // Derive view from URL path: /admin/:id, /admin/:id/plot-board, etc.
  const pathParts = location.pathname.split('/').filter(Boolean);
  // pathParts = ['admin', id, view?]  or  ['admin', id]
  const view = pathParts[2] || 'dashboard';

  const [npcDialogOpen, setNpcDialogOpen] = useState(false);
  const [npcName, setNpcName] = useState('');
  const [npcDesc, setNpcDesc] = useState('');
  const [errorState, setErrorState] = useState<string | null>(null);
  const [agentCalls, setAgentCalls] = useState<any[]>([]);
  const [agentCallFilter, setAgentCallFilter] = useState<number | undefined>(undefined);
  const [showAgentDialog, setShowAgentDialog] = useState(false);
  const [agentFrom, setAgentFrom] = useState(AgentType.GM);
  const [agentTo, setAgentTo] = useState(AgentType.LLM);
  const [agentAction, setAgentAction] = useState(AgentAction.Query);
  const [agentInput, setAgentInput] = useState('');
  const [showCharCreateWizard, setShowCharCreateWizard] = useState(false);
  const { invoke } = useGameHub();

  // LLM Interaction logs state
  const [llmLogs, setLlmLogs] = useState<any[]>([]);
  const [logsLoading, setLogsLoading] = useState(false);
  const [selectedLog, setSelectedLog] = useState<any>(null);
  const [showLogDetail, setShowLogDetail] = useState(false);
  const [logFilterProvider, setLogFilterProvider] = useState('');
  const [logFilterFrom, setLogFilterFrom] = useState('');
  const [logFilterTo, setLogFilterTo] = useState('');


  const handleCreateNPC = async () => {
    if (!npcName.trim()) return;
    try {
      await createNPC!({ name: npcName, description: npcDesc });
      setNpcDialogOpen(false);
      setNpcName('');
      setNpcDesc('');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleCheckConsistency = async () => {
    await check(50);
  };

  // ==================== LLM Interaction Log Handlers ====================

  const handleRefreshLLMLogs = async () => {
    if (!id) return;
    setLogsLoading(true);
    try {
      const params: any = { limit: 200 };
      if (logFilterProvider) params.providerType = logFilterProvider;
      if (logFilterFrom) params.from = logFilterFrom;
      if (logFilterTo) params.to = logFilterTo;
      const logs = await invoke('GetLLMInteractions', id, undefined, logFilterProvider, logFilterFrom || undefined, logFilterTo || undefined, 200);
      if (logs) setLlmLogs(logs);
    } catch (e) {
      console.error('Failed to fetch LLM logs', e);
    }
    setLogsLoading(false);
  };

  const handleDeleteLog = async (logId: string) => {
    try {
      await invoke('DeleteLLMInteraction', logId);
      setLlmLogs(prev => prev.filter(l => l.id !== logId));
    } catch (e) {
      console.error('Failed to delete log', e);
    }
  };

  const handleRefreshAgentCalls = async () => {
    if (!id) return;
    try {
      const calls = await invoke('GetAgentCallHistory', id, agentCallFilter, undefined, 50);
      if (calls) setAgentCalls(calls);
    } catch (e) {
      console.error('Failed to fetch agent calls', e);
    }
  };

  const handleCreateAgentCall = async () => {
    if (!id) return;
    try {
      await invoke('CallAgent', agentFrom, agentTo, agentAction, agentInput || undefined);
      setShowAgentDialog(false);
      setAgentInput('');
      await handleRefreshAgentCalls();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleDeleteAgentCall = async (callId: string) => {
    try {
      await invoke('GetAgentCall', callId); // Just verify it exists
      setAgentCalls(prev => prev.filter(c => c.id !== callId));
    } catch (e) {
      console.error('Failed to delete agent call', e);
    }
  };

  if (isLoading) {
    return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading...</Typography></Box>;
  }

  if (!game) {
    return <Box sx={{ textAlign: 'center', mt: 8 }}>
      <Typography variant="h5" color="error">Game not found</Typography>
      <Button onClick={() => navigate('/dashboard')} sx={{ mt: 2 }}>Back to Dashboard</Button>
    </Box>;
  }

  return (
    <Box>
      {/* Error */}
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}

      {/* Back to Game */}
      <Box sx={{ mb: 1 }}>
        <Button
          size="small"
          variant="outlined"
          startIcon={<ChevronLeftIcon />}
          onClick={() => navigate(`/game/${id}`)}
        >
          Back to Game
        </Button>
      </Box>

      {/* Content */}
      {view === 'dashboard' && <GameStatePage />}
      {view === 'plot-board' && <PlotBoardAdminTab threads={plotThreads} isLoading={plotThreadsLoading} gameId={id || ''} />}
      {view === 'npcs' && (
        <NPCsTab npcs={npcs} npcsLoading={npcsLoading} onOpenDialog={() => setNpcDialogOpen(true)} onDelete={deleteNPC} onUpdate={updateNPC} />
      )}
      {view === 'characters' && <CharactersTab characters={characters} />}
      {view === 'consistency' && (
        <ConsistencyTab report={report} isLoading={consistencyLoading} onCheck={handleCheckConsistency} />
      )}
      {view === 'llm-logs' && (
        <LLMLogsTab logs={llmLogs} isLoading={logsLoading} onRefresh={handleRefreshLLMLogs} onOpenDetail={setSelectedLog} onDelete={handleDeleteLog} filterProvider={logFilterProvider} onFilterProviderChange={setLogFilterProvider} filterFrom={logFilterFrom} onFilterFromChange={setLogFilterFrom} filterTo={logFilterTo} onFilterToChange={setLogFilterTo} />
      )}
      {view === 'agent-calls' && (
        <AgentCallsTab calls={agentCalls} isLoading={false} onRefresh={handleRefreshAgentCalls} onOpenDialog={() => setShowAgentDialog(true)} onDelete={handleDeleteAgentCall} filter={agentCallFilter} onFilterChange={setAgentCallFilter} />
      )}

      {/* NPC Dialog */}
      <Dialog open={npcDialogOpen} onClose={() => setNpcDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>New NPC</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <TextField fullWidth label="Name" value={npcName} onChange={e => setNpcName(e.target.value)} sx={{ mb: 2 }} autoFocus />
          <TextField fullWidth label="Description" multiline rows={3} value={npcDesc} onChange={e => setNpcDesc(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setNpcDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleCreateNPC} variant="contained" disabled={!npcName.trim()}>Create</Button>
        </DialogActions>
      </Dialog>

      {/* Agent Call Dialog */}
      <Dialog open={showAgentDialog} onClose={() => setShowAgentDialog(false)} maxWidth="md" fullWidth>
        <DialogTitle>New Agent Call</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Box sx={{ display: 'flex', gap: 2 }}>
            <Select fullWidth size="small" value={agentFrom} onChange={e => setAgentFrom(Number(e.target.value))}>
              {Object.entries(AgentType).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
                <MenuItem key={name} value={val}>{name}</MenuItem>
              ))}
            </Select>
            <Select fullWidth size="small" value={agentTo} onChange={e => setAgentTo(Number(e.target.value))}>
              {Object.entries(AgentType).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
                <MenuItem key={name} value={val}>{name}</MenuItem>
              ))}
            </Select>
            <Select fullWidth size="small" value={agentAction} onChange={e => setAgentAction(Number(e.target.value))}>
              {Object.entries(AgentAction).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
                <MenuItem key={name} value={Number(val)}>{name}</MenuItem>
              ))}
            </Select>
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
          <Button onClick={handleCreateAgentCall} variant="contained">Execute</Button>
        </DialogActions>
      </Dialog>

      {/* Character Create Wizard */}
      <CharacterCreateWizard
        open={showCharCreateWizard}
        onClose={() => setShowCharCreateWizard(false)}
        onFinish={() => { refetchCharacters(); }}
      />

      {/* LLM Log Detail Dialog */}
      <Dialog open={showLogDetail} onClose={() => setShowLogDetail(false)} maxWidth="md" fullWidth>
        <DialogTitle>LLM Interaction Details</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          {selectedLog && (
            <>
              <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                <Chip label={selectedLog.providerType} size="small" />
                <Chip label={selectedLog.model} size="small" />
                <Chip label={selectedLog.success ? 'Success' : 'Failed'} size="small" color={selectedLog.success ? 'success' : 'error'} />
                <Chip label={`${selectedLog.durationMs}ms`} size="small" />
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">Tokens</Typography>
                <Typography>Prompt: {selectedLog.promptTokens ?? '—'} | Completion: {selectedLog.completionTokens ?? '—'} | Total: {selectedLog.totalTokens ?? '—'}</Typography>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">System Prompt</Typography>
                <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 150, overflow: 'auto' }}>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>{selectedLog.systemPrompt || '—'}</Typography>
                </Box>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">User Prompt</Typography>
                <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 150, overflow: 'auto' }}>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>{selectedLog.userPrompt || '—'}</Typography>
                </Box>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">Response</Typography>
                <Box sx={{ bgcolor: 'background.default', p: 1, borderRadius: 1, maxHeight: 200, overflow: 'auto' }}>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 12 }}>{selectedLog.response || '—'}</Typography>
                </Box>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">Origin</Typography>
                <Typography variant="body2">{selectedLog.origin}{selectedLog.originAgent ? ` (${selectedLog.originAgent})` : ''}{selectedLog.originAction ? ` → ${selectedLog.originAction}` : ''}</Typography>
              </Box>
              {selectedLog.error && (
                <Alert severity="error">{selectedLog.error}</Alert>
              )}
              <Typography variant="caption" color="text.secondary">
                {new Date(selectedLog.startedAt).toLocaleString()} → {new Date(selectedLog.completedAt).toLocaleString()}
              </Typography>
            </>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowLogDetail(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

// ==================== Sub-Components ====================

function NPCsTab({ npcs, npcsLoading, onOpenDialog, onDelete }: any) {
  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">NPCs</Typography>
        <Button variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
          Add NPC
        </Button>
      </Box>
      <Divider />
      {npcsLoading ? (
        <Typography sx={{ p: 2 }}>Loading...</Typography>
      ) : npcs.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No NPCs yet. Add one to get started.
        </Typography>
      ) : (
        <List>
          {npcs.map((npc: any) => (
            <ListItem key={npc.id} sx={{ px: 2, alignItems: 'flex-start' }}>
              <ListItemAvatar>
                <Avatar>🧙</Avatar>
              </ListItemAvatar>
              <ListItemText
                primary={npc.name}
                secondary={npc.description || 'No description'}
              />
              <IconButton size="small" onClick={() => onDelete(npc.id)} color="error">
                <DeleteIcon />
              </IconButton>
            </ListItem>
          ))}
        </List>
      )}
    </Paper>
  );
}

function CharactersTab({ characters }: { characters: any[] }) {
  const navigate = useNavigate();
  return (
    <Paper>
      <Box sx={{ p: 2 }}>
        <Typography variant="h6">Characters ({characters.length})</Typography>
      </Box>
      <Divider />
      {characters.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No characters yet.
        </Typography>
      ) : (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 2, p: 2 }}>
          {characters.map((c: any) => (
            <Card key={c.id}>
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                  <Typography variant="h6">{c.name}</Typography>
                  <Chip label={`${c.class} Lv.${c.level}`} size="small" />
                </Box>
                <Typography variant="body2" color="text.secondary">
                  Player: {c.playerName}
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, mt: 1 }}>
                  <Chip label={`HP: ${c.currentHP}/${c.maxHP}`} size="small" color={c.currentHP < c.maxHP * 0.3 ? 'error' : 'default'} />
                </Box>
                <Box sx={{ mt: 1.5 }}>
                  <Button size="small" variant="outlined" fullWidth startIcon={<SheetIcon />}
                    onClick={() => navigate(`/character/${c.id}`)}>
                    View Sheet
                  </Button>
                </Box>
              </CardContent>
            </Card>
          ))}
        </Box>
      )}
    </Paper>
  );
}

function ConsistencyTab({ report, isLoading, onCheck }: any) {
  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Plot Consistency Check</Typography>
        <Button variant="contained" onClick={onCheck} disabled={isLoading}>
          {isLoading ? 'Checking...' : 'Check Consistency'}
        </Button>
      </Box>
      <Divider />
      {!report ? (
        <Typography sx={{ p: 4, textAlign: 'center', color: 'text.secondary' }}>
          Click "Check Consistency" to analyze recent game events for potential plot inconsistencies.
        </Typography>
      ) : (
        <Box sx={{ p: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Analyzed {report.messagesAnalyzed} messages · Checked at {new Date(report.checkedAt).toLocaleString()}
          </Typography>

          {report.warnings.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle1" color="warning.main">⚠️ Warnings ({report.warnings.length})</Typography>
              {report.warnings.map((w: string, i: number) => (
                <Alert severity="warning" variant="outlined" sx={{ mt: 1 }} key={i}>
                  <AlertTitle>Warning</AlertTitle>
                  {w}
                </Alert>
              ))}
            </Box>
          )}

          {report.findings.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle1" color="info.main">📋 Findings ({report.findings.length})</Typography>
              {report.findings.map((f: string, i: number) => (
                <Alert severity="info" variant="outlined" sx={{ mt: 1 }} key={i}>
                  <CheckCircleIcon fontSize="small" sx={{ mr: 1 }} />
                  {f}
                </Alert>
              ))}
            </Box>
          )}

          {report.warnings.length === 0 && report.findings.length === 0 && (
            <Alert severity="success" sx={{ mt: 2 }}>
              <AlertTitle>No Issues Found</AlertTitle>
              All recent events appear consistent. No plot contradictions detected.
            </Alert>
          )}
        </Box>
      )}
    </Paper>
  );
}

// ==================== Agent Calls Tab ====================

function AgentCallsTab({ calls, isLoading, onRefresh, onOpenDialog, onDelete, filter, onFilterChange }: any) {
  const getStatusColor = (status: number) => {
    switch (status) {
      case AgentCallStatus.Running: return 'warning';
      case AgentCallStatus.Completed: return 'success';
      case AgentCallStatus.Failed: return 'error';
      case AgentCallStatus.Pending: return 'default';
      default: return 'default';
    }
  };

  const getAgentLabel = (agent: number) => {
    switch (agent) {
      case AgentType.GM: return '🤖 AI-GM';
      case AgentType.LLM: return '🤖 LLM';
      case AgentType.Dice: return '🎲 Dice';
      case AgentType.RAG: return '📚 RAG';
      case AgentType.NPC: return '🧙 NPC';
      case AgentType.Player: return '👤 Player';
      case AgentType.System: return '⚙️ System';
      default: return '❓ Unknown';
    }
  };

  const getActionLabel = (action: number) => {
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
      default: return 'Unknown';
    }
  };

  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h6">Agent Calls</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Select size="small" value={filter ?? ''} onChange={e => onFilterChange(e.target.value ? parseInt(e.target.value) : undefined)} sx={{ minWidth: 140 }}>
            <MenuItem value="">All Agents</MenuItem>
            {Object.entries(AgentType).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
              <MenuItem key={name} value={Number(val)}>{getAgentLabel(Number(val))}</MenuItem>
            ))}
          </Select>
          <Button variant="outlined" onClick={onRefresh} size="small">Refresh</Button>
          <Button variant="contained" onClick={onOpenDialog} size="small">New Call</Button>
        </Box>
      </Box>
      <Divider />
      {isLoading ? (
        <Typography sx={{ p: 2 }}>Loading...</Typography>
      ) : calls.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No agent calls yet. Agent calls are made internally by the game system.
        </Typography>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>From</TableCell>
                <TableCell>To</TableCell>
                <TableCell>Action</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Duration</TableCell>
                <TableCell>Time</TableCell>
                <TableCell>Output</TableCell>
                <TableCell></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {calls.map((call: any) => (
                <TableRow key={call.id} sx={{ '&:nth-of-type(odd)': { bgcolor: 'action.hover' } }}>
                  <TableCell>{getAgentLabel(call.fromAgent)}</TableCell>
                  <TableCell>{getAgentLabel(call.toAgent)}</TableCell>
                  <TableCell>{getActionLabel(call.action)}</TableCell>
                  <TableCell>
                    <Chip label={call.status} size="small" color={getStatusColor(call.status) as any} variant="outlined" />
                  </TableCell>
                  <TableCell>{call.durationMs}ms</TableCell>
                  <TableCell>{new Date(call.createdAt).toLocaleTimeString()}</TableCell>
                  <TableCell sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {call.outputMessage || call.output?.substring(0, 50) || '—'}
                  </TableCell>
                  <TableCell>
                    <IconButton size="small" onClick={() => onDelete(call.id)} color="error">
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
}

// ==================== LLM Logs Tab ====================

function LLMLogsTab({ logs, isLoading, onRefresh, onOpenDetail, onDelete, filterProvider, onFilterProviderChange, filterFrom, onFilterFromChange, filterTo, onFilterToChange }: any) {
  const getStatusColor = (success: boolean) => success ? 'success' : 'error';

  const getProviderIcon = (provider: string) => {
    switch (provider) {
      case 'ollama': return '🦙';
      case 'lmstudio': return '🏠';
      case 'openai': return '🔵';
      case 'google': return '🟢';
      default: return '🤖';
    }
  };

  const formatTokens = (tokens: number | undefined) => {
    if (!tokens) return '—';
    if (tokens >= 1000) return `${(tokens / 1000).toFixed(1)}k`;
    return tokens.toString();
  };

  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h6">LLM Interactions ({logs.length})</Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          <Select size="small" value={filterProvider} onChange={e => onFilterProviderChange(e.target.value)} sx={{ minWidth: 120 }}>
            <MenuItem value="">All Providers</MenuItem>
            <MenuItem value="ollama">Ollama</MenuItem>
            <MenuItem value="lmstudio">LM Studio</MenuItem>
            <MenuItem value="openai">OpenAI</MenuItem>
            <MenuItem value="google">Google AI</MenuItem>
          </Select>
          <TextField size="small" label="From" type="datetime-local" value={filterFrom} onChange={e => onFilterFromChange(e.target.value)} InputLabelProps={{ shrink: true }} />
          <TextField size="small" label="To" type="datetime-local" value={filterTo} onChange={e => onFilterToChange(e.target.value)} InputLabelProps={{ shrink: true }} />
          <Button variant="outlined" onClick={onRefresh} size="small">Refresh</Button>
        </Box>
      </Box>
      <Divider />
      {isLoading ? (
        <Typography sx={{ p: 2 }}>Loading...</Typography>
      ) : logs.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No LLM interactions recorded yet.
        </Typography>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Provider</TableCell>
                <TableCell>Model</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Tokens</TableCell>
                <TableCell>Duration</TableCell>
                <TableCell>Origin</TableCell>
                <TableCell>Time</TableCell>
                <TableCell></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {logs.map((log: any) => (
                <TableRow key={log.id} sx={{ '&:nth-of-type(odd)': { bgcolor: 'action.hover' } }}>
                  <TableCell>{getProviderIcon(log.providerType)} {log.providerType}</TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', fontSize: 11 }}>{log.model}</TableCell>
                  <TableCell>
                    <Chip label={log.success ? 'OK' : 'Fail'} size="small" color={getStatusColor(log.success) as any} variant="outlined" />
                  </TableCell>
                  <TableCell sx={{ fontSize: 11 }}>
                    {formatTokens(log.promptTokens)} / {formatTokens(log.completionTokens)}
                  </TableCell>
                  <TableCell>{log.durationMs}ms</TableCell>
                  <TableCell sx={{ fontSize: 11 }}>
                    {log.origin}{log.originAgent ? `:${log.originAgent}` : ''}
                  </TableCell>
                  <TableCell>{new Date(log.startedAt).toLocaleTimeString()}</TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', gap: 0.5 }}>
                      <IconButton size="small" onClick={() => onOpenDetail(log)} color="primary">
                        <HistoryIcon fontSize="small" />
                      </IconButton>
                      <IconButton size="small" onClick={() => onDelete(log.id)} color="error">
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
}
