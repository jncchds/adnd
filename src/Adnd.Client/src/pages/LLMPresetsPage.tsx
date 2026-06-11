import { useState, useEffect, useCallback } from 'react';
import { Box, Typography, Paper, Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField, MenuItem, Chip, Alert, IconButton, List, ListItemButton, ListItemText, ListItemAvatar, Avatar, ListItemSecondaryAction, Autocomplete, CircularProgress, InputAdornment } from '@mui/material';
import { useLLMPresets } from '../api/hooks/useLLM';
import { llmGetProviderModels } from '../api/llm/llmApi';
import { Add as AddIcon, Delete as DeleteIcon, Edit as EditIcon, Link as LinkIcon, WifiOff as WifiOffIcon, CheckCircle as CheckIcon } from '@mui/icons-material';

export default function LLMPresetsPage() {
  const { presets, isLoading, createPreset, updatePreset, deletePreset, setDefault, testConnection } = useLLMPresets();

  const [openDialog, setOpenDialog] = useState(false);
  const [editingPreset, setEditingPreset] = useState<any>(null);
  const [presetName, setPresetName] = useState('');
  const [presetProvider, setPresetProvider] = useState('ollama');
  const [presetEndpoint, setPresetEndpoint] = useState('');
  const [presetApiKey, setPresetApiKey] = useState('');
  const [showApiKey, setShowApiKey] = useState(false);
  const [presetBaseModel, setPresetBaseModel] = useState('');
  const [presetEmbeddingModel, setPresetEmbeddingModel] = useState('');
  const [models, setModels] = useState<string[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [modelsError, setModelsError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [_actionError, setActionError] = useState<string | null>(null);
  const [testingId, setTestingId] = useState<string | null>(null);

  const fetchModels = useCallback(async () => {
    setModelsLoading(true);
    setModelsError(null);
    setModels([]);
    try {
      const data = await llmGetProviderModels(presetProvider, presetEndpoint || undefined, presetApiKey || undefined);
      setModels(data);
    } catch (e: any) {
      setModelsError(e.message || 'Failed to load models');
      setModels([]);
    } finally {
      setModelsLoading(false);
    }
  }, [presetProvider, presetEndpoint, presetApiKey]);

  useEffect(() => {
    if (openDialog && !editingPreset) {
      setPresetName(''); setPresetProvider('ollama'); setPresetBaseModel('');
      setPresetEmbeddingModel(''); setPresetEndpoint(''); setPresetApiKey('');
      setSuccessMsg(null); setActionError(null);
    }
  }, [openDialog, editingPreset]);

  const defaultProviders = [
    { value: 'ollama', label: 'Ollama', icon: '🦙' },
    { value: 'lmstudio', label: 'LM Studio', icon: '🏠' },
    { value: 'openai', label: 'OpenAI', icon: '🔵' },
    { value: 'google', label: 'Google AI', icon: '🟢' },
  ];

  const handleOpenCreate = () => { setEditingPreset(null); setOpenDialog(true); };
  const handleOpenEdit = (preset: any) => {
    setEditingPreset(preset);
    setPresetName(preset.name);
    setPresetProvider(preset.providerType);
    setPresetBaseModel(preset.baseModel);
    setPresetEmbeddingModel(preset.embeddingModel || '');
    setPresetEndpoint(preset.endpointUrl || '');
    setPresetApiKey(preset.apiKey || '');
    setOpenDialog(true);
  };

  const handleSave = async () => {
    setActionError(null);
    try {
      const body: any = { name: presetName, providerType: presetProvider, baseModel: presetBaseModel, temperature: 0.7, maxTokens: 4096, topP: 0.9 };
      if (presetEndpoint) body.endpointUrl = presetEndpoint;
      if (presetApiKey) body.apiKey = presetApiKey;
      if (presetEmbeddingModel) body.embeddingModel = presetEmbeddingModel;
      if (editingPreset) {
        await updatePreset(editingPreset.id, body);
        setSuccessMsg('Preset updated!');
      } else {
        await createPreset(body);
        setSuccessMsg('Preset created!');
      }
      setOpenDialog(false);
      setSuccessMsg(null);
    } catch (e: any) {
      setActionError(e.message);
    }
  };

  const handleTest = async (presetId: string) => {
    setTestingId(presetId);
    try {
      await testConnection(presetId);
      setSuccessMsg('Connection successful!');
    } catch (e: any) {
      setActionError(e.message);
    } finally {
      setTestingId(null);
    }
  };

  const handleSetDefault = async (presetId: string) => {
    try { await setDefault(presetId); setSuccessMsg('Default preset updated!'); }
    catch (e: any) { setActionError(e.message); }
  };

  const handleDelete = async (presetId: string) => {
    try { await deletePreset(presetId); setSuccessMsg('Preset deleted!'); }
    catch (e: any) { setActionError(e.message); }
  };

  if (isLoading) return <Box sx={{ textAlign: 'center', mt: 8 }}><Typography>Loading presets...</Typography></Box>;

  return (
    <Box>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>LLM Presets</Typography>
      {successMsg && <Alert severity="success" onClose={() => setSuccessMsg(null)} sx={{ mb: 2, alignItems: 'center' }}><CheckIcon fontSize="small" sx={{ mr: 1 }} />{successMsg}</Alert>}

      <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenCreate} sx={{ mb: 2 }}>Add Preset</Button>

      {presets.length === 0 ? (
        <Paper sx={{ p: 6, textAlign: 'center', bgcolor: 'background.paper' }}>
          <Typography variant="h6" color="text.secondary" sx={{ mb: 2 }}>No LLM presets yet</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>Add your first preset to configure an LLM provider for your games.</Typography>
          <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenCreate}>Add Preset</Button>
        </Paper>
      ) : (
        <List>
          {presets.map((preset: any) => (
            <ListItemButton key={preset.id} onClick={() => handleOpenEdit(preset)}>
              <ListItemAvatar>
                <Avatar>{defaultProviders.find(p => p.value === preset.providerType)?.icon || '🤖'}</Avatar>
              </ListItemAvatar>
              <ListItemText primary={preset.name} secondary={`${preset.providerType} · ${preset.baseModel}${preset.embeddingModel ? ' · embed: ' + preset.embeddingModel : ''}`} />
              <ListItemSecondaryAction>
                <Chip label={preset.isDefault ? 'Default' : ''} size="small" color={preset.isDefault ? 'primary' : 'default'} sx={{ mr: 1 }} />
                <IconButton size="small" onClick={e => { e.stopPropagation(); handleTest(preset.id); }} disabled={testingId === preset.id} color="inherit">
                  {testingId === preset.id ? <CircularProgress size={16} /> : <WifiOffIcon fontSize="small" />}
                </IconButton>
                <IconButton size="small" onClick={() => handleOpenEdit(preset)} color="primary"><EditIcon fontSize="small" /></IconButton>
                <IconButton size="small" onClick={() => handleSetDefault(preset.id)} color="warning"><LinkIcon fontSize="small" /></IconButton>
                <IconButton size="small" onClick={() => handleDelete(preset.id)} color="error"><DeleteIcon fontSize="small" /></IconButton>
              </ListItemSecondaryAction>
            </ListItemButton>
          ))}
        </List>
      )}

      <Dialog open={openDialog} onClose={() => setOpenDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editingPreset ? 'Edit LLM Preset' : 'Create LLM Preset'}</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField fullWidth label="Name" value={presetName} onChange={e => setPresetName(e.target.value)} placeholder="My Awesome LLM" autoFocus />
          <TextField fullWidth select label="Provider" value={presetProvider} onChange={e => setPresetProvider(e.target.value)}>
            {defaultProviders.map(p => <MenuItem key={p.value} value={p.value}>{p.icon} {p.label}</MenuItem>)}
          </TextField>
          <TextField fullWidth label="Endpoint" value={presetEndpoint} onChange={e => setPresetEndpoint(e.target.value)} placeholder={presetProvider === 'ollama' ? 'http://localhost:11434' : presetProvider === 'openai' ? 'https://api.openai.com/v1' : 'https://api.example.com/v1'} />
          <TextField fullWidth label="API Key" type={showApiKey ? 'text' : 'password'} value={presetApiKey} onChange={e => setPresetApiKey(e.target.value)} placeholder="sk-..." InputProps={{ endAdornment: <InputAdornment position="end"><IconButton onClick={() => setShowApiKey(!showApiKey)} edge="end">{showApiKey ? '👁️' : '🙈'}</IconButton></InputAdornment> }} />
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
            <Button variant="outlined" size="small" onClick={fetchModels} disabled={modelsLoading} startIcon={modelsLoading ? <CircularProgress size={14} /> : undefined}>
              {modelsLoading ? 'Loading...' : 'Load Models'}
            </Button>
            {modelsError && <Typography variant="body2" color="error">{modelsError}</Typography>}
            {models.length > 0 && <Chip label={`${models.length} models loaded`} size="small" color="success" variant="outlined" />}          </Box>
          <Autocomplete options={models} value={presetBaseModel} onChange={(_, v) => setPresetBaseModel(v || '')} renderInput={p => <TextField {...p} fullWidth label="Base Model" placeholder={models.length > 0 ? 'Select or type a model' : 'Type a model name or click "Load Models"'} />} />
          <Autocomplete options={models} value={presetEmbeddingModel} onChange={(_, v) => setPresetEmbeddingModel(v || '')} renderInput={p => <TextField {...p} fullWidth label="Embedding Model" placeholder={models.length > 0 ? 'Select or type a model' : 'Type a model name or click "Load Models"'} />} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenDialog(false)}>Cancel</Button>
          <Button onClick={handleSave} variant="contained">Save</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
