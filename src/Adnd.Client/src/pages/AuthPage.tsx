import { useState, useEffect } from 'react';
import { useAuth } from '../api/hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { Container, Box, Typography, TextField, Button, Paper, Tabs, Tab, Alert, AlertTitle } from '@mui/material';

export default function AuthPage() {
  const [tab, setTab] = useState(0);
  const { login, register, isLoading, user } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Redirect if already authenticated
  useEffect(() => {
    if (user) {
      navigate('/dashboard', { replace: true });
    }
  }, [user, navigate]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    try {
      if (tab === 0) {
        await login(email, password);
        setSuccess('Login successful! Redirecting...');
        setTimeout(() => navigate('/dashboard'), 800);
      } else {
        if (!displayName.trim()) {
          setError('Display name is required.');
          return;
        }
        await register(email, password, displayName);
        setSuccess('Registration successful! Redirecting...');
        setTimeout(() => navigate('/dashboard'), 800);
      }
    } catch (err: any) {
      setError(err.message || 'Authentication failed.');
    }
  };

  return (
    <Container maxWidth="sm">
      <Box sx={{ mt: { xs: 4, sm: 8 }, textAlign: 'center', px: { xs: 1, sm: 0 } }}>
        <Typography sx={{ fontSize: { xs: '1.25rem', sm: '1.5rem' }, mb: 2 }}>
          {tab === 0 ? 'Login' : 'Register'}
        </Typography>

        <Paper elevation={3} sx={{ p: { xs: 2, sm: 4 }, mt: 2, borderRadius: 2 }}>
          <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 3 }}>
            <Tab label="Login" />
            <Tab label="Register" />
          </Tabs>

          <form onSubmit={handleSubmit}>
            <TextField
              fullWidth
              label="Email"
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              sx={{ mb: 2 }}
              required
            />

            <TextField
              fullWidth
              label="Password"
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              sx={{ mb: 2 }}
              required
              inputProps={{ minLength: 8 }}
            />

            {tab === 1 && (
              <TextField
                fullWidth
                label="Display Name"
                value={displayName}
                onChange={e => setDisplayName(e.target.value)}
                sx={{ mb: 2 }}
                required
              />
            )}

            {error && (
              <Alert severity="error" sx={{ mb: 2 }}>
                <AlertTitle>Error</AlertTitle>
                {error}
              </Alert>
            )}

            {success && (
              <Alert severity="success" sx={{ mb: 2 }}>
                <AlertTitle>Success</AlertTitle>
                {success}
              </Alert>
            )}

            <Button
              type="submit"
              variant="contained"
              size="large"
              fullWidth
              disabled={isLoading}
              sx={{ mt: 2, py: 1.5 }}
            >
              {tab === 0 ? 'Login' : 'Register'}
            </Button>
          </form>
        </Paper>
      </Box>
    </Container>
  );
}
