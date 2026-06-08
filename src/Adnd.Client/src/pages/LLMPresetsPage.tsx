import { useState, useCallback } from 'react';
import { useLLMPresets, useProviderModels } from '../api/gameHooks';
import {
  Box,
  Typography,
  Paper,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  IconButton,
  Chip,
  Alert,
  AlertTitle,
  InputAdornment,
  Collapse,
  List,
  ListItemText,
  ListItemAvatar,
  ListItemSecondaryAction,
  Avatar,
  MenuItem,
  ListItemButton,
  ListItemIcon,
  Autocomplete,
  CircularProgress,
} from '@mui/material';
import {
  Add as AddIcon,
  Delete as DeleteIcon,
  Edit as EditIcon,
  CheckCircle as CheckIcon,
  Link as LinkIcon,
  Wifi as WifiIcon,
  WifiOff as WifiOffIcon,
  Visibility as EyeIcon,
  VisibilityOff as EyeOffIcon,
  Refresh as RefreshIcon,
} from '@mui/icons-material';

export default function LLMPresetsPage() {
  const {
    presets,
    isLoading,
    error,
    createPreset,
    updatePreset,
    deletePreset,
    testConnection,
    setDefault,
  } = useLLMPresets();

  // Model listing hook
  const { models: providerModels, isLoading: loadingModels, error: modelError, fetchModels } = useProviderModels();

  const [openDialog, setOpenDialog] = useState(false);
  const [editingPreset, setEditingPreset] = useState<any>(null);
  const [presetName, setPresetName] = useState('');
  const [presetProvider, setPresetProvider] = useState('ollama');
  const [presetModel, setPresetModel] = useState('');
  const [presetEndpoint, setPresetEndpoint] = useState('');
  const [presetApiKey, setPresetApiKey] = useState('');
  const [presetTemp, setPresetTemp] = useState(0.7);
  const [presetMaxTokens, setPresetMaxTokens] = useState(2048);
  const [presetTopP, setPresetTopP] = useState(0.9);
  const [presetEmbedModel, setPresetEmbedModel] = useState('');
  const [showApiKey, setShowApiKey] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [testingId, setTestingId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const defaultProviders = [
    { value: 'ollama', label: 'Ollama', placeholder: 'http://localhost:11434', icon: '🦙' },
    { value: 'lmstudio', label: 'LM Studio', placeholder: 'http://localhost:1234', icon: '🏠' },
    { value: 'openai', label: 'OpenAI', placeholder: 'https://api.openai.com/v1', icon: '🔵' },
    { value: 'google', label: 'Google AI Studio', placeholder: 'https://generativelanguage.googleapis.com/v1beta', icon: '🟢' },
  ];

  const getProviderInfo = (provider: string) =>
    defaultProviders.find(p => p.value === provider) || defaultProviders[0];

  const supportsModelListing = (provider: string) =>
    ['ollama', 'lmstudio', 'openai', 'google'].includes(provider.toLowerCase());

  const getProviderEndpoint = (provider: string) => {
    const info = getProviderInfo(provider);
    return presetEndpoint || info.placeholder;
  };

  const handleLoadModels = useCallback(async () => {
    const endpoint = getProviderEndpoint(presetProvider);
    await fetchModels(presetProvider, endpoint, presetApiKey || undefined);
  }, [presetProvider, presetEndpoint, presetApiKey, fetchModels]);



  const handleOpenCreate = () => {
    setEditingPreset(null);
    setPresetName('');
    setPresetProvider('ollama');
    setPresetModel('');
    setPresetEndpoint('');
    setPresetApiKey('');
    setPresetTemp(0.7);
    setPresetMaxTokens(2048);
    setPresetTopP(0.9);
    setPresetEmbedModel('');
    setShowApiKey(false);
    setTestResult(null);
    setOpenDialog(true);
  };

  const handleOpenEdit = (preset: any) => {
    setEditingPreset(preset);
    setPresetName(preset.name);
    setPresetProvider(preset.providerType);
    setPresetModel(preset.baseModel);
    setPresetEndpoint(preset.endpointUrl || '');
    setPresetApiKey('');
    setPresetTemp(preset.temperature);
    setPresetMaxTokens(preset.maxTokens);
    setPresetTopP(preset.topP);
    setPresetEmbedModel(preset.embeddingModel || '');
    setShowApiKey(false);
    setTestResult(null);
    setOpenDialog(true);
  };

  const handleSave = async () => {
    if (!presetName.trim() || !presetModel.trim()) return;
    setActionError(null);
    setSuccessMsg(null);
    try {
      const request = {
        name: presetName,
        providerType: presetProvider,
        baseModel: presetModel,
        endpointUrl: presetEndpoint || undefined,
        apiKey: presetApiKey || undefined,
        temperature: presetTemp,
        maxTokens: presetMaxTokens,
        topP: presetTopP,
        embeddingModel: presetEmbedModel || undefined,
        isDefault: false,
        isActive: true,
      };
      if (editingPreset) {
        await updatePreset(editingPreset.id, request);
        setSuccessMsg('Preset updated!');
      } else {
        await createPreset(request);
        setSuccessMsg('Preset created!');
      }
      setOpenDialog(false);
      setTimeout(() => setSuccessMsg(null), 2000);
    } catch (e: any) {
      setActionError(e.message);
    }
  };

  const handleTest = async (presetId: string) => {
    setTestingId(presetId);
    try {
      const result: any = await testConnection(presetId);
      setTestResult({ success: result.success, message: result.message || '' });
    } catch (e: any) {
      setTestResult({ success: false, message: e.message });
    } finally {
      setTestingId(null);
    }
  };

  const handleSetDefault = async (presetId: string) => {
    setActionError(null);
    try {
      await setDefault(presetId);
      setSuccessMsg('Default preset updated!');
      setTimeout(() => setSuccessMsg(null), 2000);
    } catch (e: any) {
      setActionError(e.message);
    }
  };

  const handleDelete = async (presetId: string) => {
    if (window.confirm('Delete this preset? This cannot be undone.')) {
      setActionError(null);
      try {
        await deletePreset(presetId);
      } catch (e: any) {
        setActionError(e.message);
      }
    }
  };

  if (isLoading) {
    return (
      <Box sx={{ textAlign: 'center', mt: 8 }}>
        <Typography>Loading presets...</Typography>
      </Box>
    );
  }

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Box>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            LLM Presets
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Manage your LLM provider configurations. Use these presets when creating games.
          </Typography>
        </Box>
        <ListItemButton
          onClick={handleOpenCreate}
          sx={{
            borderRadius: 1,
            justifyContent: 'flex-start',
            pl: 2,
            bgcolor: 'rgba(145,71,255,0.1)',
            '&:hover': { bgcolor: 'rgba(145,71,255,0.15)' },
          }}
        >
          <ListItemIcon sx={{ minWidth: 0, mr: 2, justifyContent: 'center' }}>
            <AddIcon fontSize="small" color="primary" />
          </ListItemIcon>
          <ListItemText primary="Add Preset" sx={{ color: 'primary.light' }} />
        </ListItemButton>
      </Box>

      {/* Messages */}
      {successMsg && (
        <Alert severity="success" onClose={() => setSuccessMsg(null)} sx={{ mb: 2, alignItems: 'center' }}>
          <CheckIcon fontSize="small" sx={{ mr: 1 }} />
          {successMsg}
        </Alert>
      )}
      {actionError && (
        <Alert severity="error" onClose={() => setActionError(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {actionError}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {error}
        </Alert>
      )}

      {/* Presets List */}
      {presets.length === 0 ? (
        <Paper sx={{ p: 6, textAlign: 'center', bgcolor: 'background.paper' }}>
          <Typography variant="h6" color="text.secondary" sx={{ mb: 2 }}>
            No LLM presets yet
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Add your first preset to configure an LLM provider for your games.
          </Typography>
          <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenCreate}>
            Add Preset
          </Button>
        </Paper>
      ) : (
        <Box>
          <Typography variant="caption" sx={{ px: 2, color: 'text.secondary', display: 'block', mb: 0.5 }}>
            YOUR PRESETS
          </Typography>
          <List>
            {presets.map(preset => {
              const providerInfo = getProviderInfo(preset.providerType);
              return (
                <ListItemButton
                  key={preset.id}
                  sx={{
                    mb: 0.5,
                    borderRadius: 1,
                    bgcolor: preset.isDefault ? 'rgba(145,71,255,0.08)' : 'transparent',
                    border: preset.isDefault ? '2px solid' : '1px solid',
                    borderColor: preset.isDefault ? 'primary.main' : 'divider',
                    '&:hover': { bgcolor: 'rgba(145,71,255,0.06)' },
                  }}
                >
                  <ListItemAvatar>
                    <Avatar sx={{ bgcolor: 'background.default' }}>
                      <Typography>{providerInfo.icon}</Typography>
                    </Avatar>
                  </ListItemAvatar>
                  <ListItemText
                    primary={
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <Typography variant="body1" sx={{ fontWeight: 600 }}>
                          {preset.name}
                        </Typography>
                        {preset.isDefault && (
                          <Chip label="Default" size="small" color="primary" sx={{ height: 18, fontSize: 10 }} />
                        )}
                      </Box>
                    }
                    secondary={
                      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mt: 0.5 }}>
                        <Typography variant="caption" color="text.secondary">
                          {providerInfo.label} · {preset.baseModel}
                        </Typography>
                        <Chip
                          label={`Temp: ${preset.temperature}`}
                          size="small"
                          variant="outlined"
                          sx={{ height: 16, fontSize: 10 }}
                        />
                        <Chip
                          label={`Max: ${preset.maxTokens}`}
                          size="small"
                          variant="outlined"
                          sx={{ height: 16, fontSize: 10 }}
                        />
                        <Chip
                          label={`TopP: ${preset.topP}`}
                          size="small"
                          variant="outlined"
                          sx={{ height: 16, fontSize: 10 }}
                        />
                        {preset.hasApiKey && (
                          <Chip
                            label="🔑 API Key"
                            size="small"
                            color="success"
                            variant="outlined"
                            sx={{ height: 16, fontSize: 10 }}
                          />
                        )}
                        {(preset as any).embeddingModel && (
                          <Chip
                            label={`Embed: ${(preset as any).embeddingModel}`}
                            size="small"
                            color="info"
                            variant="outlined"
                            sx={{ height: 16, fontSize: 10 }}
                          />
                        )}
                        {preset.isActive ? (
                          <Chip
                            label="Active"
                            size="small"
                            color="success"
                            sx={{ height: 16, fontSize: 10 }}
                          />
                        ) : (
                          <Chip
                            label="Inactive"
                            size="small"
                            color="default"
                            sx={{ height: 16, fontSize: 10 }}
                          />
                        )}
                      </Box>
                    }
                  />
                  <ListItemSecondaryAction>
                    <Box sx={{ display: 'flex', gap: 0.5 }}>
                      <IconButton
                        size="small"
                        onClick={() => handleTest(preset.id)}
                        disabled={testingId === preset.id}
                        color="inherit"
                      >
                        {testingId === preset.id ? (
                          <WifiIcon fontSize="small" />
                        ) : (
                          <WifiOffIcon fontSize="small" />
                        )}
                      </IconButton>
                      <IconButton size="small" onClick={() => handleOpenEdit(preset)} color="primary">
                        <EditIcon fontSize="small" />
                      </IconButton>
                      <IconButton size="small" onClick={() => handleSetDefault(preset.id)} color="warning">
                        <LinkIcon fontSize="small" />
                      </IconButton>
                      <IconButton size="small" onClick={() => handleDelete(preset.id)} color="error">
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Box>
                  </ListItemSecondaryAction>
                </ListItemButton>
              );
            })}
          </List>
        </Box>
      )}

      {/* Create/Edit Dialog */}
      <Dialog open={openDialog} onClose={() => setOpenDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>
          {editingPreset ? 'Edit LLM Preset' : 'Create LLM Preset'}
        </DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            fullWidth
            label="Name"
            value={presetName}
            onChange={e => setPresetName(e.target.value)}
            placeholder="My Awesome LLM"
            autoFocus
          />
          <TextField
            fullWidth
            select
            label="Provider"
            value={presetProvider}
            onChange={e => setPresetProvider(e.target.value)}
          >
            {defaultProviders.map(p => (
              <MenuItem key={p.value} value={p.value}>
                {p.icon} {p.label}
              </MenuItem>
            ))}
          </TextField>

          {/* Base Model with model listing support */}
          {supportsModelListing(presetProvider) ? (
            <Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>
                  Base Model
                </Typography>
                <Button
                  size="small"
                  startIcon={loadingModels ? <CircularProgress size={14} /> : <RefreshIcon />}
                  onClick={handleLoadModels}
                  disabled={loadingModels}
                  sx={{ textTransform: 'none', minWidth: 0, px: 1 }}
                >
                  Refresh
                </Button>
              </Box>
              <Autocomplete
                freeSolo
                options={providerModels}
                value={presetModel || null}
                onChange={(_event, newValue) => setPresetModel(newValue || '')}
                renderInput={params => (
                  <TextField
                    {...params}
                    placeholder="e.g., llama3, gpt-4o-mini, gemini-pro"
                    InputProps={{
                      ...params.InputProps,
                      endAdornment: params.InputProps?.endAdornment,
                    }}
                  />
                )}
                isOptionEqualToValue={(option, value) => option === value}
                ListboxComponent={props => (
                  <Box {...props} sx={{ maxHeight: 200, overflow: 'auto' }}>
                    {props.children}
                  </Box>
                )}
              />
              {loadingModels && (
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                  Loading available models from provider...
                </Typography>
              )}
              {modelError && (
                <Typography variant="caption" color="error" sx={{ display: 'block', mt: 0.5 }}>
                  {modelError}
                </Typography>
              )}
              {!loadingModels && providerModels.length > 0 && (
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                  {providerModels.length} models available — type to filter
                </Typography>
              )}
            </Box>
          ) : (
            <TextField
              fullWidth
              label="Base Model"
              value={presetModel}
              onChange={e => setPresetModel(e.target.value)}
              placeholder="e.g., llama3, gpt-4o-mini, gemini-pro"
            />
          )}

          <TextField
            fullWidth
            label="Endpoint URL"
            value={presetEndpoint}
            onChange={e => setPresetEndpoint(e.target.value)}
            placeholder={getProviderInfo(presetProvider).placeholder}
          />
          <TextField
            fullWidth
            label="API Key"
            type={showApiKey ? 'text' : 'password'}
            value={presetApiKey}
            onChange={e => setPresetApiKey(e.target.value)}
            placeholder="Leave blank to keep existing key"
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton size="small" onClick={() => setShowApiKey(!showApiKey)}>
                    {showApiKey ? <EyeOffIcon fontSize="small" /> : <EyeIcon fontSize="small" />}
                  </IconButton>
                </InputAdornment>
              ),
            }}
          />
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField
              fullWidth
              label="Temperature"
              type="number"
              value={presetTemp}
              onChange={e => setPresetTemp(parseFloat(e.target.value) || 0)}
              inputProps={{ step: 0.1, min: 0, max: 2 }}
              size="small"
            />
            <TextField
              fullWidth
              label="Max Tokens"
              type="number"
              value={presetMaxTokens}
              onChange={e => setPresetMaxTokens(parseInt(e.target.value) || 2048)}
              inputProps={{ min: 1 }}
              size="small"
            />
            <TextField
              fullWidth
              label="Top P"
              type="number"
              value={presetTopP}
              onChange={e => setPresetTopP(parseFloat(e.target.value) || 0.9)}
              inputProps={{ step: 0.1, min: 0, max: 1 }}
              size="small"
            />
          </Box>

          {/* Embedding Model with model listing support */}
          {supportsModelListing(presetProvider) ? (
            <Box>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                Embedding Model (optional)
              </Typography>
              <Autocomplete
                freeSolo
                options={providerModels}
                value={presetEmbedModel || null}
                onChange={(_event, newValue) => setPresetEmbedModel(newValue || '')}
                renderInput={params => (
                  <TextField
                    {...params}
                    placeholder="e.g., nomic-embed-text, text-embedding-3-small"
                    InputProps={{
                      ...params.InputProps,
                      endAdornment: params.InputProps?.endAdornment,
                    }}
                  />
                )}
                isOptionEqualToValue={(option, value) => option === value}
                ListboxComponent={props => (
                  <Box {...props} sx={{ maxHeight: 200, overflow: 'auto' }}>
                    {props.children}
                  </Box>
                )}
              />
              {!loadingModels && providerModels.length > 0 && (
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                  {providerModels.length} models available — type to filter
                </Typography>
              )}
            </Box>
          ) : (
            <TextField
              fullWidth
              label="Embedding Model (optional)"
              value={presetEmbedModel}
              onChange={e => setPresetEmbedModel(e.target.value)}
              placeholder="e.g., nomic-embed-text, text-embedding-3-small"
            />
          )}

          {testResult && (
            <Collapse in={!!testResult}>
              <Alert
                severity={testResult.success ? 'success' : 'error'}
                sx={{ alignItems: 'center' }}
              >
                {testResult.success ? <CheckIcon fontSize="small" sx={{ mr: 1 }} /> : null}
                {testResult.message}
              </Alert>
            </Collapse>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenDialog(false)}>Cancel</Button>
          {editingPreset && (
            <Button
              variant="outlined"
              onClick={() => handleTest(editingPreset.id)}
              disabled={testingId === editingPreset.id}
            >
              {testingId === editingPreset.id ? 'Testing...' : 'Test Connection'}
            </Button>
          )}
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={!presetName.trim() || !presetModel.trim()}
          >
            {editingPreset ? 'Save' : 'Create'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
