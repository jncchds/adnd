import { useState } from 'react';
import { Dialog, DialogTitle, DialogContent, DialogActions, TextField, Button, Alert, AlertTitle } from '@mui/material';

interface AdminNPCDialogProps {
  open: boolean;
  onClose: () => void;
  onCreate: (name: string, description: string) => Promise<void>;
}

export default function AdminNPCDialog({ open, onClose, onCreate }: AdminNPCDialogProps) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleCreate = async () => {
    if (!name.trim()) return;
    try {
      await onCreate(name, description);
      setName('');
      setDescription('');
      onClose();
    } catch (e: any) {
      setError(e.message);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>New NPC</DialogTitle>
      <DialogContent sx={{ mt: 1 }}>
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            <AlertTitle>Error</AlertTitle>
            {error}
          </Alert>
        )}
        <TextField fullWidth label="Name" value={name} onChange={e => setName(e.target.value)} sx={{ mb: 2 }} autoFocus />
        <TextField fullWidth label="Description" multiline rows={3} value={description} onChange={e => setDescription(e.target.value)} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleCreate} variant="contained" disabled={!name.trim()}>Create</Button>
      </DialogActions>
    </Dialog>
  );
}
