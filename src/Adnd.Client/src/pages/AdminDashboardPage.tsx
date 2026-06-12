import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGame } from '../api/hooks/useGameDetail';
import { usePlayers } from '../api/hooks/useSessionPlayers';
import { usePlotWeaver } from '../api/hooks/usePlot';
import { useCharacters } from '../api/hooks/useCharacters';
import { useNPCs } from '../api/hooks/useNPCs';
import { useGameHub } from '../api/hooks/useHub';
import { Box, Typography, Grid, Paper, Chip, Button, Divider } from '@mui/material';
import {
  People as PeopleIcon,
  MenuBook as PlotIcon,
  PeopleAlt as CharactersIcon,
  SmartToy as AgentIcon,
  History as LogsIcon,
  Balance as ConsistencyIcon,
  Chat as ChatIcon,
  SportsEsports as CombatIcon,
  Settings as SettingsIcon,
} from '@mui/icons-material';

interface QuickStatCardProps {
  title: string;
  value: string | number;
  icon: React.ReactNode;
  link?: string;
  color?: string;
  isLoading?: boolean;
  sub?: string;
}

function QuickStatCard({ title, value, icon, link, color, isLoading, sub }: QuickStatCardProps) {
  const content = (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 1 }}>
      <Box sx={{
        width: 40, height: 40, borderRadius: 1, display: 'flex', alignItems: 'center', justifyContent: 'center',
        bgcolor: `${color || 'primary.main'}22`, color: color || 'primary.main',
      }}>
        {icon}
      </Box>
      <Box>
        <Typography variant="h4" fontWeight={700}>{isLoading ? '—' : value}</Typography>
        <Typography variant="caption" color="text.secondary">{title}</Typography>
        {sub && <Typography variant="caption" display="block" color="text.secondary">{sub}</Typography>}
      </Box>
    </Box>
  );

  return link ? (
    <a href={link} style={{ textDecoration: 'none', color: 'inherit' }}>
      <Paper elevation={0} sx={{ border: '1px solid rgba(255,255,255,0.08)', borderRadius: 2, cursor: 'pointer', transition: 'border-color 0.2s', '&:hover': { borderColor: 'primary.main' } }}>
        {content}
      </Paper>
    </a>
  ) : (
    <Paper elevation={0} sx={{ border: '1px solid rgba(255,255,255,0.08)', borderRadius: 2 }}>
      {content}
    </Paper>
  );
}

export default function AdminDashboardPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { game, isLoading: gameLoading } = useGame(id);
  const { players } = usePlayers(id);
  const { threads } = usePlotWeaver(id);
  const { characters } = useCharacters(id);
  const { npcs } = useNPCs(id);
  const { invoke } = useGameHub();
  const [agentCallCount, setAgentCallCount] = useState(0);
  const [llmLogCount, setLlmLogCount] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    (async () => {
      try {
        const calls = await invoke('GetAgentCallHistory', id, undefined, undefined, 1);
        setAgentCallCount(calls?.length || 0);
        const logs = await invoke('GetLLMInteractions', id, undefined, undefined, undefined, undefined, 1);
        setLlmLogCount(logs?.length || 0);
      } catch { /* ignore */ }
      setLoading(false);
    })();
  }, [id, invoke]);

  if (gameLoading) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading...</Typography></Box>;
  if (!game) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography variant="h5" color="error">Game not found</Typography></Box>;

  const gmStatusColor = game.gmStatus === 'running' ? 'success' : game.gmStatus === 'paused' ? 'warning' : 'default';
  const gameStatusColor = game.status === 'Active' ? 'success' : game.status === 'Draft' ? 'default' : 'warning';

  return (
    <Box sx={{ p: { xs: 2, sm: 3 } }}>
      {/* Game Header */}
      <Paper sx={{ p: 3, mb: 3, borderRadius: 2 }}>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 2, mb: 2 }}>
          <Typography variant="h5" fontWeight={700}>{game.name}</Typography>
          <Chip label={game.status} color={gameStatusColor as any} size="small" />
          <Chip label={`GM: ${game.gmStatus}`} color={gmStatusColor as any} size="small" />
          <Chip label={`${game.systemId} v${game.systemVersion || 'unknown'}`} size="small" variant="outlined" />
        </Box>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
          <Button size="small" variant="outlined" onClick={() => navigate(`/game/${id}`)} startIcon={<ChatIcon />}>
            Go to Game
          </Button>
          <Button size="small" variant="outlined" onClick={() => navigate(`/game/${id}/combat`)} startIcon={<CombatIcon />}>
            Combat
          </Button>
          <Button size="small" variant="outlined" onClick={() => navigate(`/game/${id}/settings`)} startIcon={<SettingsIcon />}>
            Settings
          </Button>
        </Box>
      </Paper>

      {/* Stat Cards */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2 }}>
          <QuickStatCard
            title="Players"
            value={players?.length || 0}
            icon={<PeopleIcon fontSize="small" />}
            sub={`${players?.filter(p => p.status !== 'Left').length || 0} active`}
            color="success"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2 }}>
          <QuickStatCard
            title="Plot Threads"
            value={threads?.length || 0}
            icon={<PlotIcon fontSize="small" />}
            link={`/admin/${id}/plot-board`}
            color="info"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2 }}>
          <QuickStatCard
            title="Characters"
            value={characters?.length || 0}
            icon={<CharactersIcon fontSize="small" />}
            link={`/admin/${id}/characters`}
            color="primary"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2 }}>
          <QuickStatCard
            title="NPCs"
            value={npcs?.length || 0}
            icon={<PeopleIcon fontSize="small" />}
            link={`/admin/${id}/npcs`}
            color="warning"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2 }}>
          <QuickStatCard
            title="Agent Calls"
            value={agentCallCount}
            icon={<AgentIcon fontSize="small" />}
            link={`/admin/${id}/agent-calls`}
            color="secondary"
            isLoading={loading}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2 }}>
          <QuickStatCard
            title="LLM Logs"
            value={llmLogCount}
            icon={<LogsIcon fontSize="small" />}
            link={`/admin/${id}/llm-logs`}
            color="error"
            isLoading={loading}
          />
        </Grid>
      </Grid>

      <Divider sx={{ my: 2, borderColor: 'rgba(255,255,255,0.08)' }} />

      {/* Quick Actions */}
      <Typography variant="subtitle2" sx={{ mb: 1.5, color: 'text.secondary', textTransform: 'uppercase', fontWeight: 600 }}>
        Quick Actions
      </Typography>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, mb: 3 }}>
        <Button variant="outlined" startIcon={<PlotIcon />} onClick={() => navigate(`/admin/${id}/plot-board`)}>
          Plot Board
        </Button>
        <Button variant="outlined" startIcon={<ConsistencyIcon />} onClick={() => navigate(`/admin/${id}/consistency`)}>
          Consistency Check
        </Button>
        <Button variant="outlined" startIcon={<AgentIcon />} onClick={() => navigate(`/admin/${id}/agent-calls`)}>
          Agent Calls
        </Button>
        <Button variant="outlined" startIcon={<LogsIcon />} onClick={() => navigate(`/admin/${id}/llm-logs`)}>
          LLM Logs
        </Button>
      </Box>

      {/* Game Details */}
      <Paper sx={{ p: 3, borderRadius: 2 }}>
        <Typography variant="subtitle1" sx={{ mb: 2, fontWeight: 600 }}>Game Details</Typography>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 2 }}>
          <Box>
            <Typography variant="caption" color="text.secondary">Game ID</Typography>
            <Typography variant="body2" fontFamily="monospace" fontSize={11}>{id}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">Created</Typography>
            <Typography variant="body2">{game.createdAt ? new Date(game.createdAt).toLocaleString() : '—'}</Typography>
          </Box>
          <Box>
            <Typography variant="caption" color="text.secondary">Started</Typography>
            <Typography variant="body2">—</Typography>
          </Box>
          {game.language && (
            <Box>
              <Typography variant="caption" color="text.secondary">Language</Typography>
              <Typography variant="body2">{game.language}</Typography>
            </Box>
          )}
          {game.llmPresetName && (
            <Box>
              <Typography variant="caption" color="text.secondary">LLM Preset</Typography>
              <Typography variant="body2">{game.llmPresetName}</Typography>
            </Box>
          )}
          {game.inviteCode && (
            <Box>
              <Typography variant="caption" color="text.secondary">Invite Code</Typography>
              <Typography variant="body2" fontFamily="monospace">{game.inviteCode}</Typography>
            </Box>
          )}
        </Box>
      </Paper>
    </Box>
  );
}
