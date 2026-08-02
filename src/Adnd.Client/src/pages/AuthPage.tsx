import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Box, Paper, Typography, TextField, Button, Tabs, Tab, Alert, CircularProgress,
} from '@mui/material'
import { Casino as DiceIcon } from '@mui/icons-material'
import { useAuth } from '../context/AuthContext'

export default function AuthPage() {
  const { login, register } = useAuth()
  const navigate = useNavigate()
  const [tab, setTab] = useState(0)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handle = async () => {
    setError('')
    setLoading(true)
    try {
      if (tab === 0) {
        await login(email, password)
      } else {
        if (!displayName.trim()) { setError('Display name is required'); setLoading(false); return }
        await register(email, password, displayName)
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
      minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center',
      bgcolor: 'background.default', p: 2,
    }}>
      <Paper elevation={3} sx={{ p: 4, width: '100%', maxWidth: 400 }}>
        <Box sx={{ textAlign: 'center', mb: 3 }}>
          <DiceIcon sx={{ fontSize: 48, color: 'primary.main' }} />
          <Typography variant="h5" fontWeight={700} color="primary">ADnD</Typography>
          <Typography variant="caption" color="text.secondary">AI-Powered TTRPG Platform</Typography>
        </Box>

        <Tabs value={tab} onChange={(_, v) => { setTab(v); setError('') }} centered sx={{ mb: 3 }}>
          <Tab label="Sign In" />
          <Tab label="Register" />
        </Tabs>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField label="Email" type="email" value={email} onChange={e => setEmail(e.target.value)}
            fullWidth size="small" autoComplete="email" />
          <TextField label="Password" type="password" value={password}
            onChange={e => setPassword(e.target.value)} fullWidth size="small" autoComplete={tab === 0 ? 'current-password' : 'new-password'} />
          {tab === 1 && (
            <TextField label="Display Name" value={displayName}
              onChange={e => setDisplayName(e.target.value)} fullWidth size="small" />
          )}
          <Button variant="contained" fullWidth onClick={handle} disabled={loading}
            sx={{ mt: 1, py: 1.25 }}>
            {loading ? <CircularProgress size={20} color="inherit" /> : tab === 0 ? 'Sign In' : 'Create Account'}
          </Button>
        </Box>
      </Paper>
    </Box>
  )
}
