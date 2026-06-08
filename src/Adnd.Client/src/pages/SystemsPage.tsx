import { useState } from 'react';
import { useGameHub } from '../api/hubHook';
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
  Alert,
  AlertTitle,
  Chip,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  IconButton,
  Divider,
} from '@mui/material';
import {
  Add as AddIcon,
  Delete as DeleteIcon,
  Code as CodeIcon,
} from '@mui/icons-material';

// Builtin systems
const BUILTIN_SYSTEMS = [
  { id: 'dnd5e', name: 'D&D 5th Edition', version: '5.4' },
  { id: 'pf2e', name: 'Pathfinder 2nd Edition', version: '2.3' },
  { id: 'coc7e', name: 'Call of Cthulhu 7th Edition', version: '7.1' },
];

export default function SystemsPage() {
  const { invoke } = useGameHub();
  const [systems, setSystems] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [openDialog, setOpenDialog] = useState(false);
  const [systemName, setSystemName] = useState('');
  const [systemJson, setSystemJson] = useState('{"name": "", "attributes": [], "skills": [], "defaultHP": 10}');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const loadSystems = async () => {
    // Get gameId from URL
    const gameId = window.location.pathname.match(/\/game\/([a-f0-9-]+)/)?.[1];
    if (!gameId) return;
    setLoading(true);
    try {
      const result = await invoke('GetSystems', gameId);
      if (result) setSystems(result);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    if (!systemName.trim()) return;
    setError(null);
    setSuccess(null);
    try {
      const gameId = window.location.pathname.match(/\/game\/([a-f0-9-]+)/)?.[1];
      if (!gameId) {
        setError('No game context. Create systems within a game.');
        return;
      }
      await invoke('CreateSystem', gameId, systemName, systemJson);
      setOpenDialog(false);
      setSystemName('');
      setSystemJson('{"name": "", "attributes": [], "skills": [], "defaultHP": 10}');
      setSuccess('System created!');
      setTimeout(() => setSuccess(null), 2000);
      await loadSystems();
    } catch (e: any) {
      setError(e.message);
    }
  };

  return (
    <Box>
      {/* Title */}
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>
        Systems
      </Typography>

      {/* Error / Success */}
      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          <AlertTitle>Error</AlertTitle>
          {error}
        </Alert>
      )}
      {success && (
        <Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2, alignItems: 'center' }}>
          {success}
        </Alert>
      )}

      {/* Builtin Systems */}
      <Paper sx={{ p: 3, mb: 3, borderRadius: 2 }}>
        <Typography variant="h6" gutterBottom sx={{ fontWeight: 600 }}>
          Built-in Systems
        </Typography>
        <Divider sx={{ mb: 2 }} />
        <List dense>
          {BUILTIN_SYSTEMS.map(sys => (
            <ListItem key={sys.id} sx={{ px: 0, mb: 0.5 }}>
              <ListItemText
                primary={
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Typography variant="body1" sx={{ fontWeight: 500 }}>{sys.name}</Typography>
                    <Chip label={`v${sys.version}`} size="small" variant="outlined" />
                    <Chip label="Built-in" size="small" color="default" />
                  </Box>
                }
                secondary={`System ID: ${sys.id}`}
              />
            </ListItem>
          ))}
        </List>
      </Paper>

      {/* Custom Systems */}
      <Paper sx={{ p: 3, borderRadius: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
          Custom Systems ({systems.length})
        </Typography>
        <Divider sx={{ mb: 2 }} />

        {loading ? (
          <Typography color="text.secondary" sx={{ textAlign: 'center', py: 2 }}>Loading...</Typography>
        ) : systems.length === 0 ? (
          <Box sx={{ textAlign: 'center', py: 4 }}>
            <Typography color="text.secondary" sx={{ mb: 2 }}>
              No custom systems yet. Create one to define your own RPG rules.
            </Typography>
            <Button variant="contained" onClick={() => setOpenDialog(true)} startIcon={<AddIcon />}>New System</Button>
          </Box>
        ) : (
          <List dense>
            {systems.map((sys, i) => (
              <ListItem key={i} sx={{ px: 0, mb: 0.5, bgcolor: 'action.hover', borderRadius: 1 }}>
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <CodeIcon fontSize="small" color="secondary" />
                      <Typography variant="body1" sx={{ fontWeight: 500 }}>{sys.name}</Typography>
                      <Chip label="Custom" size="small" color="secondary" variant="outlined" />
                    </Box>
                  }
                  secondary={`Created: ${new Date(sys.createdAt).toLocaleDateString()}`}
                />
                <ListItemSecondaryAction>
                  <IconButton size="small" color="error">
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </ListItemSecondaryAction>
              </ListItem>
            ))}
          </List>
        )}
      </Paper>

      {/* Create System Dialog */}
      <Dialog open={openDialog} onClose={() => setOpenDialog(false)} maxWidth="md" fullWidth>
        <DialogTitle>Create Custom System</DialogTitle>
        <DialogContent sx={{ mt: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            fullWidth
            label="System Name"
            value={systemName}
            onChange={e => setSystemName(e.target.value)}
            placeholder="My Custom RPG"
            autoFocus
          />
          <TextField
            fullWidth
            label="JSON Definition"
            multiline
            rows={10}
            value={systemJson}
            onChange={e => setSystemJson(e.target.value)}
            placeholder='{"name": "...", "attributes": [...], "skills": [...]}'
            sx={{ fontFamily: 'monospace' }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenDialog(false)}>Cancel</Button>
          <Button onClick={handleCreate} variant="contained" disabled={!systemName.trim()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
