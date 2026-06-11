import { Box, Typography, Button, Table, TableContainer, TableHead, TableCell, TableRow, TableBody } from '@mui/material';
import { NotificationAdd as NotificationIcon, Speed as SpeedIcon } from '@mui/icons-material';
import { SectionCard } from './GameStateCards';

function getAgentLabel(type: string): string {
  const labels: Record<string, string> = {
    GameMaster: 'GM', Planner: 'Planner', Combat: 'Combat', NPC: 'NPC',
    Environment: 'Environment', Player: 'Player', System: 'System',
  };
  return labels[type] || type;
}

function getActionLabel(action: string): string {
  const labels: Record<string, string> = {
    Narrate: 'Narrate', QueryRAG: 'RAG Query', QueryPlot: 'Plot Query',
    QueryCharacter: 'Character Query', QueryPlayers: 'Player Query',
    ManageState: 'State Update', RollDice: 'Roll Dice',
    RollSkillCheck: 'Skill Check', RollAttack: 'Attack',
    StartCombat: 'Start Combat', EndCombat: 'End Combat',
    CreateNPC: 'Create NPC', UpdateNPC: 'Update NPC',
    ApplyDamage: 'Apply Damage', ApplyCondition: 'Apply Condition',
  };
  return labels[action] || action;
}

interface GameStateAgentProps {
  gameState: any;
  onOpenAgentDialog: (call: any) => void;
}

export default function GameStateAgent({ gameState, onOpenAgentDialog }: GameStateAgentProps) {
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
