import { useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, Paper, Button, CircularProgress, Alert, Chip,
  Dialog, DialogTitle, DialogContent, DialogActions, TextField,
  Select, MenuItem, FormControl, InputLabel, IconButton,
} from '@mui/material'
import { Add as AddIcon, Delete as DeleteIcon, Edit as EditIcon } from '@mui/icons-material'
import { useNPCs } from '../api/hooks/useNPCs'
import { api } from '../api/client'
import type { NPC } from '../types'

const ATTITUDES = ['Friendly', 'Neutral', 'Unfriendly', 'Hostile'] as const
const STATUSES = ['Active', 'Dead', 'Departed'] as const

export default function AdminNPCsPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const { npcs, loading, refresh } = useNPCs(gameId ?? null)
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<NPC | null>(null)
  const [form, setForm] = useState({ name: '', description: '', attitude: 'Neutral', faction: '', status: 'Active' })
  const [error, setError] = useState<string | null>(null)

  const openCreate = () => { setEditing(null); setForm({ name: '', description: '', attitude: 'Neutral', faction: '', status: 'Active' }); setOpen(true) }
  const openEdit = (npc: NPC) => {
    setEditing(npc)
    setForm({
      name: npc.name, description: npc.description ?? '', attitude: npc.attitude ?? 'Neutral',
      faction: npc.faction ?? '', status: npc.status ?? 'Active',
    })
    setOpen(true)
  }

  const handleSave = async () => {
    if (!gameId || !form.name.trim()) return
    try {
      const data = {
        gameId, name: form.name, description: form.description,
        attitude: form.attitude as NPC['attitude'], faction: form.faction || null,
        status: form.status as NPC['status'],
      }
      if (editing) await api.npcs.update(editing.id, data)
      else await api.npcs.create(data)
      setOpen(false)
      refresh()
    } catch (e) { setError((e as Error).message) }
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Delete this NPC?')) return
    try { await api.npcs.delete(id); refresh() }
    catch (e) { setError((e as Error).message) }
  }

  const attitudeColor = (a?: string) => ({ Friendly: 'success', Neutral: 'default', Unfriendly: 'warning', Hostile: 'error' }[a ?? 'Neutral'] as 'success' | 'default' | 'warning' | 'error')

  return (
    <Box sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" fontWeight={700}>NPCs</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>Add NPC</Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
      {loading && <CircularProgress />}

      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: 2 }}>
        {npcs.map(npc => (
          <Paper key={npc.id} sx={{ p: 2 }}>
            <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
              <Box sx={{ flex: 1 }}>
                <Typography variant="subtitle2" fontWeight={600}>{npc.name}</Typography>
                <Box sx={{ display: 'flex', gap: 0.5, mt: 0.5, flexWrap: 'wrap' }}>
                  {npc.attitude && <Chip label={npc.attitude} size="small" color={attitudeColor(npc.attitude)} />}
                  {npc.faction && <Chip label={npc.faction} size="small" variant="outlined" />}
                  {/* Only the exceptions are worth a chip — an Active badge on every card is noise. */}
                  {npc.status && npc.status !== 'Active' && (
                    <Chip label={npc.status} size="small" variant="outlined" color="default" />
                  )}
                </Box>
              </Box>
              <Box>
                <IconButton size="small" onClick={() => openEdit(npc)}><EditIcon fontSize="small" /></IconButton>
                <IconButton size="small" onClick={() => handleDelete(npc.id)}><DeleteIcon fontSize="small" /></IconButton>
              </Box>
            </Box>
            {npc.description && (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 1, fontSize: 12 }}>
                {npc.description}
              </Typography>
            )}
          </Paper>
        ))}
      </Box>

      <Dialog open={open} onClose={() => setOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editing ? 'Edit NPC' : 'Add NPC'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
          <TextField label="Name" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} fullWidth />
          <TextField label="Description" multiline rows={3} value={form.description}
            onChange={e => setForm(f => ({ ...f, description: e.target.value }))} fullWidth />
          <FormControl fullWidth>
            <InputLabel>Attitude</InputLabel>
            <Select value={form.attitude} label="Attitude" onChange={e => setForm(f => ({ ...f, attitude: e.target.value }))}>
              {ATTITUDES.map(a => <MenuItem key={a} value={a}>{a}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControl fullWidth>
            <InputLabel>Status</InputLabel>
            <Select value={form.status} label="Status" onChange={e => setForm(f => ({ ...f, status: e.target.value }))}>
              {STATUSES.map(s => <MenuItem key={s} value={s}>{s}</MenuItem>)}
            </Select>
          </FormControl>
          <TextField label="Faction (optional)" value={form.faction}
            onChange={e => setForm(f => ({ ...f, faction: e.target.value }))} fullWidth />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSave}>Save</Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
