import { useState } from 'react';
import { Box, Typography, Paper, Button, TextField, IconButton, InputAdornment, CircularProgress } from '@mui/material';
import { Lock as LockIcon, Visibility as EyeIcon, VisibilityOff as EyeOffIcon } from '@mui/icons-material';

export interface PasswordTabProps {
  onError: (msg: string) => void;
  onSuccess: (msg: string) => void;
  onRefreshUser: () => Promise<void>;
}

export default function PasswordTab({ onError, onSuccess, onRefreshUser }: PasswordTabProps) {
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleChangePassword = async () => {
    if (!currentPassword || !newPassword || !confirmPassword) {
      onError('All fields are required.');
      return;
    }
    if (newPassword !== confirmPassword) {
      onError('New passwords do not match.');
      return;
    }
    if (newPassword.length < 8) {
      onError('New password must be at least 8 characters.');
      return;
    }
    setLoading(true);
    try {
      await (window as any).api.changePassword(currentPassword, newPassword);
      onSuccess('Password changed successfully!');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      await onRefreshUser();
    } catch (e: any) {
      onError(e.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Paper sx={{ p: 3, borderRadius: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 3 }}>
        <LockIcon color="primary" />
        <Typography variant="h6" fontWeight={600}>Change Password</Typography>
      </Box>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 400 }}>
        <TextField
          fullWidth
          label="Current Password"
          type={showCurrent ? 'text' : 'password'}
          value={currentPassword}
          onChange={e => setCurrentPassword(e.target.value)}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton size="small" onClick={() => setShowCurrent(!showCurrent)}>
                  {showCurrent ? <EyeOffIcon fontSize="small" /> : <EyeIcon fontSize="small" />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />
        <TextField
          fullWidth
          label="New Password"
          type={showNew ? 'text' : 'password'}
          value={newPassword}
          onChange={e => setNewPassword(e.target.value)}
          helperText={newPassword.length >= 8 ? 'Minimum 8 characters' : `${8 - newPassword.length} more characters needed`}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton size="small" onClick={() => setShowNew(!showNew)}>
                  {showNew ? <EyeOffIcon fontSize="small" /> : <EyeIcon fontSize="small" />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />
        <TextField
          fullWidth
          label="Confirm New Password"
          type={showConfirm ? 'text' : 'password'}
          value={confirmPassword}
          onChange={e => setConfirmPassword(e.target.value)}
          helperText={confirmPassword && newPassword !== confirmPassword ? 'Passwords do not match' : ''}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton size="small" onClick={() => setShowConfirm(!showConfirm)}>
                  {showConfirm ? <EyeOffIcon fontSize="small" /> : <EyeIcon fontSize="small" />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />
        <Button
          variant="contained"
          onClick={handleChangePassword}
          disabled={loading || !currentPassword || !newPassword || !confirmPassword}
        >
          {loading ? <CircularProgress size={24} /> : 'Change Password'}
        </Button>
      </Box>
    </Paper>
  );
}

// ==================== LLM Statistics Tab ====================

