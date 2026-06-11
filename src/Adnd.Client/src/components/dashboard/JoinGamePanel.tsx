import { useState } from 'react';
import { Box, Paper, Typography, TextField, Button, Alert, AlertTitle } from '@mui/material';
import { Share as ShareIcon } from '@mui/icons-material';

interface JoinGamePanelProps {
  onJoin: (code: string) => Promise<void>;
  errorState: string | null;
  successState: string | null;
}

export default function JoinGamePanel({ onJoin, errorState, successState }: JoinGamePanelProps) {
  const [joinCode, setJoinCode] = useState('');
  const [showDialog, setShowDialog] = useState(false);

  const handleJoin = async () => {
    if (!joinCode.trim()) return;
    await onJoin(joinCode);
    setJoinCode('');
    setShowDialog(false);
  };

  return (
    <>
      <Paper elevation={1} sx={{ p: 3, mb: 3, borderRadius: 2, bgcolor: 'background.paper' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1.5 }}>
          <ShareIcon sx={{ color: 'primary.main' }} />
          <Typography variant="h6" sx={{ fontWeight: 600 }}>Join a Game</Typography>
        </Box>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>Enter the invite code to join an existing game.</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField fullWidth label="Invite Code" value={joinCode} onChange={e => setJoinCode(e.target.value)} placeholder="e.g., abc12345" sx={{ maxWidth: 300 }} onKeyDown={e => e.key === 'Enter' && handleJoin()} />
          <Button variant="contained" onClick={handleJoin} disabled={!joinCode.trim()}>Join</Button>
        </Box>
        {errorState && (
          <Alert severity="error" onClose={() => {}} sx={{ mt: 2 }}>
            <AlertTitle>Error</AlertTitle>
            {errorState}
          </Alert>
        )}
      </Paper>

      {successState && (
        <Alert severity="success" onClose={() => {}} sx={{ mb: 2, alignItems: 'center' }}>{successState}</Alert>
      )}

      {showDialog && (
        <Box sx={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, bgcolor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }} onClick={() => setShowDialog(false)}>
          <Paper sx={{ p: 3, m: 2, maxWidth: 400, width: '100%' }} onClick={e => e.stopPropagation()}>
            <Typography variant="h6" sx={{ mb: 2 }}>Join a Game</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>Enter the invite code shared by the game creator.</Typography>
            <TextField fullWidth label="Invite Code" value={joinCode} onChange={e => setJoinCode(e.target.value)} placeholder="e.g., abc12345 or /join/abc12345" autoFocus onKeyDown={e => e.key === 'Enter' && handleJoin()} />
            <Box sx={{ display: 'flex', gap: 1, mt: 2, justifyContent: 'flex-end' }}>
              <Button onClick={() => setShowDialog(false)}>Cancel</Button>
              <Button onClick={handleJoin} variant="contained" disabled={!joinCode.trim()}>Join</Button>
            </Box>
          </Paper>
        </Box>
      )}
    </>
  );
}
