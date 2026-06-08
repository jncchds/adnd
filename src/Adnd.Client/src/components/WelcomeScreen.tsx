import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../api/authHook';
import {
  Box,
  Typography,
  Button,
  Paper,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Tabs,
  Tab,
  Alert,
  AlertTitle,
} from '@mui/material';
import {
  SportsEsports as DiceIcon,
  Groups as GroupsIcon,
  AutoAwesome as LLMIcon,
  Speed as SpeedIcon,
  Palette as PaletteIcon,
  Shield as ShieldIcon,
} from '@mui/icons-material';

export default function WelcomeScreen() {
  const navigate = useNavigate();
  const { login, register, isLoading, user } = useAuth();
  const [authTab, setAuthTab] = useState(0);
  const [authOpen, setAuthOpen] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [authError, setAuthError] = useState<string | null>(null);
  const [authSuccess, setAuthSuccess] = useState<string | null>(null);

  // Redirect if already authenticated
  if (user) {
    navigate('/dashboard', { replace: true });
    return null;
  }

  const handleAuthSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthError(null);
    setAuthSuccess(null);

    try {
      if (authTab === 0) {
        await login(email, password);
        setAuthSuccess('Login successful!');
        setTimeout(() => {
          setAuthOpen(false);
          navigate('/dashboard');
        }, 500);
      } else {
        if (!displayName.trim()) {
          setAuthError('Display name is required.');
          return;
        }
        await register(email, password, displayName);
        setAuthSuccess('Registration successful!');
        setTimeout(() => {
          setAuthOpen(false);
          navigate('/dashboard');
        }, 500);
      }
    } catch (err: any) {
      setAuthError(err.message || 'Authentication failed.');
    }
  };

  const openAuthDialog = (tab = 0) => {
    setAuthTab(tab);
    setEmail('');
    setPassword('');
    setDisplayName('');
    setAuthError(null);
    setAuthSuccess(null);
    setAuthOpen(true);
  };

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      {/* Hero Section */}
      <Box sx={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        py: 6,
        px: 2,
      }}>
        {/* Title */}
        <Typography variant="h2" component="h1" gutterBottom sx={{
          fontWeight: 700,
          background: 'linear-gradient(135deg, #9147ff 0%, #f50057 100%)',
          WebkitBackgroundClip: 'text',
          WebkitTextFillColor: 'transparent',
          mb: 1,
        }}>
          ADnD
        </Typography>
        <Typography variant="h5" color="text.secondary" sx={{ mb: 1, fontWeight: 300 }}>
          Advanced Dungeon Network
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{
          mb: 5,
          maxWidth: 600,
          mx: 'auto',
          textAlign: 'center',
          lineHeight: 1.6,
        }}>
          Your multi-system TTRPG framework with LLM-powered Game Master assistance,
          real-time chat, and custom system support.
        </Typography>

        {/* CTA Buttons */}
        <Box sx={{ display: 'flex', gap: 2, mb: 8 }}>
          <Button
            variant="contained"
            size="large"
            onClick={() => openAuthDialog(0)}
            sx={{ px: 4, py: 1.5, fontSize: 16 }}
          >
            Log in
          </Button>
          <Button
            variant="outlined"
            size="large"
            onClick={() => openAuthDialog(1)}
            sx={{ px: 4, py: 1.5, fontSize: 16 }}
          >
            Register
          </Button>
        </Box>

        {/* Features Grid */}
        <Box sx={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: 3,
          maxWidth: 900,
          width: '100%',
        }}>
          {[
            { icon: <DiceIcon fontSize="large" />, title: 'Multi-System Support', desc: 'D&D 5e, Pathfinder 2e, Call of Cthulhu 7e, and custom systems' },
            { icon: <SpeedIcon fontSize="large" />, title: 'Real-Time Chat', desc: 'SignalR-powered game rooms with dice rolling and action tracking' },
            { icon: <LLMIcon fontSize="large" />, title: 'LLM Game Master', desc: 'Pluggable AI assistant for plot suggestions and consistency checks' },
            { icon: <GroupsIcon fontSize="large" />, title: 'NPC & Plot Management', desc: 'Track NPCs, plot threads, and game events with RAG-powered context' },
            { icon: <PaletteIcon fontSize="large" />, title: 'Custom Systems', desc: 'Define your own RPG systems with custom attributes, skills, and rules' },
            { icon: <ShieldIcon fontSize="large" />, title: 'Secure Auth', desc: 'JWT-based authentication with refresh tokens and role management' },
          ].map((feature, i) => (
            <Paper key={i} elevation={2} sx={{
              height: '100%',
              display: 'flex',
              flexDirection: 'column',
              p: 3,
              borderRadius: 2,
            }}>
              <Box sx={{ color: 'primary.main', mb: 1, textAlign: 'center' }}>
                {feature.icon}
              </Box>
              <Typography variant="h6" gutterBottom sx={{ textAlign: 'center' }}>
                {feature.title}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', lineHeight: 1.5 }}>
                {feature.desc}
              </Typography>
            </Paper>
          ))}
        </Box>
      </Box>

      {/* Footer */}
      <Typography variant="body2" color="text.secondary" sx={{
        textAlign: 'center',
        py: 3,
        borderTop: '1px solid rgba(255,255,255,0.06)',
      }}>
        ADnD · Built with ASP.NET Core 10, React 19, PostgreSQL + PGVector
      </Typography>

      {/* Auth Modal */}
      <Dialog open={authOpen} onClose={() => setAuthOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ pb: 1 }}>
          {authTab === 0 ? 'Log in' : 'Create Account'}
        </DialogTitle>
        <DialogContent sx={{ mt: 1 }}>
          {/* Tabs */}
          <Tabs value={authTab} onChange={(_, v) => setAuthTab(v)} sx={{ mb: 2 }}>
            <Tab label="Log in" />
            <Tab label="Register" />
          </Tabs>

          <form onSubmit={handleAuthSubmit}>
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

            {authTab === 1 && (
              <TextField
                fullWidth
                label="Display Name"
                value={displayName}
                onChange={e => setDisplayName(e.target.value)}
                sx={{ mb: 2 }}
                required
              />
            )}

            {authError && (
              <Alert severity="error" sx={{ mb: 2 }}>
                <AlertTitle>Error</AlertTitle>
                {authError}
              </Alert>
            )}

            {authSuccess && (
              <Alert severity="success" sx={{ mb: 2 }}>
                <AlertTitle>Success</AlertTitle>
                {authSuccess}
              </Alert>
            )}

            <Button
              type="submit"
              variant="contained"
              size="large"
              fullWidth
              disabled={isLoading}
              sx={{ mt: 1 }}
            >
              {authTab === 0 ? 'Log in' : 'Register'}
            </Button>
          </form>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setAuthOpen(false)}>Cancel</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
