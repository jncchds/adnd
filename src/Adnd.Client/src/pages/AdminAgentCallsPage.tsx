import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useGameHub } from '../api/hooks/useHub';
import { AgentType, AgentAction } from '../types';
import AdminAgentCallDialog from '../components/admin/AdminAgentCallDialog';
import AdminAgentCallsTab from '../components/admin/AdminAgentCallsTab';
import { Box, Alert, AlertTitle } from '@mui/material';

export default function AdminAgentCallsPage() {
  const { id } = useParams<{ id: string }>();
  const { invoke } = useGameHub();
  const [agentCalls, setAgentCalls] = useState<any[]>([]);
  const [agentCallFilter, setAgentCallFilter] = useState<number | undefined>(undefined);
  const [showAgentDialog, setShowAgentDialog] = useState(false);
  const [agentFrom, setAgentFrom] = useState(AgentType.GM);
  const [agentTo, setAgentTo] = useState(AgentType.LLM);
  const [agentAction, setAgentAction] = useState(AgentAction.Query);
  const [agentInput, setAgentInput] = useState('');
  const [errorState, setErrorState] = useState<string | null>(null);

  const handleRefresh = async () => {
    if (!id) return;
    try {
      const calls = await invoke('GetAgentCallHistory', id, agentCallFilter, undefined, 50);
      if (calls) setAgentCalls(calls);
    } catch (e) {
      console.error('Failed to fetch agent calls', e);
    }
  };

  const handleCreate = async () => {
    if (!id) return;
    try {
      await invoke('CallAgent', agentFrom, agentTo, agentAction, agentInput || undefined);
      setShowAgentDialog(false);
      setAgentInput('');
      await handleRefresh();
    } catch (e: any) {
      setErrorState(e.message);
    }
  };

  const handleDelete = async (callId: string) => {
    try {
      await invoke('GetAgentCall', callId);
      setAgentCalls(prev => prev.filter(c => c.id !== callId));
    } catch (e) {
      console.error('Failed to delete agent call', e);
    }
  };

  if (!id) return null;

  return (
    <Box>
      {errorState && (
        <Alert severity="error" onClose={() => setErrorState(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {errorState}
        </Alert>
      )}
      <AdminAgentCallsTab
        calls={agentCalls}
        isLoading={false}
        onRefresh={handleRefresh}
        onOpenDialog={() => setShowAgentDialog(true)}
        onDelete={handleDelete}
        filter={agentCallFilter}
        onFilterChange={setAgentCallFilter}
      />
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
        onExecute={handleCreate}
      />
    </Box>
  );
}
