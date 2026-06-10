import { useState, useEffect } from 'react';
import {
  Container,
  Paper,
  Typography,
  Box,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  MenuItem,
  Chip,
  IconButton,
  Alert,
  Autocomplete,
  FormControl,
  InputLabel,
  Select,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import client from '@/api/client';

interface LlmPreset {
  id: string;
  name: string;
  provider: string;
  baseUrlModel: string;
  embeddingModel: string;
  systemPrompt: string;
  temperature: number;
  maxTokens?: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

interface ModelList {
  chatModels: string[];
  embeddingModels: string[];
}

const PROVIDERS = [
  { value: 'openai', label: 'OpenAI API' },
  { value: 'ollama', label: 'Ollama' },
  { value: 'lmstudio', label: 'LMStudio' },
  { value: 'google', label: 'Google AI Studio' },
];

export default function LlmPresetsPage() {
  const [presets, setPresets] = useState<LlmPreset[]>([]);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<LlmPreset | null>(null);
  const [models, setModels] = useState<ModelList | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [form, setForm] = useState({
    name: '',
    provider: 'openai',
    baseUrlModel: '',
    embeddingModel: '',
    apiKey: '',
    systemPrompt: '',
    temperature: 0.7,
    maxTokens: undefined as number | undefined,
    hostUrl: '',
  });

  const fetchPresets = async () => {
    const res = await client.get('/llm-presets');
    setPresets(res.data);
  };

  useEffect(() => { fetchPresets(); }, []);

  const handleOpen = (preset?: LlmPreset) => {
    if (preset) {
      setEditing(preset);
      setForm({
        name: preset.name,
        provider: preset.provider,
        baseUrlModel: preset.baseUrlModel,
        embeddingModel: preset.embeddingModel,
        apiKey: '',
        systemPrompt: preset.systemPrompt,
        temperature: preset.temperature,
        maxTokens: preset.maxTokens,
        hostUrl: '',
      });
    } else {
      setEditing(null);
      setForm({
        name: '',
        provider: 'openai',
        baseUrlModel: '',
        embeddingModel: '',
        apiKey: '',
        systemPrompt: '',
        temperature: 0.7,
        maxTokens: undefined,
        hostUrl: '',
      });
    }
    setOpen(true);
  };

  const handleClose = () => {
    setOpen(false);
    setEditing(null);
    setModels(null);
  };

  const handleProviderChange = async (provider: string) => {
    setForm({ ...form, provider });
    try {
      const res = await client.get(`/llm-presets/providers/${provider}/models`);
      setModels(res.data);
    } catch {
      setModels(null);
    }
  };

  const handleSave = async () => {
    setError('');
    setLoading(true);
    try {
      if (editing) {
        await client.put(`/llm-presets/${editing.id}`, form);
      } else {
        await client.post('/llm-presets', form);
      }
      setSuccess('Saved');
      setTimeout(() => setSuccess(''), 3000);
      handleClose();
      fetchPresets();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Save failed');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    await client.delete(`/llm-presets/${id}`);
    fetchPresets();
  };

  const handleActivate = async (id: string) => {
    await client.post(`/llm-presets/${id}/activate`);
    fetchPresets();
  };

  return (
    <Container maxWidth="md">
      <Box sx={{ mt: 4 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
          <Typography variant="h4">LLM Presets</Typography>
          <Button variant="contained" onClick={() => handleOpen()}>
            New Preset
          </Button>
        </Box>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {success && <Alert severity="success" sx={{ mb: 2 }}>{success}</Alert>}

        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Provider</TableCell>
                <TableCell>Base Model</TableCell>
                <TableCell>Embedding</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Updated</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {presets.map((preset) => (
                <TableRow key={preset.id}>
                  <TableCell>{preset.name}</TableCell>
                  <TableCell>{PROVIDERS.find(p => p.value === preset.provider)?.label}</TableCell>
                  <TableCell>{preset.baseUrlModel}</TableCell>
                  <TableCell>{preset.embeddingModel}</TableCell>
                  <TableCell>
                    <Chip
                      label={preset.isActive ? 'Active' : 'Inactive'}
                      color={preset.isActive ? 'success' : 'default'}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>{new Date(preset.updatedAt).toLocaleDateString()}</TableCell>
                  <TableCell>
                    <IconButton onClick={() => handleOpen(preset)} size="small">
                      Edit
                    </IconButton>
                    {preset.isActive ? (
                      <IconButton onClick={() => handleDelete(preset.id)} color="error" size="small">
                        <DeleteIcon />
                      </IconButton>
                    ) : (
                      <Button size="small" onClick={() => handleActivate(preset.id)}>
                        Activate
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        {/* Create/Edit Dialog */}
        <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
          <DialogTitle>{editing ? 'Edit Preset' : 'New Preset'}</DialogTitle>
          <DialogContent>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
              <TextField
                label="Name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                fullWidth
              />

              <FormControl fullWidth>
                <InputLabel>Provider</InputLabel>
                <Select
                  value={form.provider}
                  label="Provider"
                  onChange={(e) => handleProviderChange(e.target.value)}
                >
                  {PROVIDERS.map((p) => (
                    <MenuItem key={p.value} value={p.value}>{p.label}</MenuItem>
                  ))}
                </Select>
              </FormControl>

              {form.provider !== 'ollama' && form.hostUrl && (
                <TextField
                  label="Host URL"
                  value={form.hostUrl}
                  onChange={(e) => setForm({ ...form, hostUrl: e.target.value })}
                  fullWidth
                  helperText="Leave blank for default"
                />
              )}

              {models && models.chatModels.length > 0 ? (
                <Autocomplete
                  options={models.chatModels}
                  value={form.baseUrlModel}
                  onChange={(_, val) => setForm({ ...form, baseUrlModel: val || '' })}
                  renderInput={(params) => (
                    <TextField {...params} label="Base Model" helperText="Or type freely" />
                  )}
                />
              ) : (
                <TextField
                  label="Base Model"
                  value={form.baseUrlModel}
                  onChange={(e) => setForm({ ...form, baseUrlModel: e.target.value })}
                  fullWidth
                />
              )}

              {models && models.embeddingModels.length > 0 ? (
                <Autocomplete
                  options={models.embeddingModels}
                  value={form.embeddingModel}
                  onChange={(_, val) => setForm({ ...form, embeddingModel: val || '' })}
                  renderInput={(params) => (
                    <TextField {...params} label="Embedding Model" helperText="Or type freely" />
                  )}
                />
              ) : (
                <TextField
                  label="Embedding Model"
                  value={form.embeddingModel}
                  onChange={(e) => setForm({ ...form, embeddingModel: e.target.value })}
                  fullWidth
                />
              )}

              <TextField
                label="API Key"
                type="password"
                value={form.apiKey}
                onChange={(e) => setForm({ ...form, apiKey: e.target.value })}
                fullWidth
                helperText={editing ? "Leave blank to keep current key" : "Required for this provider"}
              />

              <TextField
                label="System Prompt"
                multiline
                rows={4}
                value={form.systemPrompt}
                onChange={(e) => setForm({ ...form, systemPrompt: e.target.value })}
                fullWidth
              />

              <Box sx={{ display: 'flex', gap: 2 }}>
                <TextField
                  label="Temperature"
                  type="number"
                  value={form.temperature}
                  onChange={(e) => setForm({ ...form, temperature: parseFloat(e.target.value) || 0 })}
                  fullWidth
                  inputProps={{ min: 0, max: 2, step: 0.1 }}
                />
                <TextField
                  label="Max Tokens"
                  type="number"
                  value={form.maxTokens ?? ''}
                  onChange={(e) => setForm({ ...form, maxTokens: e.target.value ? parseInt(e.target.value) : undefined })}
                  fullWidth
                  InputProps={{ inputProps: { min: 1 } }}
                  InputLabelProps={{ shrink: true }}
                />
              </Box>
            </Box>
          </DialogContent>
          <DialogActions>
            <Button onClick={handleClose}>Cancel</Button>
            <Button onClick={handleSave} variant="contained" disabled={loading}>
              {loading ? 'Saving...' : 'Save'}
            </Button>
          </DialogActions>
        </Dialog>
      </Box>
    </Container>
  );
}
