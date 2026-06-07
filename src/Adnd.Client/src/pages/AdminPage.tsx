import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGame, useNPCs, usePlotThreads, useCharacters, useConsistency, useLLMPresets, usePlotWeaver } from '../api/gameHooks';
import LLMUsagePanel from './LLMUsagePanel';
import { useGameHub } from '../api/hubHook';
import { WhisperType, AgentType, AgentAction, AgentCallStatus } from '../types';
import CharacterCreateWizard from './CharacterCreateWizard';
import PlotBoardAdminTab from './PlotBoardAdminTab';
import {
  Container, Box, Typography, Paper, Tabs, Tab,
  List, ListItem, ListItemText, ListItemAvatar, Avatar,
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Button, IconButton, Chip, Alert, AlertTitle,
  Divider, Card, CardContent, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Select, MenuItem, Collapse
} from '@mui/material';
import { Delete as DeleteIcon, Add as AddIcon,
  CheckCircle as CheckCircleIcon,
  MenuBook as BookIcon, People as PeopleIcon, History as HistoryIcon,
  AutoFixHigh as ConsistencyIcon, Mic as MicIcon, Chat as ChatBubbleIcon,
  Settings as SettingsIcon, Shield as ShieldIcon, Article as SheetIcon,
  Lightbulb as BulbIcon,
  BarChart as BarChartIcon, ExpandMore as ExpandMoreIcon, ExpandLess as ExpandLessIcon,
  Chat as ChatIcon, Cloud as OllamaIcon, Home as LMStudioIcon,
  Google as GoogleIcon, Circle as OpenAIIcon } from '@mui/icons-material';

export default function AdminPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { game, isLoading } = useGame(id);
  const { npcs, isLoading: npcsLoading, createNPC, updateNPC, deleteNPC } = useNPCs(id);
  const { threads, isLoading: threadsLoading, createThread, updateThread } = usePlotThreads(id);
  const { characters } = useCharacters(id);
  const { report, isLoading: consistencyLoading, check } = useConsistency(id);
  const { threads: plotThreads, isLoading: plotThreadsLoading } = usePlotWeaver(id);

  const [activeTab, setActiveTab] = useState(0);
  const [npcDialogOpen, setNpcDialogOpen] = useState(false);
  const [npcName, setNpcName] = useState('');
  const [npcDesc, setNpcDesc] = useState('');
  const [threadDialogOpen, setThreadDialogOpen] = useState(false);
  const [threadTitle, setThreadTitle] = useState('');
  const [threadDesc, setThreadDesc] = useState('');
  const [errorState, setErrorState] = useState<string | null>(null);
  const [agentCalls, setAgentCalls] = useState<any[]>([]);
  const [whispers, setWhispers] = useState<any[]>([]);
  const [agentCallFilter, setAgentCallFilter] = useState<number | undefined>(undefined);
  const [whisperFilter, setWhisperFilter] = useState<number | undefined>(undefined);
  const [showAgentDialog, setShowAgentDialog] = useState(false);
  const [agentFrom, setAgentFrom] = useState(AgentType.GM);
  const [agentTo, setAgentTo] = useState(AgentType.LLM);
  const [agentAction, setAgentAction] = useState(AgentAction.Query);
  const [agentInput, setAgentInput] = useState('');
  const [gameStateJson, setGameStateJson] = useState('{}');
  const [plotSeed, setPlotSeed] = useState('');
  const [gameParameters, setGameParameters] = useState('');
  const [showCharCreateWizard, setShowCharCreateWizard] = useState(false);
  const [showSystemRegistry, setShowSystemRegistry] = useState(false);
  const [systems, setSystems] = useState<any[]>([]);
  const [customSystemName, setCustomSystemName] = useState('');
  const [customSystemJson, setCustomSystemJson] = useState('{"name": "", "attributes": [], "skills": [], "defaultHP": 10}');
  const { invoke } = useGameHub();

  // LLM Preset state
  const { presets, isLoading: presetsLoading, createPreset, updatePreset, deletePreset, testConnection, setDefault: setDefaultPreset } = useLLMPresets();
  const [showPresetDialog, setShowPresetDialog] = useState(false);
  const [editingPreset, setEditingPreset] = useState<any>(null);
  const [presetName, setPresetName] = useState('');
  const [presetProvider, setPresetProvider] = useState('ollama');
  const [presetModel, setPresetModel] = useState('');
  const [presetEndpoint, setPresetEndpoint] = useState('');
  const [presetApiKey, setPresetApiKey] = useState('');
  const [presetTemp, setPresetTemp] = useState(0.7);
  const [presetMaxTokens, setPresetMaxTokens] = useState(2048);
  const [presetTopP, setPresetTopP] = useState(0.9);
  const [presetEmbedModel, setPresetEmbedModel] = useState('');
  const [presetTestLoading, setPresetTestLoading] = useState(false);

  // LLM Interaction logs state
  const [llmLogs, setLlmLogs] = useState<any[]>([]);
  const [logsLoading, setLogsLoading] = useState(false);
  const [selectedLog, setSelectedLog] = useState<any>(null);
  const [showLogDetail, setShowLogDetail] = useState(false);
  const [logFilterProvider, setLogFilterProvider] = useState('');
  const [logFilterFrom, setLogFilterFrom] = useState('');
  const [logFilterTo, setLogFilterTo] = useState('');
  const [presetTestResult, setPresetTestResult] = useState<{success: boolean; message?: string} | null>(null);

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

  const handleCreateThread = async () => {
    if (!threadTitle.trim()) return;
    try {
      await createThread!({ title: threadTitle, description: threadDesc });
      setThreadDialogOpen(false);
      setThreadTitle('');
      setThreadDesc('');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleCheckConsistency = async () => {
    await check(50);
  };

  const handleSaveGameState = async () => {
    if (!id) return;
    try {
      await invoke('UpdateGameState', id, gameStateJson, plotSeed, gameParameters);
      setGameStateJson(gameStateJson);
      setPlotSeed(plotSeed);
      setGameParameters(gameParameters);
      setErrorState('Game state saved successfully.');
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleLoadSystems = async () => {
    if (!id) return;
    try {
      const result = await invoke('GetSystems', id);
      if (result) setSystems(result);
    } catch (e) {
      console.error('Failed to load systems', e);
    }
  };

  // ==================== LLM Preset Handlers ====================

  const handleOpenPresetDialog = (preset?: any) => {
    if (preset) {
      setEditingPreset(preset);
      setPresetName(preset.name);
      setPresetProvider(preset.providerType);
      setPresetModel(preset.baseModel);
      setPresetEndpoint(preset.endpointUrl || '');
      setPresetApiKey('');
      setPresetTemp(preset.temperature);
      setPresetMaxTokens(preset.maxTokens);
      setPresetTopP(preset.topP);
      setPresetEmbedModel(preset.embeddingModel || '');
    } else {
      setEditingPreset(null);
      setPresetName('');
      setPresetProvider('ollama');
      setPresetModel('');
      setPresetEndpoint('');
      setPresetApiKey('');
      setPresetTemp(0.7);
      setPresetMaxTokens(2048);
      setPresetTopP(0.9);
      setPresetEmbedModel('');
    }
    setPresetTestResult(null);
    setShowPresetDialog(true);
  };

  const handleSavePreset = async () => {
    if (!presetName.trim()) return;
    try {
      const request = {
        name: presetName,
        providerType: presetProvider,
        baseModel: presetModel,
        endpointUrl: presetEndpoint || undefined,
        apiKey: presetApiKey || undefined,
        temperature: presetTemp,
        maxTokens: presetMaxTokens,
        topP: presetTopP,
        embeddingModel: presetEmbedModel || undefined,
        isDefault: false,
        isActive: true,
      };
      if (editingPreset) {
        await updatePreset(editingPreset.id, request);
      } else {
        await createPreset(request);
      }
      setShowPresetDialog(false);
      setPresetTestResult(null);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleTestPreset = async (presetId: string) => {
    setPresetTestLoading(true);
    try {
      const result: any = await testConnection(presetId);
      setPresetTestResult({ success: result.success, message: result.message });
    } catch (e: any) {
      setPresetTestResult({ success: false, message: e.message });
    } finally {
      setPresetTestLoading(false);
    }
  };

  const handleSetDefaultPreset = async (presetId: string) => {
    try {
      await setDefaultPreset(presetId);
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleDeletePreset = async (presetId: string) => {
    if (window.confirm('Delete this preset?')) {
      try {
        await deletePreset(presetId);
      } catch (e: any) {
        setErrorState(e.message);
      }
    }
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

  const handleCreateSystem = async () => {
    if (!id || !customSystemName.trim()) return;
    try {
      await invoke('CreateSystem', id, customSystemName, customSystemJson);
      setShowSystemRegistry(false);
      setCustomSystemName('');
      setCustomSystemJson('{"name": "", "attributes": [], "skills": [], "defaultHP": 10}');
      await handleLoadSystems();
    } catch (e: any) {
      setErrorState(e.message);
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

  const handleRefreshWhispers = async () => {
    if (!id) return;
    try {
      const w = await invoke('GetWhisperHistory', id, 50);
      if (w) setWhispers(w);
    } catch (e) {
      console.error('Failed to fetch whispers', e);
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

  const tabs = [
    { label: 'NPCs', icon: <PeopleIcon />, count: npcs.length },
    { label: 'Plot Board', icon: <BulbIcon />, count: plotThreads.length },
    { label: 'Plot Threads', icon: <BookIcon />, count: threads.length },
    { label: 'Characters', icon: <HistoryIcon />, count: characters.length },
    { label: 'Consistency', icon: <ConsistencyIcon /> },
    { label: 'Agent Calls', icon: <MicIcon />, count: agentCalls.length },
    { label: 'LLM Presets', icon: <SettingsIcon />, count: presets.length },
    { label: 'LLM Usage', icon: <BarChartIcon /> },
    { label: 'LLM Logs', icon: <HistoryIcon />, count: llmLogs.length },
    { label: 'Whispers', icon: <ChatBubbleIcon />, count: whispers.length },
    { label: 'Game State', icon: <SettingsIcon /> },
    { label: 'Systems', icon: <ShieldIcon /> },
  ];

  return (
    <Container maxWidth="lg" sx={{ mt: 2, mb: 2 }}>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Box>
          <Typography variant="h5">{game.name}</Typography>
          <Typography variant="body2" color="text.secondary">Admin Panel</Typography>
        </Box>
        <Button variant="outlined" onClick={() => navigate(`/game/${id}`)}>
          ← Back to Game Room
        </Button>
      </Box>

      {/* Error */}
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}

      {/* Tabs */}
      <Paper sx={{ mb: 2 }}>
        <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)}>
          {tabs.map((tab, i) => (
            <Tab key={i} label={`${tab.label}${tab.count !== undefined ? ` (${tab.count})` : ''}`} icon={tab.icon} iconPosition="start" />
          ))}
        </Tabs>
      </Paper>

      {/* Tab Content */}
      {activeTab === 0 && (
        <NPCsTab
          npcs={npcs}
          npcsLoading={npcsLoading}
          onOpenDialog={() => setNpcDialogOpen(true)}
          onDelete={deleteNPC}
          onUpdate={updateNPC}
        />
      )}

      {activeTab === 1 && (
        <PlotBoardAdminTab threads={plotThreads} isLoading={plotThreadsLoading} gameId={id || ''} />
      )}

      {activeTab === 2 && (
        <PlotThreadsTab
          threads={threads}
          threadsLoading={threadsLoading}
          onOpenDialog={() => setThreadDialogOpen(true)}
          onUpdate={updateThread}
        />
      )}

      {activeTab === 3 && (
        <CharactersTab characters={characters} />
      )}

      {activeTab === 4 && (
        <ConsistencyTab
          report={report}
          isLoading={consistencyLoading}
          onCheck={handleCheckConsistency}
        />
      )}

      {activeTab === 5 && (
        <AgentCallsTab
          calls={agentCalls}
          isLoading={false}
          onRefresh={handleRefreshAgentCalls}
          onOpenDialog={() => setShowAgentDialog(true)}
          onDelete={handleDeleteAgentCall}
          filter={agentCallFilter}
          onFilterChange={setAgentCallFilter}
        />
      )}

      {activeTab === 6 && (
        <LLMPresetsTab
          presets={presets}
          isLoading={presetsLoading}
          onOpenDialog={handleOpenPresetDialog}
          onDelete={handleDeletePreset}
          onTest={handleTestPreset}
          onSetDefault={handleSetDefaultPreset}
        />
      )}

      {activeTab === 7 && (
        <LLMUsagePanel gameId={id!} />
      )}

      {activeTab === 8 && (
        <LLMLogsTab
          logs={llmLogs}
          isLoading={logsLoading}
          onRefresh={handleRefreshLLMLogs}
          onOpenDetail={setSelectedLog}
          onDelete={handleDeleteLog}
          filterProvider={logFilterProvider}
          onFilterProviderChange={setLogFilterProvider}
          filterFrom={logFilterFrom}
          onFilterFromChange={setLogFilterFrom}
          filterTo={logFilterTo}
          onFilterToChange={setLogFilterTo}
        />
      )}

      {activeTab === 9 && (
        <WhispersTabAdmin
          whispers={whispers}
          isLoading={false}
          onRefresh={handleRefreshWhispers}
          filter={whisperFilter}
          onFilterChange={setWhisperFilter}
        />
      )}

      {activeTab === 10 && (
        <GameStateTab
          game={game}
          gameStateJson={gameStateJson}
          setGameStateJson={setGameStateJson}
          plotSeed={plotSeed}
          setPlotSeed={setPlotSeed}
          gameParameters={gameParameters}
          setGameParameters={setGameParameters}
          onSave={handleSaveGameState}
          onOpenCharCreate={() => setShowCharCreateWizard(true)}
        />
      )}

      {activeTab === 11 && (
        <SystemsTab
          systems={systems}
          showRegistry={showSystemRegistry}
          customSystemName={customSystemName}
          setCustomSystemName={setCustomSystemName}
          customSystemJson={customSystemJson}
          setCustomSystemJson={setCustomSystemJson}
          onToggleRegistry={() => {
            setShowSystemRegistry(!showSystemRegistry);
            if (!showSystemRegistry) handleLoadSystems();
          }}
          onCreateSystem={handleCreateSystem}
        />
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

      {/* Plot Thread Dialog */}
      <Dialog open={threadDialogOpen} onClose={() => setThreadDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>New Plot Thread</DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          <TextField fullWidth label="Title" value={threadTitle} onChange={e => setThreadTitle(e.target.value)} sx={{ mb: 2 }} autoFocus />
          <TextField fullWidth label="Description" multiline rows={3} value={threadDesc} onChange={e => setThreadDesc(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setThreadDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleCreateThread} variant="contained" disabled={!threadTitle.trim()}>Create</Button>
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
        gameId={id || ''}
      />

      {/* LLM Preset Dialog */}
      <Dialog open={showPresetDialog} onClose={() => setShowPresetDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editingPreset ? 'Edit LLM Preset' : 'New LLM Preset'}</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField fullWidth label="Name" value={presetName} onChange={e => setPresetName(e.target.value)} autoFocus />
          <TextField fullWidth select label="Provider" value={presetProvider} onChange={e => setPresetProvider(e.target.value)} SelectProps={{ native: true }}>
            <option value="ollama">Ollama</option>
            <option value="lmstudio">LM Studio</option>
            <option value="openai">OpenAI / OpenAI-Compatible</option>
            <option value="google">Google AI Studio</option>
          </TextField>
          <TextField fullWidth label="Base Model" value={presetModel} onChange={e => setPresetModel(e.target.value)} placeholder="e.g., llama3, gpt-4o-mini, gemini-pro" />
          <TextField fullWidth label="Endpoint URL" value={presetEndpoint} onChange={e => setPresetEndpoint(e.target.value)} placeholder={presetProvider === 'ollama' ? 'http://localhost:11434' : presetProvider === 'lmstudio' ? 'http://localhost:1234' : 'https://api.openai.com/v1'} />
          <TextField fullWidth label="API Key" type="password" value={presetApiKey} onChange={e => setPresetApiKey(e.target.value)} placeholder="Leave blank to keep existing key" />
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField fullWidth label="Temperature" type="number" value={presetTemp} onChange={e => setPresetTemp(parseFloat(e.target.value) || 0)} inputProps={{ step: 0.1, min: 0, max: 2 }} />
            <TextField fullWidth label="Max Tokens" type="number" value={presetMaxTokens} onChange={e => setPresetMaxTokens(parseInt(e.target.value) || 2048)} />
            <TextField fullWidth label="Top P" type="number" value={presetTopP} onChange={e => setPresetTopP(parseFloat(e.target.value) || 0.9)} inputProps={{ step: 0.1, min: 0, max: 1 }} />
          </Box>
          <TextField fullWidth label="Embedding Model (optional)" value={presetEmbedModel} onChange={e => setPresetEmbedModel(e.target.value)} placeholder="e.g., nomic-embed-text, text-embedding-3-small" />
          {presetTestResult && (
            <Alert severity={presetTestResult.success ? 'success' : 'error'}>{presetTestResult.message}</Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowPresetDialog(false)}>Cancel</Button>
          {editingPreset && (
            <Button variant="outlined" onClick={() => handleTestPreset(editingPreset.id)} disabled={presetTestLoading}>
              {presetTestLoading ? 'Testing...' : 'Test Connection'}
            </Button>
          )}
          <Button onClick={handleSavePreset} variant="contained" disabled={!presetName.trim() || !presetModel.trim()}>
            {editingPreset ? 'Save' : 'Create'}
          </Button>
        </DialogActions>
      </Dialog>

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
    </Container>
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

function PlotThreadsTab({ threads, threadsLoading, onOpenDialog }: any) {
  const statusColor = (status: string) => {
    switch (status) {
      case 'Active': return 'success';
      case 'Resolved': return 'default';
      case 'Abandoned': return 'error';
      default: return 'default';
    }
  };

  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Plot Threads</Typography>
        <Button variant="outlined" startIcon={<AddIcon />} onClick={onOpenDialog}>
          New Thread
        </Button>
      </Box>
      <Divider />
      {threadsLoading ? (
        <Typography sx={{ p: 2 }}>Loading...</Typography>
      ) : threads.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No plot threads yet. Create one to track storylines.
        </Typography>
      ) : (
        <List>
          {threads.map((thread: any) => (
            <ListItem key={thread.id} sx={{ px: 2 }}>
              <ListItemAvatar>
                <Avatar>📖</Avatar>
              </ListItemAvatar>
              <ListItemText
                primary={thread.title}
                secondary={`${thread.description} · ${thread.keyEventMessageIds?.length || 0} events · ${new Date(thread.createdAt).toLocaleDateString()}`}
              />
              <Chip label={thread.status} size="small" color={statusColor(thread.status) as any} />
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

// ==================== Whispers Tab (Admin) ====================

function WhispersTabAdmin({ whispers, isLoading, onRefresh }: any) {
  const getWhisperTypeLabel = (type: number) => {
    switch (type) {
      case WhisperType.InGamePlayerToGM: return '🤫 P→GM (In-Game)';
      case WhisperType.InGameGMToPlayer: return '🤫 GM→P (In-Game)';
      case WhisperType.OOCPlayerToGM: return '🤫 P→GM (OOC)';
      case WhisperType.OOCGMToPlayer: return '🤫 GM→P (OOC)';
      case WhisperType.PlayerToPlayer: return '🤫 P→P';
      case WhisperType.GMToGroup: return '🤫 Creator→G';
      case WhisperType.GMToAll: return '🤫 Creator→All';
      default: return '🤫 Whisper';
    }
  };

  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Whispers ({whispers.length})</Typography>
        <Button variant="outlined" onClick={onRefresh} size="small">Refresh</Button>
      </Box>
      <Divider />
      {isLoading ? (
        <Typography sx={{ p: 2 }}>Loading...</Typography>
      ) : whispers.length === 0 ? (
        <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
          No whispers recorded yet.
        </Typography>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>From</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Content</TableCell>
                <TableCell>Time</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {whispers.map((w: any) => (
                <TableRow key={w.id} sx={{ '&:nth-of-type(odd)': { bgcolor: 'action.hover' } }}>
                  <TableCell>{w.fromCharacter}</TableCell>
                  <TableCell><Chip label={getWhisperTypeLabel(w.type)} size="small" sx={{ height: 16, fontSize: 10 }} /></TableCell>
                  <TableCell sx={{ maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{w.content}</TableCell>
                  <TableCell>{new Date(w.createdAt).toLocaleTimeString()}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
}

// ==================== Game State Tab ====================

function GameStateTab({ game, gameStateJson, setGameStateJson, plotSeed, setPlotSeed, gameParameters, setGameParameters, onSave, onOpenCharCreate }: any) {
  const [editMode, setEditMode] = useState(false);

  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Game State</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button variant="outlined" onClick={onOpenCharCreate}>Create Character</Button>
          <Button variant="outlined" onClick={() => setEditMode(!editMode)}>
            {editMode ? 'Cancel' : 'Edit State'}
          </Button>
          {editMode && (
            <Button variant="contained" onClick={onSave}>Save</Button>
          )}
        </Box>
      </Box>
      <Divider />
      <Box sx={{ p: 2 }}>
        <Box sx={{ mb: 2 }}>
          <Typography variant="subtitle1" color="primary">Game Info</Typography>
          <Typography variant="body2">Name: {game.name}</Typography>
          <Typography variant="body2">System: {game.systemId} v{game.systemVersion || 'unknown'}</Typography>
          <Typography variant="body2">Status: {game.status}</Typography>
          <Typography variant="body2">Created: {new Date(game.createdAt).toLocaleDateString()}</Typography>
        </Box>
        <Divider sx={{ my: 2 }} />
        <Box sx={{ mb: 2 }}>
          <Typography variant="subtitle1" color="primary">Plot Seed</Typography>
          {editMode ? (
            <TextField
              fullWidth
              multiline
              rows={3}
              value={plotSeed}
              onChange={e => setPlotSeed(e.target.value)}
              placeholder="Initial plot setup (JSON or text)"
              sx={{ mt: 1 }}
            />
          ) : (
            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', bgcolor: 'background.default', p: 1, borderRadius: 1 }}>
              {plotSeed || '(not set)'}
            </Typography>
          )}
        </Box>
        <Box sx={{ mb: 2 }}>
          <Typography variant="subtitle1" color="primary">Game Parameters</Typography>
          {editMode ? (
            <TextField
              fullWidth
              multiline
              rows={3}
              value={gameParameters}
              onChange={e => setGameParameters(e.target.value)}
              placeholder="Game tone, difficulty, pacing (JSON or text)"
              sx={{ mt: 1 }}
            />
          ) : (
            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', bgcolor: 'background.default', p: 1, borderRadius: 1 }}>
              {gameParameters || '(not set)'}
            </Typography>
          )}
        </Box>
        <Box>
          <Typography variant="subtitle1" color="primary">Game State (JSON)</Typography>
          {editMode ? (
            <TextField
              fullWidth
              multiline
              rows={10}
              value={gameStateJson}
              onChange={e => setGameStateJson(e.target.value)}
              placeholder='{"currentScene": "...", "npcs": [...], ...}'
              sx={{ mt: 1, fontFamily: 'monospace' }}
            />
          ) : (
            <Box sx={{ bgcolor: 'background.default', p: 2, borderRadius: 1, maxHeight: 300, overflow: 'auto' }}>
              <Typography variant="body2" sx={{ fontFamily: 'monospace', whiteSpace: 'pre-wrap' }}>
                {(() => {
                  try { return JSON.stringify(JSON.parse(gameStateJson), null, 2); }
                  catch { return gameStateJson || '{}'; }
                })()}
              </Typography>
            </Box>
          )}
        </Box>
      </Box>
    </Paper>
  );
}

// ==================== Systems Tab ====================

function SystemsTab({ systems, showRegistry, customSystemName, setCustomSystemName, customSystemJson, setCustomSystemJson, onToggleRegistry, onCreateSystem }: any) {
  const [showCreate, setShowCreate] = useState(false);

  const builtinSystems = [
    { id: 'dnd5e', name: 'D&D 5th Edition', version: '5.4' },
    { id: 'pf2e', name: 'Pathfinder 2nd Edition', version: '2.3' },
    { id: 'coc7e', name: 'Call of Cthulhu 7th Edition', version: '7.1' },
  ];

  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">RPG Systems</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button variant="outlined" onClick={onToggleRegistry}>
            {showRegistry ? 'Hide Registry' : 'Show Registry'}
          </Button>
          <Button variant="outlined" onClick={() => setShowCreate(!showCreate)}>
            Add Custom System
          </Button>
        </Box>
      </Box>
      <Divider />
      <Box sx={{ p: 2 }}>
        <Typography variant="subtitle1" color="primary">Built-in Systems</Typography>
        <List>
          {builtinSystems.map((sys: any, i: number) => (
            <ListItem key={i} sx={{ px: 0 }}>
              <ListItemAvatar>
                <Avatar sx={{ bgcolor: 'primary.main' }}>🎲</Avatar>
              </ListItemAvatar>
              <ListItemText
                primary={sys.name}
                secondary={`ID: ${sys.id} · Version: ${sys.version}`}
              />
              <Chip label="Built-in" size="small" color="default" variant="outlined" />
            </ListItem>
          ))}
        </List>

        {showRegistry && (
          <Box sx={{ mt: 3 }}>
            <Typography variant="subtitle1" color="primary">Custom Systems ({systems.length})</Typography>
            {systems.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ p: 1 }}>No custom systems yet.</Typography>
            ) : (
              <List>
                {systems.map((sys: any, i: number) => (
                  <ListItem key={i} sx={{ px: 0 }}>
                    <ListItemAvatar>
                      <Avatar sx={{ bgcolor: 'secondary.main' }}>⚙️</Avatar>
                    </ListItemAvatar>
                    <ListItemText
                      primary={sys.name}
                      secondary={`Created: ${new Date(sys.createdAt).toLocaleDateString()}`}
                    />
                    <Chip label="Custom" size="small" color="secondary" variant="outlined" />
                  </ListItem>
                ))}
              </List>
            )}

            {showCreate && (
              <Paper sx={{ p: 2, mt: 2, bgcolor: 'background.default' }}>
                <Typography variant="subtitle2" gutterBottom>Create Custom System</Typography>
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                  <TextField
                    size="small"
                    label="System Name"
                    value={customSystemName}
                    onChange={e => setCustomSystemName(e.target.value)}
                    placeholder="My Custom RPG"
                  />
                  <TextField
                    size="small"
                    label="JSON Definition"
                    multiline
                    rows={6}
                    value={customSystemJson}
                    onChange={e => setCustomSystemJson(e.target.value)}
                    placeholder='{"name": "...", "attributes": [...], "skills": [...]}'
                    sx={{ fontFamily: 'monospace' }}
                  />
                  <Box sx={{ display: 'flex', gap: 1 }}>
                    <Button size="small" variant="contained" onClick={onCreateSystem}>
                      Create
                    </Button>
                    <Button size="small" variant="outlined" onClick={() => setShowCreate(false)}>
                      Cancel
                    </Button>
                  </Box>
                </Box>
              </Paper>
            )}
          </Box>
        )}
      </Box>
    </Paper>
  );
}

// ==================== LLM Presets Tab ====================

function LLMPresetsTab({ presets, isLoading, onOpenDialog, onDelete, onTest, onSetDefault }: any) {
  const [expanded, setExpanded] = useState(true);

  const getProviderIcon = (provider: string) => {
    switch (provider) {
      case 'ollama': return <OllamaIcon fontSize="small" />;
      case 'lmstudio': return <LMStudioIcon fontSize="small" />;
      case 'openai': return <OpenAIIcon fontSize="small" sx={{ color: '#10a97f' }} />;
      case 'google': return <GoogleIcon fontSize="small" />;
      default: return <ChatIcon fontSize="small" />;
    }
  };

  const getProviderLabel = (provider: string) => {
    switch (provider) {
      case 'ollama': return 'Ollama';
      case 'lmstudio': return 'LM Studio';
      case 'openai': return 'OpenAI';
      case 'google': return 'Google AI';
      default: return provider;
    }
  };

  return (
    <Paper>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">LLM Presets ({presets.length})</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button variant="outlined" size="small" onClick={() => setExpanded(!expanded)}>
            {expanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
            {expanded ? 'Collapse' : 'Expand'}
          </Button>
          <Button variant="contained" size="small" onClick={() => onOpenDialog()} startIcon={<AddIcon fontSize="small" />}>
            Add Preset
          </Button>
        </Box>
      </Box>
      <Collapse in={expanded}>
        <Divider />
        {isLoading ? (
          <Typography sx={{ p: 2 }}>Loading...</Typography>
        ) : presets.length === 0 ? (
          <Typography sx={{ p: 2, textAlign: 'center', color: 'text.secondary' }}>
            No LLM presets configured. Click "Add Preset" to get started.
          </Typography>
        ) : (
          <List>
            {presets.map((preset: any) => (
              <ListItem key={preset.id} sx={{ px: 2, alignItems: 'flex-start' }}>
                <ListItemAvatar>
                  <Avatar sx={{ width: 32, height: 32 }}>{getProviderIcon(preset.providerType)}</Avatar>
                </ListItemAvatar>
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Typography variant="body1">{preset.name}</Typography>
                      {preset.isDefault && <Chip label="Default" size="small" color="primary" />}
                      {!preset.isActive && <Chip label="Inactive" size="small" color="default" />}
                    </Box>
                  }
                  secondary={
                    <Box sx={{ mt: 0.5 }}>
                      <Typography variant="body2" color="text.secondary">
                        {getProviderLabel(preset.providerType)} · {preset.baseModel} · Temp: {preset.temperature} · Max: {preset.maxTokens}T
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {preset.endpointUrl || 'Default endpoint'}{preset.hasApiKey ? ' · Has API key' : ' · No API key'}
                      </Typography>
                    </Box>
                  }
                />
                <Box sx={{ display: 'flex', gap: 0.5 }}>
                  <IconButton size="small" onClick={() => onTest(preset.id)} color="info">
                    <CheckCircleIcon fontSize="small" />
                  </IconButton>
                  <IconButton size="small" onClick={() => onOpenDialog(preset)}>
                    <SettingsIcon fontSize="small" />
                  </IconButton>
                  {!preset.isDefault && (
                    <IconButton size="small" onClick={() => onSetDefault(preset.id)} color="primary">
                      <ShieldIcon fontSize="small" />
                    </IconButton>
                  )}
                  <IconButton size="small" onClick={() => onDelete(preset.id)} color="error">
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Box>
              </ListItem>
            ))}
          </List>
        )}
      </Collapse>
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
