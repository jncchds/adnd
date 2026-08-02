import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Box, Paper, Typography, TextField, Button, Alert, CircularProgress, Link,
} from '@mui/material'
import { Casino as DiceIcon } from '@mui/icons-material'
import { useAuth } from '../context/AuthContext'

/**
 * Sign-in and registration are two routes rather than two tabs: they are navigation
 * destinations in the public sidebar, and a tab index cannot be linked to. The form itself is
 * shared because the only differences are the display-name field and the labels.
 */
export default function AuthPage({ mode }: { mode: 'login' | 'register' }) {
  const { login, register } = useAuth()
  const navigate = useNavigate()
  const isRegister = mode === 'register'
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handle = async () => {
    setError('')
    setLoading(true)
    try {
      if (isRegister) {
        if (!displayName.trim()) { setError('Display name is required'); setLoading(false); return }
        await register(email, password, displayName)
      } else {
        await login(email, password)
      }
      navigate('/dashboard')
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box sx={{
      flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center',
      px: 2, py: { xs: 4, md: 8 },
    }}>
      <Paper elevation={3} sx={{ p: 4, width: '100%', maxWidth: 420 }}>
        <Box sx={{ textAlign: 'center', mb: 3 }}>
          <DiceIcon sx={{ fontSize: 48, color: 'primary.main' }} />
          <Typography variant="h5" fontWeight={700} color="primary">
            {isRegister ? 'Create your account' : 'Welcome back'}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {isRegister ? 'Start a campaign or join one with a code' : 'Sign in to your table'}
          </Typography>
        </Box>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        <Box
          component="form"
          onSubmit={(e: React.FormEvent) => { e.preventDefault(); handle() }}
          sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}
        >
          <TextField label="Email" type="email" value={email} onChange={e => setEmail(e.target.value)}
            fullWidth size="small" autoComplete="email" />
          <TextField label="Password" type="password" value={password}
            onChange={e => setPassword(e.target.value)} fullWidth size="small"
            autoComplete={isRegister ? 'new-password' : 'current-password'} />
          {isRegister && (
            <TextField label="Display Name" value={displayName}
              onChange={e => setDisplayName(e.target.value)} fullWidth size="small" />
          )}
          <Button type="submit" variant="contained" fullWidth disabled={loading} sx={{ mt: 1, py: 1.25 }}>
            {loading ? <CircularProgress size={20} color="inherit" /> : isRegister ? 'Create Account' : 'Sign In'}
          </Button>
        </Box>

        <Typography variant="body2" color="text.secondary" sx={{ mt: 3, textAlign: 'center' }}>
          {isRegister ? 'Already have an account? ' : "Don't have an account? "}
          <Link
            component="button"
            type="button"
            onClick={() => navigate(isRegister ? '/login' : '/register')}
            sx={{ verticalAlign: 'baseline' }}
          >
            {isRegister ? 'Sign in' : 'Register'}
          </Link>
        </Typography>
      </Paper>
    </Box>
  )
}
