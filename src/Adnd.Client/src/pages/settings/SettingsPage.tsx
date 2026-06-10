import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Container,
  Paper,
  Typography,
  Box,
  TextField,
  Button,
  Alert,
  Divider,
  Tabs,
  Tab,
} from '@mui/material';
import client from '@/api/client';

interface UserProfile {
  id: string;
  email: string;
  displayName: string;
}

export default function SettingsPage() {
  const navigate = useNavigate();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [tab, setTab] = useState(0);
  const [displayName, setDisplayName] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchProfile();
  }, []);

  const fetchProfile = async () => {
    try {
      const res = await client.get('/users/me');
      setProfile(res.data);
      setDisplayName(res.data.displayName);
    } catch {
      navigate('/login');
    }
  };

  const handleDisplayName = async () => {
    setError('');
    setLoading(true);
    try {
      await client.put('/users/me/display-name', { displayName });
      setSuccess('Display name updated');
      setTimeout(() => setSuccess(''), 3000);
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to update');
    } finally {
      setLoading(false);
    }
  };

  const handleChangePassword = async () => {
    setError('');
    setLoading(true);
    try {
      await client.put('/users/me/password', {
        currentPassword,
        newPassword,
      });
      setSuccess('Password changed');
      setCurrentPassword('');
      setNewPassword('');
      setTimeout(() => setSuccess(''), 3000);
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to change password');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = async () => {
    try {
      await client.post('/auth/logout');
    } finally {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
      navigate('/login');
    }
  };

  if (!profile) return null;

  return (
    <Container maxWidth="md">
      <Box sx={{ mt: 4 }}>
        <Typography variant="h4" gutterBottom>Settings</Typography>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {success && <Alert severity="success" sx={{ mb: 2 }}>{success}</Alert>}

        <Paper elevation={2}>
          <Tabs value={tab} onChange={(_, v) => setTab(v)}>
            <Tab label="Profile" />
            <Tab label="Password" />
          </Tabs>

          <Box sx={{ p: 3 }}>
            {tab === 0 && (
              <>
                <Typography variant="subtitle1" gutterBottom>Email</Typography>
                <TextField
                  fullWidth
                  value={profile.email}
                  disabled
                  sx={{ mb: 3 }}
                />
                <Typography variant="subtitle1" gutterBottom>Display Name</Typography>
                <Box sx={{ display: 'flex', gap: 2 }}>
                  <TextField
                    fullWidth
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                  />
                  <Button
                    variant="contained"
                    onClick={handleDisplayName}
                    disabled={loading}
                  >
                    Save
                  </Button>
                </Box>
              </>
            )}

            {tab === 1 && (
              <>
                <TextField
                  fullWidth
                  label="Current Password"
                  type="password"
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  sx={{ mb: 2 }}
                />
                <TextField
                  fullWidth
                  label="New Password"
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  sx={{ mb: 3 }}
                  helperText="Must be at least 8 characters"
                />
                <Button
                  variant="contained"
                  onClick={handleChangePassword}
                  disabled={loading}
                >
                  Change Password
                </Button>
              </>
            )}
          </Box>
        </Paper>

        <Divider sx={{ my: 3 }} />

        <Button variant="outlined" color="error" onClick={handleLogout}>
          Logout
        </Button>
      </Box>
    </Container>
  );
}
