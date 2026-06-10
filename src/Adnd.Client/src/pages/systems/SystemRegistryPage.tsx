import { useState, useEffect } from 'react';
import {
  Container, Paper, Typography, Box, Button, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Chip, Dialog, DialogTitle, DialogContent,
  DialogActions, TextField, Alert, IconButton, Stack,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import client from '@/api/client';

interface GameSystem {
  id: string;
  name: string;
  slug: string;
  description: string;
  type: 'predefined' | 'custom';
  rulesetConfig?: string;
  createdAt: string;
  updatedAt: string;
}

export default function SystemRegistryPage() {
  const [systems, setSystems] = useState<GameSystem[]>([]);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<GameSystem | null>(null);
  const [form, setForm] = useState({
    name: '',
    slug: '',
    description: '',
    rulesetConfig: '',
  });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const fetchSystems = async () => {
    const res = await client.get('/systems');
    setSystems(res.data);
  };

  useEffect(() => { fetchSystems(); }, []);

  const handleOpen = (system?: GameSystem) => {
    if (system) {
      setEditing(system);
      setForm({
        name: system.name,
        slug: system.slug,
        description: system.description,
        rulesetConfig: system.rulesetConfig || '',
      });
    } else {
      setEditing(null);
      setForm({ name: '', slug: '', description: '', rulesetConfig: '' });
    }
    setOpen(true);
  };

  const handleClose = () => {
    setOpen(false);
    setEditing(null);
  };

  const handleSave = async () => {
    setError('');
    try {
      if (editing) {
        await client.put(`/systems/${editing.id}`, form);
      } else {
        await client.post('/systems', form);
      }
      setSuccess('Saved');
      setTimeout(() => setSuccess(''), 3000);
      handleClose();
      fetchSystems();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Save failed');
    }
  };

  const handleDelete = async (id: string) => {
    await client.delete(`/systems/${id}`);
    fetchSystems();
  };

  return (
    <Container maxWidth="md">
      <Box sx={{ mt: 4 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
          <Typography variant="h4">Game Systems</Typography>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => handleOpen()}>
            New System
          </Button>
        </Box>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {success && <Alert severity="success" sx={{ mb: 2 }}>{success}</Alert>}

        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Slug</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Description</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {systems.map((system) => (
                <TableRow key={system.id}>
                  <TableCell>{system.name}</TableCell>
                  <TableCell><code>{system.slug}</code></TableCell>
                  <TableCell>
                    <Chip
                      label={system.type}
                      color={system.type === 'predefined' ? 'default' : 'primary'}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>{system.description}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1}>
                      <IconButton onClick={() => handleOpen(system)} size="small">
                        <EditIcon />
                      </IconButton>
                      {system.type === 'custom' && (
                        <IconButton onClick={() => handleDelete(system.id)} size="small" color="error">
                          <DeleteIcon />
                        </IconButton>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
          <DialogTitle>{editing ? 'Edit System' : 'New System'}</DialogTitle>
          <DialogContent sx={{ mt: 2, display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField
              fullWidth
              label="Name"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
            />
            <TextField
              fullWidth
              label="Slug"
              value={form.slug}
              onChange={(e) => setForm({ ...form, slug: e.target.value.toLowerCase().replace(/\s+/g, '-') })}
              helperText="Lowercase letters, numbers, and hyphens only"
            />
            <TextField
              fullWidth
              label="Description"
              multiline
              rows={3}
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
            />
            <TextField
              fullWidth
              label="Ruleset Config (JSON)"
              multiline
              rows={4}
              value={form.rulesetConfig}
              onChange={(e) => setForm({ ...form, rulesetConfig: e.target.value })}
              helperText="Optional JSON configuration"
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={handleClose}>Cancel</Button>
            <Button onClick={handleSave} variant="contained">Save</Button>
          </DialogActions>
        </Dialog>
      </Box>
    </Container>
  );
}
