import { useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Button, Alert, AlertTitle,
} from '@mui/material';

interface JoinGameDialogProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (code: string) => void;
  error: string | null;
  success: string | null;
}

export default function JoinGameDialog({ open, onClose, onSubmit, error, success }: JoinGameDialogProps) {
  const [joinCode, setJoinCode] = useState('');

  const handleSubmit = () => {
    if (!joinCode.trim()) return;
    onSubmit(joinCode);
    setJoinCode('');
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Join a Game</DialogTitle>
      <DialogContent sx={{ mt: 1 }}>
        <TextField fullWidth label="Invite Code" value={joinCode} onChange={e => setJoinCode(e.target.value)} placeholder="Enter the 8-character invite code" autoFocus />
        {error && <Alert severity="error" onClose={() => {}} sx={{ mt: 2 }}><AlertTitle>Error</AlertTitle>{error}</Alert>}
        {success && <Alert severity="success" onClose={() => {}} sx={{ mt: 2 }}>{success}</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSubmit} variant="contained" disabled={!joinCode.trim()}>Join</Button>
      </DialogActions>
    </Dialog>
  );
}
