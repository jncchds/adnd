import { useState, useEffect } from 'react';
import { Box, Typography, Paper, Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField, MenuItem, Chip, Alert, IconButton, List, ListItemButton, ListItemText, ListItemAvatar, Avatar, ListItemSecondaryAction, Autocomplete, CircularProgress, InputAdornment } from '@mui/material';
import { useLLMPresets, useProviderModels } from '../api/hooks/useLLM';
import { Add as AddIcon, Delete as DeleteIcon, Edit as EditIcon, Link as LinkIcon, WifiOff as WifiOffIcon, CheckCircle as CheckIcon } from '@mui/icons-material';

export default function LLMPresetsPage() {
  const { presets, isLoading, createPreset, updatePreset, deletePreset, setDefault, testConnection } = useLLMPresets();
  const { models, isLoading: modelsLoading } = useProviderModels();

  const [openDialog, setOpenDialog] = useState(false);
  const [editingPreset, setEditingPreset] = useState<any>(null);
  const [presetName, setPresetName] = useState('');
  const [presetProvider, setPresetProvider] = useState('ollama');
  const [presetModel, setPresetModel] = useState('');
  const [presetEndpoint, setPresetEndpoint] = useState('');
  const [presetApiKey, setPresetApiKey] = useState('');
  const [showApiKey, setShowApiKey] = useState(false);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [_actionError, setActionError] = useState<string | null>(null);
  const [testingId, setTestingId] = useState<string | null>(null);

  useEffect(() => {
    if (openDialog && !editingPreset) {
      setPresetName(''); setPresetProvider('ollama'); setPresetModel('');
      setPresetEndpoint(''); setPresetApiKey(''); setSuccessMsg(null); setActionError(null);
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
    setPresetModel(preset.baseModel);
    setPresetEndpoint(preset.endpoint || '');
    setPresetApiKey(preset.apiKey || '');
    setOpenDialog(true);
  };

  const handleSave = async () => {
    setActionError(null);
    try {
      if (editingPreset) {
        await updatePreset(editingPreset.id, { name: presetName, temperature: 0.7, maxTokens: 4096, topP: 0.9 });
        setSuccessMsg('Preset updated!');
      } else {
        await createPreset({ name: presetName, providerType: presetProvider, baseModel: presetModel, temperature: 0.7, maxTokens: 4096, topP: 0.9 });
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
          {presets.map(preset => (
            <ListItemButton key={preset.id} onClick={() => handleOpenEdit(preset)}>
              <ListItemAvatar>
                <Avatar>{defaultProviders.find(p => p.value === preset.providerType)?.icon || '🤖'}</Avatar>
              </ListItemAvatar>
              <ListItemText primary={preset.name} secondary={`${preset.providerType} · ${preset.baseModel}`} />
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
          <Autocomplete options={models || []} value={presetModel} onChange={(_, v) => setPresetModel(v || '')} renderInput={p => <TextField {...p} fullWidth label="Model" placeholder={modelsLoading ? 'Loading...' : 'Select or type a model'} />} />
          <TextField fullWidth label="Endpoint" value={presetEndpoint} onChange={e => setPresetEndpoint(e.target.value)} placeholder="https://api.openai.com/v1" />
          <TextField fullWidth label="API Key" type={showApiKey ? 'text' : 'password'} value={presetApiKey} onChange={e => setPresetApiKey(e.target.value)} InputProps={{ endAdornment: <InputAdornment position="end"><IconButton onClick={() => setShowApiKey(!showApiKey)} edge="end">{showApiKey ? '👁️' : '🙈'}</IconButton></InputAdornment> }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenDialog(false)}>Cancel</Button>
          <Button onClick={handleSave} variant="contained">Save</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
