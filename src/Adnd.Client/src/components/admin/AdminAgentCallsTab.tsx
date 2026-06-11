import { Box, Typography, Paper, Table, TableContainer, TableHead, TableCell, TableRow, TableBody, IconButton, Chip, Select, MenuItem, Button, Divider } from '@mui/material';
import { Delete as DeleteIcon } from '@mui/icons-material';
import { AgentType, AgentAction, AgentCallStatus } from '../../types/agent.types';

export default function AgentCallsTab({ calls, isLoading, onRefresh, onOpenDialog, onDelete, filter, onFilterChange }: any) {
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

