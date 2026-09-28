import { useCallback, useEffect, useState } from 'react'
import {
  Box, Typography, TextField, Button, Alert, Paper, Chip, IconButton, Tooltip, MenuItem,
  CircularProgress,
} from '@mui/material'
import { Close as WithdrawIcon } from '@mui/icons-material'
import { api } from '../api/client'
import type { StoryNudge } from '../types'

const DURATIONS: { label: string; turns: number | null }[] = [
  { label: 'Next GM turn', turns: 1 },
  { label: 'Next 3 GM turns', turns: 3 },
  { label: 'Next 10 GM turns', turns: 10 },
  { label: 'Until I withdraw it', turns: null },
]

/**
 * The game creator's one lever on the story. The LLM is the GM; the creator does not roll,
 * deal damage or move anyone — they can only say which way they'd like the story to lean.
 * A nudge reaches the narrator and the plot weaver, and never the players.
 */
export default function NudgePanel({ gameId }: { gameId: string }) {
  const [nudges, setNudges] = useState<StoryNudge[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [text, setText] = useState('')
  const [duration, setDuration] = useState(1)
  const [saving, setSaving] = useState(false)

  const refresh = useCallback(async () => {
    try {
      setNudges(await api.nudges.list(gameId))
      setError(null)
    } catch (e) {
      setError((e as Error).message || 'Could not load nudges.')
    } finally {
      setLoading(false)
    }
  }, [gameId])

  useEffect(() => { refresh() }, [refresh])

  const submit = async () => {
    if (!text.trim()) return
    setSaving(true)
    try {
      await api.nudges.create(gameId, text.trim(), DURATIONS[duration].turns)
      setText('')
      await refresh()
    } catch (e) {
      setError((e as Error).message || 'Could not add the nudge.')
    } finally {
      setSaving(false)
    }
  }

  const withdraw = async (id: string) => {
    try {
      await api.nudges.withdraw(gameId, id)
      await refresh()
    } catch (e) {
      setError((e as Error).message || 'Could not withdraw the nudge.')
    }
  }

  const active = nudges.filter(n => !n.expiredAt)
  const past = nudges.filter(n => n.expiredAt)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Typography variant="body2" color="text.secondary">
        Tell the GM which way you'd like the story to go. It steers toward it through the world
        over the next turns without mentioning it, and the players never see it.
      </Typography>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}

      <TextField
        multiline minRows={2} maxRows={6} size="small" fullWidth
        placeholder="e.g. Bring the smugglers back into it — someone from the docks recognises the party."
        value={text} onChange={e => setText(e.target.value)}
        inputProps={{ maxLength: 2000 }}
      />
      <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
        <TextField select size="small" value={duration} onChange={e => setDuration(Number(e.target.value))} sx={{ minWidth: 200 }}>
          {DURATIONS.map((d, i) => <MenuItem key={d.label} value={i}>{d.label}</MenuItem>)}
        </TextField>
        <Button variant="contained" onClick={submit} disabled={saving || !text.trim()}>Nudge</Button>
      </Box>

      {loading ? <CircularProgress size={20} /> : (
        <>
          {active.length > 0 && (
            <Typography variant="overline" color="text.secondary">In effect</Typography>
          )}
          {active.map(n => (
            <Paper key={n.id} variant="outlined" sx={{ p: 1, display: 'flex', gap: 1, alignItems: 'flex-start' }}>
              <Typography variant="body2" sx={{ flex: 1, whiteSpace: 'pre-wrap' }}>{n.text}</Typography>
              <Chip size="small" label={n.turnsRemaining == null ? 'until withdrawn' : `${n.turnsRemaining} turn${n.turnsRemaining === 1 ? '' : 's'} left`} />
              <Tooltip title="Withdraw">
                <IconButton size="small" onClick={() => withdraw(n.id)}><WithdrawIcon fontSize="small" /></IconButton>
              </Tooltip>
            </Paper>
          ))}
          {past.length > 0 && (
            <Typography variant="overline" color="text.secondary">Earlier</Typography>
          )}
          {past.slice(0, 10).map(n => (
            <Typography key={n.id} variant="caption" color="text.disabled" sx={{ whiteSpace: 'pre-wrap' }}>
              {new Date(n.createdAt).toLocaleString()} — {n.text}
            </Typography>
          ))}
        </>
      )}
    </Box>
  )
}
