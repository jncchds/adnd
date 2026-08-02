import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  Box, Typography, Paper, Button, Select, MenuItem,
  FormControl, InputLabel, Alert, CircularProgress, Divider,
  Dialog, DialogTitle, DialogContent, DialogActions,
} from '@mui/material'
import { useGame } from '../api/hooks/useGame'
import { api } from '../api/client'
import type { LLMPreset } from '../types'

export default function GameSettingsPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { game, refresh } = useGame(gameId ?? null)
  const [presets, setPresets] = useState<LLMPreset[]>([])
  const [llmPresetId, setLlmPresetId] = useState('')
  const [inviteCode, setInviteCode] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deleting, setDeleting] = useState(false)

  useEffect(() => { api.llmPresets.list().then(setPresets).catch(() => {}) }, [])
  useEffect(() => { if (game) setLlmPresetId(game.llmPresetId ?? '') }, [game])

  const save = async () => {
    if (!gameId) return
    setSaving(true)
    setError(null)
    setSuccess(false)
    try {
      await api.games.update(gameId, { llmPresetId: llmPresetId || undefined })
      setSuccess(true)
      refresh()
    } catch (e) { setError((e as Error).message) }
    finally { setSaving(false) }
  }

  const handleDelete = async () => {
    if (!gameId) return
    setDeleting(true)
    try {
      await api.games.archive(gameId)
      navigate('/dashboard')
    } catch (e) { setError((e as Error).message) }
    finally { setDeleting(false) }
  }

  const genInvite = async () => {
    if (!gameId) return
    try {
      const { inviteCode } = await api.games.generateInvite(gameId)
      setInviteCode(inviteCode)
    } catch (e) { setError((e as Error).message) }
  }

  if (!game) return <Box sx={{ p: 3 }}><CircularProgress /></Box>

  return (
    <Box sx={{ p: 3, maxWidth: 600 }}>
      <Typography variant="h5" fontWeight={700} sx={{ mb: 3 }}>Game Settings — {game.name}</Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }}>Settings saved.</Alert>}

      <Paper sx={{ p: 2, mb: 2 }}>
        <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 2 }}>LLM Configuration</Typography>
        <FormControl fullWidth>
          <InputLabel>LLM Preset</InputLabel>
          <Select value={llmPresetId} label="LLM Preset" onChange={e => setLlmPresetId(e.target.value)}>
            <MenuItem value=""><em>None</em></MenuItem>
            {presets.map(p => <MenuItem key={p.id} value={p.id}>{p.name} ({p.providerType})</MenuItem>)}
          </Select>
        </FormControl>
        <Button variant="contained" sx={{ mt: 2 }} onClick={save} disabled={saving}>
          {saving ? <CircularProgress size={18} /> : 'Save Settings'}
        </Button>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="subtitle2" fontWeight={600} sx={{ mb: 2 }}>Invite Code</Typography>
        <Divider sx={{ mb: 2 }} />
        <Button variant="outlined" onClick={genInvite}>Generate Invite Code</Button>
        {inviteCode && (
          <Alert severity="success" sx={{ mt: 1.5 }}>
            Code: <strong>{inviteCode}</strong> — share with players to join this game
          </Alert>
        )}
        {game.inviteCode && !inviteCode && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            Current code: <strong>{game.inviteCode}</strong>
          </Typography>
        )}
      </Paper>

      <Paper sx={{ p: 2, mb: 2, border: '1px solid', borderColor: 'error.main' }}>
        <Typography variant="subtitle2" fontWeight={600} color="error" sx={{ mb: 1 }}>Danger Zone</Typography>
        <Divider sx={{ mb: 2 }} />
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2 }}>
          <Box>
            <Typography variant="body2" fontWeight={500}>Archive this game</Typography>
            <Typography variant="caption" color="text.secondary">
              Stops all GM processing and hides the game from everyone's list. This cannot be undone.
            </Typography>
          </Box>
          <Button variant="outlined" color="error" sx={{ flexShrink: 0 }} onClick={() => setDeleteOpen(true)}>
            Archive Game
          </Button>
        </Box>
      </Paper>

      <Dialog open={deleteOpen} onClose={() => setDeleteOpen(false)}>
        <DialogTitle>Archive "{game.name}"?</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            This will stop all GM processing and hide the game from everyone's list.
            The game will remain accessible in the archived games list (read-only).
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteOpen(false)}>Cancel</Button>
          <Button variant="contained" color="error" onClick={handleDelete} disabled={deleting}>
            {deleting ? <CircularProgress size={18} /> : 'Archive Game'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
