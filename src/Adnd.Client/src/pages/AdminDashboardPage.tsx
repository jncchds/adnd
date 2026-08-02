import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  Box, Typography, Paper, Chip, Button, Grid, CircularProgress,
  Divider, Alert, Tooltip, IconButton,
} from '@mui/material'
import {
  PlayArrow as StartIcon, Archive as ArchiveIcon,
  Pause as PauseIcon, PlayCircle as ResumeIcon,
  Share as InviteIcon, Refresh as RefreshIcon,
} from '@mui/icons-material'
import { useGame } from '../api/hooks/useGame'
import { usePlayers } from '../api/hooks/usePlayers'
import { api } from '../api/client'

function StatCard({ label, value, color }: { label: string; value: number | string; color?: string }) {
  return (
    <Paper sx={{ p: 2, textAlign: 'center' }}>
      <Typography variant="h4" fontWeight={700} color={color ?? 'primary.main'}>{value}</Typography>
      <Typography variant="caption" color="text.secondary">{label}</Typography>
    </Paper>
  )
}

export default function AdminDashboardPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const { game, refresh: refreshGame } = useGame(gameId ?? null)
  const { players } = usePlayers(gameId ?? null)
  const navigate = useNavigate()
  const [stats, setStats] = useState({ plots: 0, npcs: 0, agentCalls: 0, llmLogs: 0 })
  const [loading, setLoading] = useState(false)
  const [invite, setInvite] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!gameId) return
    Promise.all([
      api.plots.list(gameId),
      api.npcs.list(gameId),
      api.agentCalls.list(gameId),
      api.llmLogs.list(gameId),
    ]).then(([plots, npcs, calls, logs]) => {
      setStats({ plots: plots.length, npcs: npcs.length, agentCalls: calls.length, llmLogs: logs.total })
    }).catch(() => {})
  }, [gameId])

  const handle = async (fn: () => Promise<unknown>) => {
    setLoading(true)
    setError(null)
    try { await fn(); refreshGame() }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  const genInvite = async () => {
    if (!gameId) return
    try {
      const { inviteCode } = await api.games.generateInvite(gameId)
      setInvite(inviteCode)
    } catch (e) { setError((e as Error).message) }
  }

  if (!game) return <Box sx={{ p: 3 }}><CircularProgress /></Box>

  const gmLinks = [
    { label: 'Plot Board', path: `/admin/${gameId}/plot-board` },
    { label: 'NPCs', path: `/admin/${gameId}/npcs` },
    { label: 'Characters', path: `/admin/${gameId}/characters` },
    { label: 'Consistency', path: `/admin/${gameId}/consistency` },
    { label: 'LLM Logs', path: `/admin/${gameId}/llm-logs` },
    { label: 'Agent Calls', path: `/admin/${gameId}/agent-calls` },
    { label: 'Settings', path: `/admin/${gameId}/settings` },
  ]

  return (
    <Box sx={{ p: 3 }}>
      {/* Header */}
      <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', mb: 3, flexWrap: 'wrap', gap: 2 }}>
        <Box>
          <Typography variant="h5" fontWeight={700}>{game.name}</Typography>
          <Box sx={{ display: 'flex', gap: 1, mt: 0.5, flexWrap: 'wrap' }}>
            <Chip label={game.status} size="small" color={game.status === 'Active' ? 'success' : 'default'} />
            <Chip label={`GM: ${game.gmStatus}`} size="small" color={game.gmStatus === 'Running' ? 'primary' : 'default'} />
            <Chip label={game.systemId} size="small" variant="outlined" />
          </Box>
        </Box>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          {gmLinks.map(l => (
            <Button key={l.path} size="small" variant="outlined" onClick={() => navigate(l.path)}>{l.label}</Button>
          ))}
        </Box>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {/* Stat cards */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 6, sm: 4, md: 2 }}><StatCard label="Players" value={players.length} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 2 }}><StatCard label="Plot Threads" value={stats.plots} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 2 }}><StatCard label="NPCs" value={stats.npcs} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 2 }}><StatCard label="Agent Calls" value={stats.agentCalls} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 2 }}><StatCard label="LLM Logs" value={stats.llmLogs} /></Grid>
      </Grid>

      {/* Controls */}
      <Paper sx={{ p: 2, mb: 2 }}>
        <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 1.5 }}>Game Controls</Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          {game.status === 'Draft' && (
            <Button variant="contained" startIcon={<StartIcon />} disabled={loading}
              onClick={() => handle(() => api.games.start(gameId!))}>
              Start Game
            </Button>
          )}
          {game.status === 'Active' && (
            <Button variant="outlined" color="warning" startIcon={<ArchiveIcon />} disabled={loading}
              onClick={() => { if (confirm('Archive?')) handle(() => api.games.archive(gameId!)) }}>
              Archive
            </Button>
          )}
          {game.gmStatus === 'Running' && (
            <Button variant="outlined" startIcon={<PauseIcon />} disabled={loading}
              onClick={() => handle(() => api.games.pauseGM(gameId!))}>
              Pause GM
            </Button>
          )}
          {game.status === 'Active' && (game.gmStatus === 'Paused' || game.gmStatus === 'Idle') && (
            <Button variant="outlined" color="success" startIcon={<ResumeIcon />} disabled={loading}
              onClick={() => handle(() => api.games.resumeGM(gameId!))}>
              Resume GM
            </Button>
          )}
          <Tooltip title="Generate invite code">
            <Button variant="outlined" startIcon={<InviteIcon />} onClick={genInvite}>
              Invite Code
            </Button>
          </Tooltip>
          <Tooltip title="Refresh">
            <IconButton onClick={refreshGame}><RefreshIcon /></IconButton>
          </Tooltip>
        </Box>
        {invite && (
          <Alert severity="success" sx={{ mt: 1.5 }} onClose={() => setInvite(null)}>
            Invite code: <strong>{invite}</strong> (share this with players)
          </Alert>
        )}
      </Paper>

      {/* Details */}
      <Paper sx={{ p: 2 }}>
        <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 1.5 }}>Game Details</Typography>
        <Divider sx={{ mb: 1.5 }} />
        {[
          ['ID', game.id],
          ['System', game.systemId],
          ['Language', game.language],
          ['LLM Preset', game.llmPresetId ?? 'None'],
          ['Session', game.currentSessionId ?? 'None'],
          ['Last GM Action', game.lastGMAction ?? '—'],
          ['Created', new Date(game.createdAt).toLocaleString()],
        ].map(([label, value]) => (
          <Box key={label} sx={{ display: 'flex', gap: 2, mb: 0.75 }}>
            <Typography variant="body2" color="text.secondary" sx={{ width: 120, flexShrink: 0 }}>{label}</Typography>
            <Typography variant="body2" sx={{ wordBreak: 'break-all' }}>{value}</Typography>
          </Box>
        ))}
      </Paper>
    </Box>
  )
}
