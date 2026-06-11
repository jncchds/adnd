import { useState } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { useGame } from '../api/hooks/useGame';
import { useNPCs } from '../api/hooks/useGame';
import { useCharacters } from '../api/hooks/useGame';
import { useConsistency } from '../api/hooks/useGame';
import { usePlotWeaver } from '../api/hooks/usePlot';
import { useGameHub } from '../api/hooks/useHub';
import { AgentType, AgentAction } from '../types';
import GameStatePage from './GameStatePage';
import PlotBoardAdminTab from './PlotBoardAdminTab';
import AdminNPCDialog from '../components/admin/AdminNPCDialog';
import AdminAgentCallDialog from '../components/admin/AdminAgentCallDialog';
import LLMLogDetailDialog from '../components/admin/LLMLogDetailDialog';
import AdminNPCsTab from '../components/admin/AdminNPCsTab';
import AdminCharactersTab from '../components/admin/AdminCharactersTab';
import AdminConsistencyTab from '../components/admin/AdminConsistencyTab';
import AdminAgentCallsTab from '../components/admin/AdminAgentCallsTab';
import AdminLLMLogsTab from '../components/admin/AdminLLMLogsTab';
import { Box, Typography, Button, Alert, AlertTitle } from '@mui/material';
import { ChevronLeft as ChevronLeftIcon } from '@mui/icons-material';

export default function AdminPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { game, isLoading } = useGame(id);
  const { npcs, isLoading: npcsLoading, createNPC, updateNPC, deleteNPC } = useNPCs(id);
  const { characters } = useCharacters(id);
  const { report, isLoading: consistencyLoading, check } = useConsistency(id);
  const { threads: plotThreads, isLoading: plotThreadsLoading } = usePlotWeaver(id);
  const location = useLocation();
  const { invoke } = useGameHub();

  const pathParts = location.pathname.split('/').filter(Boolean);
  const view = pathParts[2] || 'dashboard';

  const [npcDialogOpen, setNpcDialogOpen] = useState(false);
  const [errorState, setErrorState] = useState<string | null>(null);
  const [agentCalls, setAgentCalls] = useState<any[]>([]);
  const [agentCallFilter, setAgentCallFilter] = useState<number | undefined>(undefined);
  const [showAgentDialog, setShowAgentDialog] = useState(false);
  const [agentFrom, setAgentFrom] = useState(AgentType.GM);
  const [agentTo, setAgentTo] = useState(AgentType.LLM);
  const [agentAction, setAgentAction] = useState(AgentAction.Query);
  const [agentInput, setAgentInput] = useState('');
  const [llmLogs, setLlmLogs] = useState<any[]>([]);
  const [logsLoading, setLogsLoading] = useState(false);
  const [selectedLog, setSelectedLog] = useState<any>(null);
  const [showLogDetail, setShowLogDetail] = useState(false);
  const [logFilterProvider, setLogFilterProvider] = useState('');
  const [logFilterFrom, setLogFilterFrom] = useState('');
  const [logFilterTo, setLogFilterTo] = useState('');

  const handleCheckConsistency = async () => {
    await check(50);
  };

  const handleRefreshLLMLogs = async () => {
    if (!id) return;
    setLogsLoading(true);
    try {
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
      await invoke('GetAgentCall', callId);
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
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}

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

      {view === 'dashboard' && id && <GameStatePage gameId={id} />}
      {view === 'plot-board' && <PlotBoardAdminTab threads={plotThreads} isLoading={plotThreadsLoading} gameId={id || ''} />}
      {view === 'npcs' && (
        <AdminNPCsTab npcs={npcs} npcsLoading={npcsLoading} onOpenDialog={() => setNpcDialogOpen(true)} onDelete={deleteNPC} onUpdate={updateNPC} />
      )}
      {view === 'characters' && <AdminCharactersTab characters={characters} />}
      {view === 'consistency' && (
        <AdminConsistencyTab report={report} isLoading={consistencyLoading} onCheck={handleCheckConsistency} />
      )}
      {view === 'llm-logs' && (
        <AdminLLMLogsTab logs={llmLogs} isLoading={logsLoading} onRefresh={handleRefreshLLMLogs} onOpenDetail={setSelectedLog} onDelete={handleDeleteLog} filterProvider={logFilterProvider} onFilterProviderChange={setLogFilterProvider} filterFrom={logFilterFrom} onFilterFromChange={setLogFilterFrom} filterTo={logFilterTo} onFilterToChange={setLogFilterTo} />
      )}
      {view === 'agent-calls' && (
        <AdminAgentCallsTab calls={agentCalls} isLoading={false} onRefresh={handleRefreshAgentCalls} onOpenDialog={() => setShowAgentDialog(true)} onDelete={handleDeleteAgentCall} filter={agentCallFilter} onFilterChange={setAgentCallFilter} />
      )}

      <AdminNPCDialog open={npcDialogOpen} onClose={() => setNpcDialogOpen(false)} onCreate={async (name, desc) => {
        if (!id) return;
        await createNPC!(name, desc);
        setNpcDialogOpen(false);
      }} />

      <AdminAgentCallDialog
        open={showAgentDialog}
        onClose={() => setShowAgentDialog(false)}
        agentFrom={agentFrom}
        agentTo={agentTo}
        agentAction={agentAction}
        agentInput={agentInput}
        onAgentFromChange={setAgentFrom}
        onAgentToChange={setAgentTo}
        onAgentActionChange={setAgentAction}
        onAgentInputChange={setAgentInput}
        onExecute={handleCreateAgentCall}
      />

      <LLMLogDetailDialog open={showLogDetail} onClose={() => setShowLogDetail(false)} log={selectedLog} />
    </Box>
  );
}
