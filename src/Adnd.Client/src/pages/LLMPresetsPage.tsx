import { useEffect, useState } from 'react'
import {
  Box, Typography, Paper, Button, List, ListItem, ListItemText, ListItemSecondaryAction,
  IconButton, Chip, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Select, MenuItem, FormControl, InputLabel, Switch, FormControlLabel,
  CircularProgress, Alert, Tooltip, Divider, Slider,
} from '@mui/material'
import {
  Add as AddIcon, Edit as EditIcon, Delete as DeleteIcon, Star as StarIcon,
  StarBorder as StarBorderIcon, PlayArrow as TestIcon, Sync as LoadModelsIcon,
} from '@mui/icons-material'
import { api } from '../api/client'
import type { LLMPreset, LLMPresetCreate } from '../types'

const PROVIDERS = ['ollama', 'openaicompatible', 'openai', 'google'] as const
const REASONING = ['none', 'low', 'medium', 'high'] as const

const emptyForm = (): LLMPresetCreate => ({
  name: '', providerType: 'ollama', baseModel: '',
  endpointUrl: '', apiKey: '', temperature: 0.7, maxTokens: 2048,
  topP: 0.9, frequencyPenalty: 0, presencePenalty: 0, stream: false,
  timeoutMs: undefined, reasoningEffort: 'none',
  embeddingModel: '', embeddingEndpointUrl: '', isDefault: false,
})

export default function LLMPresetsPage() {
  const [presets, setPresets] = useState<LLMPreset[]>([])
  const [loading, setLoading] = useState(false)
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<LLMPreset | null>(null)
  const [form, setForm] = useState<LLMPresetCreate>(emptyForm())
  const [error, setError] = useState<string | null>(null)
  const [testResult, setTestResult] = useState<string | null>(null)
  const [testing, setTesting] = useState(false)
  const [availableModels, setAvailableModels] = useState<string[]>([])
  const [loadingModels, setLoadingModels] = useState(false)

  const load = async () => {
    setLoading(true)
    try { setPresets(await api.llmPresets.list()) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [])

  const openCreate = () => { setEditing(null); setForm(emptyForm()); setTestResult(null); setAvailableModels([]); setOpen(true) }
  const openEdit = (p: LLMPreset) => {
    setEditing(p)
    setForm({
      name: p.name, providerType: p.providerType, baseModel: p.baseModel,
      endpointUrl: p.endpointUrl ?? '', apiKey: '', temperature: p.temperature,
      maxTokens: p.maxTokens, topP: p.topP, frequencyPenalty: p.frequencyPenalty,
      presencePenalty: p.presencePenalty, stream: p.stream, timeoutMs: p.timeoutMs ?? undefined,
      reasoningEffort: p.reasoningEffort, embeddingModel: p.embeddingModel ?? '',
      embeddingEndpointUrl: p.embeddingEndpointUrl ?? '', isDefault: p.isDefault,
    })
    setTestResult(null)
    setAvailableModels([])
    setOpen(true)
  }

  const handleLoadModels = async () => {
    setLoadingModels(true)
    try {
      const models = editing
        ? await api.llmPresets.listModels(editing.id)
        : await api.llmPresets.queryModels(form.providerType, form.endpointUrl, form.apiKey)
      setAvailableModels(models)
    } catch { /* silently ignore — endpoint or provider may not support listing */ }
    finally { setLoadingModels(false) }
  }

  const handleSave = async () => {
    if (!form.name.trim() || !form.baseModel.trim()) { setError('Name and model are required'); return }
    setError(null)
    try {
      if (editing) await api.llmPresets.update(editing.id, { ...form, id: editing.id })
      else await api.llmPresets.create(form)
      setOpen(false)
      load()
    } catch (e) { setError((e as Error).message) }
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Delete this preset?')) return
    try { await api.llmPresets.delete(id); load() }
    catch (e) { setError((e as Error).message) }
  }

  const handleSetDefault = async (id: string) => {
    try { await api.llmPresets.setDefault(id); load() }
    catch (e) { setError((e as Error).message) }
  }

  const handleTest = async () => {
    if (!editing) return
    setTesting(true)
    setTestResult(null)
    try {
      const status = await api.llmPresets.test(editing.id)
      setTestResult(status.isAvailable ? '✓ Connection successful' : `✗ ${status.message}`)
    } catch (e) { setTestResult(`✗ ${(e as Error).message}`) }
    finally { setTesting(false) }
  }

  const f = (key: keyof LLMPresetCreate, val: unknown) => setForm(prev => ({ ...prev, [key]: val }))
  const needsEndpoint = form.providerType === 'ollama' || form.providerType === 'openaicompatible'
  const needsKey = form.providerType === 'openai' || form.providerType === 'google' || form.providerType === 'openaicompatible'

  return (
    <Box sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5" fontWeight={700}>LLM Presets</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>New Preset</Button>
      </Box>

      {error && !open && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {loading && <CircularProgress />}

      <Paper>
        <List>
          {presets.map((p, i) => (
            <Box key={p.id}>
              {i > 0 && <Divider />}
              <ListItem>
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      {p.name}
                      {p.isDefault && <Chip label="Default" size="small" color="primary" />}
                      <Chip label={p.providerType} size="small" variant="outlined" />
                    </Box>
                  }
                  secondary={`${p.baseModel} · T=${p.temperature} · ${p.maxTokens} tokens`}
                />
                <ListItemSecondaryAction>
                  <Tooltip title="Set as default"><IconButton onClick={() => handleSetDefault(p.id)}>
                    {p.isDefault ? <StarIcon color="primary" /> : <StarBorderIcon />}
                  </IconButton></Tooltip>
                  <IconButton onClick={() => openEdit(p)}><EditIcon /></IconButton>
                  <IconButton onClick={() => handleDelete(p.id)}><DeleteIcon /></IconButton>
                </ListItemSecondaryAction>
              </ListItem>
            </Box>
          ))}
          {!loading && presets.length === 0 && (
            <ListItem><ListItemText secondary="No presets yet. Create one to get started." /></ListItem>
          )}
        </List>
      </Paper>

      <Dialog open={open} onClose={() => setOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>{editing ? 'Edit Preset' : 'New LLM Preset'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
          {error && <Alert severity="error">{error}</Alert>}
          {testResult && <Alert severity={testResult.startsWith('✓') ? 'success' : 'error'}>{testResult}</Alert>}

          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField label="Name" value={form.name} onChange={e => f('name', e.target.value)} fullWidth />
            <FormControl sx={{ minWidth: 160 }}>
              <InputLabel>Provider</InputLabel>
              <Select value={form.providerType} label="Provider" onChange={e => f('providerType', e.target.value)}>
                {PROVIDERS.map(p => <MenuItem key={p} value={p}>{p}</MenuItem>)}
              </Select>
            </FormControl>
          </Box>

          {needsEndpoint && (
            <TextField label="Endpoint URL" value={form.endpointUrl} onChange={e => f('endpointUrl', e.target.value)} fullWidth
              helperText="e.g. http://localhost:11434 (Ollama) or http://localhost:1234/v1 (LM Studio)" />
          )}
          {needsKey && (
            <TextField label="API Key" value={form.apiKey} onChange={e => f('apiKey', e.target.value)} fullWidth
              type="password" helperText="Leave empty to keep existing key when editing" />
          )}

          <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
            <TextField
              label="Base Model"
              value={form.baseModel}
              onChange={e => f('baseModel', e.target.value)}
              fullWidth
              helperText="e.g. llama3.2, gpt-4o, gemini-2.0-flash"
            />
            <Tooltip title="Load available models from the configured endpoint">
              <span>
                <IconButton onClick={handleLoadModels} disabled={loadingModels} sx={{ mt: 1 }}>
                  {loadingModels ? <CircularProgress size={20} /> : <LoadModelsIcon />}
                </IconButton>
              </span>
            </Tooltip>
          </Box>
          {availableModels.length > 0 && (
            <FormControl fullWidth size="small">
              <InputLabel>Pick from available models</InputLabel>
              <Select
                label="Pick from available models"
                value={form.baseModel}
                onChange={e => f('baseModel', e.target.value)}
              >
                {availableModels.map(m => <MenuItem key={m} value={m}>{m}</MenuItem>)}
              </Select>
            </FormControl>
          )}

          <TextField label="Embedding Model (optional)" value={form.embeddingModel}
            onChange={e => f('embeddingModel', e.target.value)} fullWidth
            helperText="Uses base model endpoint. Leave empty to skip embeddings." />

          <Divider />

          <Box sx={{ display: 'flex', gap: 2 }}>
            <Box sx={{ flex: 1 }}>
              <Typography variant="caption" color="text.secondary">Temperature: {form.temperature}</Typography>
              <Slider value={form.temperature as number} onChange={(_, v) => f('temperature', v)} min={0} max={2} step={0.05} />
            </Box>
            <TextField label="Max Tokens" type="number" value={form.maxTokens}
              onChange={e => f('maxTokens', parseInt(e.target.value))} sx={{ width: 130 }} />
          </Box>
          <Box sx={{ display: 'flex', gap: 2 }}>
            <Box sx={{ flex: 1 }}>
              <Typography variant="caption" color="text.secondary">Top P: {form.topP}</Typography>
              <Slider value={form.topP as number} onChange={(_, v) => f('topP', v)} min={0} max={1} step={0.05} />
            </Box>
            <FormControl sx={{ minWidth: 140 }}>
              <InputLabel>Reasoning</InputLabel>
              <Select value={form.reasoningEffort} label="Reasoning" onChange={e => f('reasoningEffort', e.target.value)}>
                {REASONING.map(r => <MenuItem key={r} value={r}>{r}</MenuItem>)}
              </Select>
            </FormControl>
          </Box>
          <Box sx={{ display: 'flex', gap: 2 }}>
            <FormControlLabel
              control={<Switch checked={form.isDefault} onChange={e => f('isDefault', e.target.checked)} />}
              label="Set as default preset"
            />
            <FormControlLabel
              control={<Switch checked={form.stream} onChange={e => f('stream', e.target.checked)} />}
              label="Enable streaming"
            />
          </Box>
        </DialogContent>
        <DialogActions>
          {editing && (
            <Button onClick={handleTest} startIcon={<TestIcon />} disabled={testing} sx={{ mr: 'auto' }}>
              {testing ? <CircularProgress size={18} /> : 'Test Connection'}
            </Button>
          )}
          <Button onClick={() => setOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSave}>Save</Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
