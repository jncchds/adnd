import { useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  Box, Typography, Paper, Chip, Button, CircularProgress, Alert,
  LinearProgress, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Select, MenuItem, FormControl, InputLabel, IconButton,
} from '@mui/material'
import { Add as AddIcon, Refresh as RefreshIcon, Delete as DeleteIcon } from '@mui/icons-material'
import { usePlotThreads } from '../api/hooks/usePlotWeaver'
import { api } from '../api/client'
import type { PlotThreadCategory, PlotThreadStatus } from '../types'

const CATEGORIES: PlotThreadCategory[] = ['General', 'Faction', 'Mystery', 'Personal', 'Threat', 'WorldEvent', 'Relationship']
const STATUS_COLORS: Record<PlotThreadStatus, 'default' | 'success' | 'error'> = {
  Active: 'success', Resolved: 'default', Abandoned: 'error',
}

function MomentumBar({ value }: { value: number }) {
  const pct = ((value + 10) / 20) * 100
  const color = value > 3 ? 'success' : value < -3 ? 'error' : 'warning'
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <LinearProgress variant="determinate" value={pct} color={color} sx={{ flex: 1, height: 6, borderRadius: 3 }} />
      <Typography variant="caption" color="text.secondary">{value.toFixed(1)}</Typography>
    </Box>
  )
}

export default function AdminPlotBoardPage() {
  const { id: gameId } = useParams<{ id: string }>()
  const { threads, loading, refresh } = usePlotThreads(gameId ?? null)
  const [addOpen, setAddOpen] = useState(false)
  const [form, setForm] = useState({ title: '', description: '', category: 'General' as PlotThreadCategory })
  const [error, setError] = useState<string | null>(null)
  const [reviewing, setReviewing] = useState(false)

  const handleAdd = async () => {
    if (!gameId || !form.title.trim()) return
    try {
      await api.plots.create({ ...form, gameId, status: 'Active', momentum: 0, relevanceScore: 0.5, isDynamic: true })
      setAddOpen(false)
      setForm({ title: '', description: '', category: 'General' })
      refresh()
    } catch (e) { setError((e as Error).message) }
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Delete this thread?')) return
    try { await api.plots.delete(id); refresh() }
    catch (e) { setError((e as Error).message) }
  }

  const handleReview = async () => {
    if (!gameId) return
    setReviewing(true)
    try { await api.plots.review(gameId); refresh() }
    catch (e) { setError((e as Error).message) }
    finally { setReviewing(false) }
  }

  return (
    <Box sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" fontWeight={700}>Plot Board</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button variant="outlined" onClick={handleReview} disabled={reviewing}>
            {reviewing ? <CircularProgress size={18} /> : 'PlotWeaver Review'}
          </Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setAddOpen(true)}>Add Thread</Button>
          <IconButton onClick={refresh}><RefreshIcon /></IconButton>
        </Box>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
      {loading && <CircularProgress />}

      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 2 }}>
        {threads.map(t => (
          <Paper key={t.id} sx={{ p: 2 }}>
            <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', mb: 1 }}>
              <Box sx={{ flex: 1 }}>
                <Typography variant="subtitle2" fontWeight={600}>{t.title}</Typography>
                <Box sx={{ display: 'flex', gap: 0.5, mt: 0.5, flexWrap: 'wrap' }}>
                  <Chip label={t.status} size="small" color={STATUS_COLORS[t.status]} />
                  <Chip label={t.category} size="small" variant="outlined" />
                </Box>
              </Box>
              <IconButton size="small" onClick={() => handleDelete(t.id)}>
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Box>
            {t.description && (
              <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5, fontSize: 12 }}>
                {t.description}
              </Typography>
            )}
            <Box sx={{ mb: 1 }}>
              <Typography variant="caption" color="text.secondary">Momentum</Typography>
              <MomentumBar value={t.momentum} />
            </Box>
            {t.nextMilestone && (
              <Typography variant="caption" color="primary">
                Next: {t.nextMilestone}
              </Typography>
            )}
            {t.adaptationHistory?.length > 0 && (
              <Typography variant="caption" color="text.disabled" sx={{ display: 'block', mt: 0.5 }}>
                {t.adaptationHistory.length} adaptations
              </Typography>
            )}
          </Paper>
        ))}
      </Box>

      <Dialog open={addOpen} onClose={() => setAddOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Plot Thread</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
          <TextField label="Title" value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))} fullWidth />
          <TextField label="Description" multiline rows={3} value={form.description}
            onChange={e => setForm(f => ({ ...f, description: e.target.value }))} fullWidth />
          <FormControl fullWidth>
            <InputLabel>Category</InputLabel>
            <Select value={form.category} label="Category" onChange={e => setForm(f => ({ ...f, category: e.target.value as PlotThreadCategory }))}>
              {CATEGORIES.map(c => <MenuItem key={c} value={c}>{c}</MenuItem>)}
            </Select>
          </FormControl>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleAdd}>Add</Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
