import { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, Paper, TextField, Button, Select, MenuItem,
  FormControl, InputLabel, Alert, CircularProgress, Divider,
} from '@mui/material'
import { useGame } from '../api/hooks/useGame'
import { api } from '../api/client'
import type { LLMPreset } from '../types'

export default function GameSettingsPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const { game, refresh } = useGame(gameId ?? null)
  const [presets, setPresets] = useState<LLMPreset[]>([])
  const [llmPresetId, setLlmPresetId] = useState('')
  const [inviteCode, setInviteCode] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

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

  const genInvite = async () => {
    if (!gameId) return
    try {
      const { code } = await api.games.generateInvite(gameId)
      setInviteCode(code)
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
    </Box>
  )
}
