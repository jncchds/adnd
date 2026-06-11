import { useState } from 'react';
import { Box, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Button, Select, MenuItem, Alert, AlertTitle } from '@mui/material';
import { AgentType, AgentAction } from '../../types/agent.types';

interface AdminAgentCallDialogProps {
  open: boolean;
  onClose: () => void;
  agentFrom: AgentType;
  agentTo: AgentType;
  agentAction: AgentAction;
  agentInput: string;
  onAgentFromChange: (val: AgentType) => void;
  onAgentToChange: (val: AgentType) => void;
  onAgentActionChange: (val: AgentAction) => void;
  onAgentInputChange: (val: string) => void;
  onExecute: () => Promise<void>;
}

function getAgentLabel(agent: AgentType): string {
  const labels: Record<number, string> = {
    [AgentType.GM]: '🤖 AI-GM',
    [AgentType.LLM]: '🤖 LLM',
    [AgentType.Dice]: '🎲 Dice',
    [AgentType.RAG]: '📚 RAG',
    [AgentType.NPC]: '🧙 NPC',
    [AgentType.Player]: '👤 Player',
    [AgentType.System]: '⚙️ System',
  };
  return labels[agent] || '❓ Unknown';
}

function getActionLabel(action: AgentAction): string {
  const labels: Record<number, string> = {
    [AgentAction.Query]: 'Query',
    [AgentAction.Generate]: 'Generate',
    [AgentAction.Roll]: 'Roll',
    [AgentAction.Check]: 'Check',
    [AgentAction.Narrate]: 'Narrate',
    [AgentAction.Suggest]: 'Suggest',
    [AgentAction.Execute]: 'Execute',
    [AgentAction.Notify]: 'Notify',
    [AgentAction.Recall]: 'Recall',
    [AgentAction.ManageState]: 'State',
  };
  return labels[action] || 'Unknown';
}

export default function AdminAgentCallDialog({
  open, onClose,
  agentFrom, agentTo, agentAction, agentInput,
  onAgentFromChange, onAgentToChange, onAgentActionChange, onAgentInputChange,
  onExecute,
}: AdminAgentCallDialogProps) {
  const [error, setError] = useState<string | null>(null);

  const handleExecute = async () => {
    try {
      await onExecute();
    } catch (e: any) {
      setError(e.message);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>New Agent Call</DialogTitle>
      <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 1 }}>
            <AlertTitle>Error</AlertTitle>
            {error}
          </Alert>
        )}
        <Box sx={{ display: 'flex', gap: 2 }}>
          <Select fullWidth size="small" value={agentFrom} onChange={e => onAgentFromChange(Number(e.target.value))}>
            {Object.entries(AgentType).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
              <MenuItem key={name} value={val}>{getAgentLabel(Number(val))}</MenuItem>
            ))}
          </Select>
          <Select fullWidth size="small" value={agentTo} onChange={e => onAgentToChange(Number(e.target.value))}>
            {Object.entries(AgentType).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
              <MenuItem key={name} value={val}>{getAgentLabel(Number(val))}</MenuItem>
            ))}
          </Select>
          <Select fullWidth size="small" value={agentAction} onChange={e => onAgentActionChange(Number(e.target.value))}>
            {Object.entries(AgentAction).filter(([k]) => isNaN(Number(k))).map(([name, val]) => (
              <MenuItem key={name} value={Number(val)}>{getActionLabel(Number(val))}</MenuItem>
            ))}
          </Select>
        </Box>
        <TextField
          fullWidth
          label="Input (JSON)"
          multiline
          rows={4}
          value={agentInput}
          onChange={e => onAgentInputChange(e.target.value)}
          placeholder='{"systemPrompt": "...", "userPrompt": "..."}'
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleExecute} variant="contained">Execute</Button>
      </DialogActions>
    </Dialog>
  );
}
