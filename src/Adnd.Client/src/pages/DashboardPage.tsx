import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Box, Typography, Button, Card, CardContent, CardActions, Chip,
  Dialog, DialogTitle, DialogContent, DialogActions, TextField,
  Select, MenuItem, FormControl, InputLabel, CircularProgress, Alert,
  IconButton, Tooltip, Collapse,
} from '@mui/material'
import { Add as AddIcon, Login as JoinIcon, PlayArrow as StartIcon, Archive as ArchiveIcon, Settings as SettingsIcon, ExpandMore, ExpandLess } from '@mui/icons-material'
import type { Game } from '../types'
import { useGames } from '../api/hooks/useGame'
import { useAuth } from '../context/AuthContext'
import { api } from '../api/client'
import type { GameStatus, LLMPreset } from '../types'

const STATUS_COLORS: Record<GameStatus, 'default' | 'info' | 'success' | 'warning'> = {
  Draft: 'default', Starting: 'info', Active: 'success', Archived: 'warning',
}

const SYSTEMS = [
  { id: 'dnd5e', name: 'D&D 5th Edition' },
  { id: 'pf2e', name: 'Pathfinder 2e' },
  { id: 'coc7e', name: 'Call of Cthulhu 7e' },
  { id: 'custom', name: 'Custom' },
]

export default function DashboardPage() {
  const { games, loading, error, refresh } = useGames()
  const { user } = useAuth()
  const navigate = useNavigate()
  const [archivedGames, setArchivedGames] = useState<Game[]>([])
  const [archivedOpen, setArchivedOpen] = useState(false)
  const [archivedLoading, setArchivedLoading] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [joinCode, setJoinCode] = useState('')
  const [joinError, setJoinError] = useState('')
  const [joining, setJoining] = useState(false)
  const [presets, setPresets] = useState<LLMPreset[]>([])
  const [form, setForm] = useState({ name: '', systemId: 'dnd5e', language: 'English', plotSeed: '', llmPresetId: '' })
  const [creating, setCreating] = useState(false)
  const [createError, setCreateError] = useState('')

  useEffect(() => {
    if (createOpen) api.llmPresets.list().then(setPresets).catch(() => {})
  }, [createOpen])

  useEffect(() => {
    if (!archivedOpen) return
    setArchivedLoading(true)
    api.games.listArchived().then(setArchivedGames).catch(() => {}).finally(() => setArchivedLoading(false))
  }, [archivedOpen])

  const handleCreate = async () => {
    if (!form.name.trim()) { setCreateError('Game name is required'); return }
    setCreating(true)
    setCreateError('')
    try {
      const game = await api.games.create({
        name: form.name,
        systemId: form.systemId,
        language: form.language,
        plotSeed: form.plotSeed || undefined,
        llmPresetId: form.llmPresetId || undefined,
      })
      setCreateOpen(false)
      setForm({ name: '', systemId: 'dnd5e', language: 'English', plotSeed: '', llmPresetId: '' })
      refresh()
      navigate(`/admin/${game.id}`)
    } catch (e) {
      setCreateError((e as Error).message)
    } finally {
      setCreating(false)
    }
  }

  const handleJoin = async () => {
    if (!joinCode.trim() || joining) return
    setJoinError('')
    setJoining(true)
    try {
      const game = await api.games.joinByCode(joinCode.trim())
      navigate(`/game/${game.id}`)
    } catch (e) {
      setJoinError((e as Error).message)
    } finally {
      setJoining(false)
    }
  }

  const handleStart = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation()
    try { await api.games.start(id); refresh() } catch (err) { alert((err as Error).message) }
  }

  const handleArchive = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (!confirm('Archive this game?')) return
    try { await api.games.archive(id); refresh() } catch (err) { alert((err as Error).message) }
  }

  return (
    <Box sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" fontWeight={700}>Games</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
          New Game
        </Button>
      </Box>

      {/* Join by invite code. Character creation happens in-game after joining. */}
      <Box sx={{ display: 'flex', gap: 1, mb: 3, maxWidth: 400, alignItems: 'flex-start' }}>
        <TextField
          size="small" label="Invite Code" value={joinCode}
          onChange={e => setJoinCode(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleJoin()}
          error={!!joinError} helperText={joinError}
          fullWidth
        />
        <Button
          variant="outlined"
          startIcon={<JoinIcon />}
          onClick={handleJoin}
          disabled={joining || !joinCode.trim()}
          sx={{ flexShrink: 0 }}
        >
          {joining ? 'Joining…' : 'Join'}
        </Button>
      </Box>

      {loading && <CircularProgress />}
      {error && <Alert severity="error">{error}</Alert>}

      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 2 }}>
        {games.map(game => (
          <Card
            key={game.id}
            sx={{ cursor: 'pointer', '&:hover': { boxShadow: 4 }, transition: 'box-shadow 0.15s' }}
            onClick={() => navigate(`/game/${game.id}`)}
          >
            <CardContent>
              <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', mb: 1 }}>
                <Typography variant="h6" fontWeight={600} noWrap>{game.name}</Typography>
                <Chip label={game.status} size="small" color={STATUS_COLORS[game.status]} />
              </Box>
              <Typography variant="body2" color="text.secondary">
                {SYSTEMS.find(s => s.id === game.systemId)?.name ?? game.systemId}
              </Typography>
              {game.language && game.language !== 'English' && (
                <Typography variant="caption" color="text.secondary"> · {game.language}</Typography>
              )}
            </CardContent>
            <CardActions sx={{ justifyContent: 'flex-end', pt: 0 }}>
              {game.status === 'Draft' && (
                <Button size="small" startIcon={<StartIcon />} onClick={e => handleStart(game.id, e)}>
                  Start
                </Button>
              )}
              {game.status === 'Active' && (
                <Tooltip title="Archive game">
                  <IconButton size="small" onClick={e => handleArchive(game.id, e)}>
                    <ArchiveIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
              {game.creatorId === user?.id && (
                <Tooltip title="Game Admin">
                  <IconButton size="small" onClick={e => { e.stopPropagation(); navigate(`/admin/${game.id}`) }}>
                    <SettingsIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
            </CardActions>
          </Card>
        ))}
      </Box>

      {/* Archived games */}
      <Box sx={{ mt: 4 }}>
        <Box
          sx={{ display: 'flex', alignItems: 'center', gap: 1, cursor: 'pointer', mb: 1, userSelect: 'none' }}
          onClick={() => setArchivedOpen(o => !o)}
        >
          <IconButton size="small">{archivedOpen ? <ExpandLess /> : <ExpandMore />}</IconButton>
          <Typography variant="subtitle2" color="text.secondary">Archived Games</Typography>
          {archivedLoading && <CircularProgress size={14} />}
        </Box>
        <Collapse in={archivedOpen}>
          {archivedGames.length === 0 && !archivedLoading && (
            <Typography variant="body2" color="text.secondary" sx={{ pl: 5 }}>No archived games.</Typography>
          )}
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 2 }}>
            {archivedGames.map(game => (
              <Card key={game.id} sx={{ opacity: 0.7 }}>
                <CardContent>
                  <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', mb: 1 }}>
                    <Typography variant="h6" fontWeight={600} noWrap>{game.name}</Typography>
                    <Chip label="Archived" size="small" color="warning" />
                  </Box>
                  <Typography variant="body2" color="text.secondary">
                    {SYSTEMS.find(s => s.id === game.systemId)?.name ?? game.systemId}
                  </Typography>
                  {game.deletedAt && (
                    <Typography variant="caption" color="text.secondary">
                      Archived {new Date(game.deletedAt).toLocaleDateString()}
                    </Typography>
                  )}
                </CardContent>
              </Card>
            ))}
          </Box>
        </Collapse>
      </Box>

      {/* Create game dialog */}
      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Create Game</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
          {createError && <Alert severity="error">{createError}</Alert>}
          <TextField label="Game Name" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} fullWidth />
          <FormControl fullWidth>
            <InputLabel>RPG System</InputLabel>
            <Select value={form.systemId} label="RPG System" onChange={e => setForm(f => ({ ...f, systemId: e.target.value }))}>
              {SYSTEMS.map(s => <MenuItem key={s.id} value={s.id}>{s.name}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControl fullWidth>
            <InputLabel>Language</InputLabel>
            <Select value={form.language} label="Language" onChange={e => setForm(f => ({ ...f, language: e.target.value }))}>
              {['English', 'Spanish', 'French', 'German', 'Italian', 'Portuguese', 'Polish', 'Russian', 'Japanese'].map(l =>
                <MenuItem key={l} value={l}>{l}</MenuItem>
              )}
            </Select>
          </FormControl>
          <FormControl fullWidth>
            <InputLabel>LLM Preset</InputLabel>
            <Select value={form.llmPresetId} label="LLM Preset" onChange={e => setForm(f => ({ ...f, llmPresetId: e.target.value }))}>
              <MenuItem value=""><em>None</em></MenuItem>
              {presets.map(p => <MenuItem key={p.id} value={p.id}>{p.name} ({p.providerType})</MenuItem>)}
            </Select>
          </FormControl>
          <TextField
            label="Plot Seed (optional)" multiline rows={3}
            value={form.plotSeed} onChange={e => setForm(f => ({ ...f, plotSeed: e.target.value }))}
            fullWidth placeholder="Describe the starting scenario for your game..."
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleCreate} disabled={creating}>
            {creating ? <CircularProgress size={18} /> : 'Create'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
