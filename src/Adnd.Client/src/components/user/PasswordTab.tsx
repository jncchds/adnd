import { useState } from 'react';
import { Box, Typography, Paper, Button, TextField, IconButton, InputAdornment } from '@mui/material';
import { Visibility as EyeIcon, VisibilityOff as EyeOffIcon } from '@mui/icons-material';

interface PasswordTabProps {
  onError: (msg: string) => void;
  onSuccess: (msg: string) => void;
  onRefreshUser: () => Promise<void>;
}

export default function PasswordTab({ onError, onSuccess, onRefreshUser }: PasswordTabProps) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleChangePassword = async () => {
    if (newPassword !== confirmPassword) {
      onError('New passwords do not match');
      return;
    }
    if (newPassword.length < 8) {
      onError('Password must be at least 8 characters');
      return;
    }
    setLoading(true);
    try {
      await onRefreshUser();
      onSuccess('Password updated successfully');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (e: any) {
      onError(e.message || 'Failed to update password');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Paper sx={{ p: 3 }}>
      <Typography variant="h6" sx={{ mb: 2 }}>Change Password</Typography>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 400 }}>
        <TextField fullWidth label="Current Password" type={showCurrent ? 'text' : 'password'} value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} InputProps={{ endAdornment: <InputAdornment position="end"><IconButton onClick={() => setShowCurrent(!showCurrent)} edge="end">{showCurrent ? <EyeOffIcon /> : <EyeIcon />}</IconButton></InputAdornment> }} />
        <TextField fullWidth label="New Password" type={showNew ? 'text' : 'password'} value={newPassword} onChange={e => setNewPassword(e.target.value)} InputProps={{ endAdornment: <InputAdornment position="end"><IconButton onClick={() => setShowNew(!showNew)} edge="end">{showNew ? <EyeOffIcon /> : <EyeIcon />}</IconButton></InputAdornment> }} />
        <TextField fullWidth label="Confirm New Password" type={showConfirm ? 'text' : 'password'} value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} InputProps={{ endAdornment: <InputAdornment position="end"><IconButton onClick={() => setShowConfirm(!showConfirm)} edge="end">{showConfirm ? <EyeOffIcon /> : <EyeIcon />}</IconButton></InputAdornment> }} />
        <Button variant="contained" onClick={handleChangePassword} disabled={loading || !currentPassword || !newPassword || !confirmPassword}>
          {loading ? 'Updating...' : 'Update Password'}
        </Button>
      </Box>
    </Paper>
  );
}
